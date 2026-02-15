using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Gateway.Config;
using Gateway.Data;
using Gateway.Interfaces;
using MCDis256.Design.App.Interface;
using MCDis256.Design.App.Interface.Log;
using MCDis256.Design.App.Interface.Log.Toolkit;

namespace Gateway.Services;

/// <summary>
/// Сервис для отправки заявок в Kafka
/// </summary>
internal class KafkaProducerService : IKafkaProducerService, IAppComponent<IKafkaProducerService>, IDisposable
{
  private readonly ILog p_log;
  private readonly GatewayConfig p_config;
  private readonly IProducer<string, string> p_producer;
  private readonly JsonSerializerOptions p_jsonOptions;

  public KafkaProducerService(ILog _log, GatewayConfig _config)
  {
    p_log = _log;
    p_config = _config;

    var config = new ProducerConfig
    {
      BootstrapServers = _config.KafkaBroker,
      Acks = Acks.Leader,
      MessageTimeoutMs = 30000,
      RequestTimeoutMs = 30000
    };

    p_producer = new ProducerBuilder<string, string>(config).Build();

    // Настройка сериализации enum как строк
    p_jsonOptions = new JsonSerializerOptions
    {
      Converters = { new JsonStringEnumConverter() }
    };

    p_log.Info($"Kafka producer инициализирован (брокер: {_config.KafkaBroker})");
  }

  public static IKafkaProducerService Activate(IAppContext _ctx) =>
    _ctx.Activate((ILog _log, GatewayConfig _config) => new KafkaProducerService(_log, _config));

  public async Task SendApplicationAsync(Application _application, CancellationToken _ct)
  {
    try
    {
      var message = new Message<string, string>
      {
        Key = _application.Id.ToString(),
        Value = JsonSerializer.Serialize(_application, p_jsonOptions)
      };

      var result = await p_producer.ProduceAsync(p_config.KafkaApplicationTopic, message, _ct);

      p_log.Info($"Заявка {_application.Id} отправлена в Kafka " +
                 $"(топик: {p_config.KafkaApplicationTopic}, партиция: {result.Partition}, смещение: {result.Offset})");
    }
    catch (Exception ex)
    {
      p_log.Error($"Не удалось отправить заявку {_application.Id} в Kafka: {ex.Message}");
      throw;
    }
  }

  public void Dispose()
  {
    p_producer?.Dispose();
    p_log.Info("Kafka producer освобожден");
  }
}
