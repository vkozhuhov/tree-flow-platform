using MCDis256.Design.App.Conf.Interfaces.Attributes;

namespace Gateway.Config;

/// <summary>
/// Конфигурация Gateway сервиса
/// </summary>
[AppConfig(Name = "gateway-config.json")]
public class GatewayConfig
{
  /// <summary>
  /// Адрес Kafka брокера
  /// </summary>
  public string KafkaBroker { get; set; } = "localhost:9092";

  /// <summary>
  /// Topic Kafka для отправки заявок в ApplicationProcessor
  /// </summary>
  public string KafkaApplicationTopic { get; set; } = "applications";

  /// <summary>
  /// URL gRPC сервиса FileStorageService
  /// </summary>
  public string GrpcFileStorageUrl { get; set; } = "http://localhost:5001";

  /// <summary>
  /// Порт для Gateway API
  /// </summary>
  public int Port { get; set; } = 5050;

  /// <summary>
  /// Таймаут обработки заявки (секунды)
  /// </summary>
  public int ProcessingTimeoutSeconds { get; set; } = 30;

  /// <summary>
  /// Минимальное время имитации обработки (мс)
  /// </summary>
  public int MinProcessingDelayMs { get; set; } = 1000;

  /// <summary>
  /// Максимальное время имитации обработки (мс)
  /// </summary>
  public int MaxProcessingDelayMs { get; set; } = 2000;
}
