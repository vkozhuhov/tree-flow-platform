using System.Text.Json;
using System.Threading.Channels;
using ApplicationProcessor.Config;
using ApplicationProcessor.Data;
using ApplicationProcessor.Interfaces;
using Confluent.Kafka;
using MCDis256.Design.App.Interface;
using MCDis256.Design.App.Interface.Log;
using MCDis256.Design.App.Interface.Log.Toolkit;

namespace ApplicationProcessor.Services;

/// <summary>
/// Сервис для потребления сообщений из Kafka и обработки заявок
/// </summary>
internal class KafkaConsumerService : IAppComponent<KafkaConsumerService>
{
  private readonly ILog p_log;
  private readonly ProcessorConfig p_config;
  private readonly IValidationService p_validationService;
  private readonly IApplicationRepository p_repository;
  private readonly IStatisticsService p_statisticsService;
  private readonly IConsumer<string, string> p_consumer;
  private readonly IProducer<string, string> p_producer;
  private readonly Channel<Application> p_processingChannel;

  public KafkaConsumerService(
    ILog _log,
    ProcessorConfig _config,
    IValidationService _validationService,
    IApplicationRepository _repository,
    IStatisticsService _statisticsService)
  {
    p_log = _log["kafka-consumer"];
    p_config = _config;
    p_validationService = _validationService;
    p_repository = _repository;
    p_statisticsService = _statisticsService;

    // Создаем канал для параллельной обработки
    p_processingChannel = Channel.CreateUnbounded<Application>();

    // Kafka Consumer
    var consumerConfig = new ConsumerConfig
    {
      BootstrapServers = _config.KafkaBroker,
      GroupId = _config.KafkaGroupId,
      AutoOffsetReset = AutoOffsetReset.Earliest,
      EnableAutoCommit = false
    };
    p_consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();

    // Kafka Producer
    var producerConfig = new ProducerConfig
    {
      BootstrapServers = _config.KafkaBroker
    };
    p_producer = new ProducerBuilder<string, string>(producerConfig).Build();

    p_log.Info("KafkaConsumerService инициализирован");
  }

  public static KafkaConsumerService Activate(IAppContext _ctx) =>
    _ctx.Activate((ILog _log, ProcessorConfig _config, IValidationService _validationService,
      IApplicationRepository _repository, IStatisticsService _statisticsService) =>
      new KafkaConsumerService(_log, _config, _validationService, _repository, _statisticsService));

  public async Task StartAsync(CancellationToken _ct)
  {
    p_log.Info($"Запуск Kafka consumer (топик: {p_config.KafkaApplicationTopic})...");

    p_consumer.Subscribe(p_config.KafkaApplicationTopic);

    // Запускаем воркеры для параллельной обработки
    var workers = Enumerable.Range(0, p_config.MaxParallelTasks)
      .Select(_i => Task.Run(() => ProcessingWorkerAsync(_i, _ct), _ct))
      .ToList();

    // Запускаем периодическое сохранение статистики
    p_statisticsService.StartPeriodicSave(_ct);

    try
    {
      while (!_ct.IsCancellationRequested)
      {
        try
        {
          var consumeResult = p_consumer.Consume(_ct);

          if (consumeResult?.Message != null)
          {
            var application = JsonSerializer.Deserialize<Application>(consumeResult.Message.Value);

            if (application != null)
            {
              p_log.Info($"Получена заявка {application.Id} из Kafka");
              await p_processingChannel.Writer.WriteAsync(application, _ct);
            }

            p_consumer.Commit(consumeResult);
          }
        }
        catch (ConsumeException ex)
        {
          p_log.Error($"Ошибка Kafka consumer: {ex.Error.Reason}");
        }
        catch (OperationCanceledException)
        {
          break;
        }
        catch (Exception ex)
        {
          p_log.Error($"Ошибка обработки сообщения: {ex.Message}");
        }
      }
    }
    finally
    {
      p_processingChannel.Writer.Complete();
      await Task.WhenAll(workers);
      p_consumer.Close();
      p_producer.Dispose();
      p_log.Info("Kafka consumer остановлен");
    }
  }

  private async Task ProcessingWorkerAsync(int _workerId, CancellationToken _ct)
  {
    p_log.Info($"Воркер {_workerId} запущен");

    try
    {
      await foreach (var application in p_processingChannel.Reader.ReadAllAsync(_ct))
      {
        await ProcessApplicationAsync(application, _workerId, _ct);
      }
    }
    catch (OperationCanceledException)
    {
      // Нормальная остановка
    }
    catch (Exception ex)
    {
      p_log.Error($"Ошибка воркера {_workerId}: {ex.Message}");
    }

    p_log.Info($"Воркер {_workerId} остановлен");
  }

  private async Task ProcessApplicationAsync(Application _application, int _workerId, CancellationToken _ct)
  {
    try
    {
      p_log.Info($"[Воркер {_workerId}] Обработка заявки {_application.Id} (канал: {_application.Channel}, вес: {_application.Weight})");

      // Валидация
      var validationResult = p_validationService.ValidateApplication(_application);

      if (!validationResult.IsValid)
      {
        p_log.Warning($"[Воркер {_workerId}] Валидация заявки {_application.Id} не пройдена (канал: {_application.Channel})");
        p_statisticsService.GetStatistics().IncrementValidationErrors();
        await SendErrorToKafkaAsync(_application, string.Join("; ", validationResult.Errors), _ct);
        return;
      }

      // Сохранение в БД
      var saved = await p_repository.SaveApplicationAsync(_application, _ct);

      if (!saved)
      {
        p_log.Error($"[Воркер {_workerId}] Не удалось сохранить заявку {_application.Id} (канал: {_application.Channel})");
        p_statisticsService.GetStatistics().IncrementFailed();
        await SendErrorToKafkaAsync(_application, "Не удалось сохранить в БД", _ct);
        return;
      }

      // Обновление статуса
      await p_repository.UpdateApplicationStatusAsync(_application.Id, "processed", null, _ct);

      // Обновление статистики
      p_statisticsService.GetStatistics().IncrementProcessed();
      p_statisticsService.GetStatistics().IncrementChannel(_application.Channel);

      // Отправка результата в Kafka
      await SendSuccessToKafkaAsync(_application, _ct);

      p_log.Info($"[Воркер {_workerId}] Заявка {_application.Id} успешно обработана (канал: {_application.Channel})");
    }
    catch (Exception ex)
    {
      p_log.Error($"[Воркер {_workerId}] Ошибка обработки заявки {_application.Id}: {ex.Message}");
      p_statisticsService.GetStatistics().IncrementFailed();
      await SendErrorToKafkaAsync(_application, ex.Message, _ct);
    }
  }

  private async Task SendSuccessToKafkaAsync(Application _application, CancellationToken _ct)
  {
    var result = new
    {
      ApplicationId = _application.Id,
      Status = "success",
      ProcessedAt = DateTime.UtcNow
    };

    var message = new Message<string, string>
    {
      Key = _application.Id.ToString(),
      Value = JsonSerializer.Serialize(result)
    };

    await p_producer.ProduceAsync(p_config.KafkaResultTopic, message, _ct);
  }

  private async Task SendErrorToKafkaAsync(Application _application, string _error, CancellationToken _ct)
  {
    var result = new
    {
      ApplicationId = _application.Id,
      Status = "error",
      Error = _error,
      ProcessedAt = DateTime.UtcNow
    };

    var message = new Message<string, string>
    {
      Key = _application.Id.ToString(),
      Value = JsonSerializer.Serialize(result)
    };

    await p_producer.ProduceAsync(p_config.KafkaResultTopic, message, _ct);
  }
}
