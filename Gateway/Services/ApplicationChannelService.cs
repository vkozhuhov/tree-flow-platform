using System.Threading.Channels;
using Gateway.Config;
using Gateway.Data;
using Gateway.Interfaces;
using MCDis256.Design.App.Interface;
using MCDis256.Design.App.Interface.Log;
using MCDis256.Design.App.Interface.Log.Toolkit;

namespace Gateway.Services;

/// <summary>
/// Сервис управления каналами обработки заявок
/// </summary>
internal class ApplicationChannelService : IApplicationChannelService, IAppComponent<IApplicationChannelService>
{
  private readonly ILog p_log;
  private readonly GatewayConfig p_config;
  private readonly IKafkaProducerService p_kafkaProducer;
  private readonly IGrpcFileStorageClient p_grpcClient;

  // Три канала для обработки заявок
  private readonly Channel<Application> p_priorityChannel;
  private readonly Channel<Application> p_mainChannel;
  private readonly Channel<Application> p_secondaryChannel;

  private readonly Random p_random = new();

  public ApplicationChannelService(
    ILog _log,
    GatewayConfig _config,
    IKafkaProducerService _kafkaProducer,
    IGrpcFileStorageClient _grpcClient)
  {
    p_log = _log;
    p_config = _config;
    p_kafkaProducer = _kafkaProducer;
    p_grpcClient = _grpcClient;

    // Создаем unbounded каналы (без ограничений по размеру)
    p_priorityChannel = Channel.CreateUnbounded<Application>();
    p_mainChannel = Channel.CreateUnbounded<Application>();
    p_secondaryChannel = Channel.CreateUnbounded<Application>();
  }

  public static IApplicationChannelService Activate(IAppContext _ctx) =>
    _ctx.Activate((ILog _log, GatewayConfig _config, IKafkaProducerService _kafka, IGrpcFileStorageClient _grpc) =>
      new ApplicationChannelService(_log, _config, _kafka, _grpc));

  public async Task<ApplicationResponse> SubmitApplicationAsync(ApplicationRequest _request, CancellationToken _ct)
  {
    var application = new Application
    {
      Id = Guid.NewGuid(),
      Weight = _request.Weight,
      Channel = DetermineChannel(_request.Weight),
      Data = _request.Data,
      Files = _request.Files,
      CreatedAt = DateTime.UtcNow
    };

    // Отправляем в соответствующий канал
    await WriteToChannelAsync(application, _ct);

    p_log.Info($"Application {application.Id} submitted to {application.Channel} channel (weight: {application.Weight})");

    return new ApplicationResponse
    {
      Id = application.Id,
      Channel = application.Channel,
      Weight = application.Weight,
      CreatedAt = application.CreatedAt
    };
  }

  public async Task StartProcessingAsync(CancellationToken _ct)
  {
    p_log.Info("Начало обработки каналов заявок с принципом ТМО (Weighted Round Robin)...");
    p_log.Info("Веса каналов: Priority=3, Main=2, Secondary=1");

    await ProcessChannelsWithPriorityAsync(_ct);
  }

  /// <summary>
  /// Обработка каналов с приоритизацией (Weighted Round Robin)
  /// Priority: 3 заявки, Main: 2 заявки, Secondary: 1 заявка
  /// </summary>
  private async Task ProcessChannelsWithPriorityAsync(CancellationToken _ct)
  {
    while (!_ct.IsCancellationRequested)
    {
      var processed = false;

      // Обрабатываем до 3 заявок из приоритетного канала
      for (var i = 0; i < 3 && !_ct.IsCancellationRequested; i++)
      {
        if (await TryProcessFromChannelAsync(p_priorityChannel.Reader, ChannelType.Priority, _ct))
        {
          processed = true;
        }
        else
        {
          break;
        }
      }

      // Обрабатываем до 2 заявок из основного канала
      for (var i = 0; i < 2 && !_ct.IsCancellationRequested; i++)
      {
        if (await TryProcessFromChannelAsync(p_mainChannel.Reader, ChannelType.Main, _ct))
        {
          processed = true;
        }
        else
        {
          break;
        }
      }

      // Обрабатываем 1 заявку из вторичного канала
      if (await TryProcessFromChannelAsync(p_secondaryChannel.Reader, ChannelType.Secondary, _ct))
      {
        processed = true;
      }

      // Если ни один канал не дал заявок, немного подождём
      if (!processed)
      {
        await Task.Delay(100, _ct);
      }
    }

    p_log.Info("Обработка каналов остановлена");
  }

  /// <summary>
  /// Пытается прочитать и обработать одну заявку из канала
  /// </summary>
  private async Task<bool> TryProcessFromChannelAsync(
    ChannelReader<Application> _reader,
    ChannelType _channelType,
    CancellationToken _ct)
  {
    if (_reader.TryRead(out var application))
    {
      try
      {
        await ProcessApplicationAsync(application, _ct);
        return true;
      }
      catch (Exception ex)
      {
        p_log.Error($"Ошибка обработки заявки {application.Id} из канала {_channelType}: {ex.Message}");
        return false;
      }
    }

    return false;
  }

  private ChannelType DetermineChannel(int _weight)
  {
    return _weight switch
    {
      > 80 => ChannelType.Priority,
      >= 40 => ChannelType.Main,
      _ => ChannelType.Secondary
    };
  }

  private async Task WriteToChannelAsync(Application _application, CancellationToken _ct)
  {
    var writer = _application.Channel switch
    {
      ChannelType.Priority => p_priorityChannel.Writer,
      ChannelType.Main => p_mainChannel.Writer,
      ChannelType.Secondary => p_secondaryChannel.Writer,
      _ => throw new ArgumentOutOfRangeException()
    };

    await writer.WriteAsync(_application, _ct);
  }

  private async Task ProcessApplicationAsync(Application _application, CancellationToken _ct)
  {
    p_log.Info($"Processing application {_application.Id} (channel: {_application.Channel}, weight: {_application.Weight})");

    // Имитация обработки 1-2 секунды
    var delayMs = p_random.Next(p_config.MinProcessingDelayMs, p_config.MaxProcessingDelayMs);
    await Task.Delay(delayMs, _ct);

    // Отправляем в Kafka и gRPC параллельно
    var kafkaTask = p_kafkaProducer.SendApplicationAsync(_application, _ct);
    var grpcTask = p_grpcClient.SendApplicationAsync(_application, _ct);

    await Task.WhenAll(kafkaTask, grpcTask);

    p_log.Info($"Application {_application.Id} processed successfully (took {delayMs}ms)");
  }
}
