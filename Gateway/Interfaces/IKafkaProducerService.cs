using Gateway.Data;

namespace Gateway.Interfaces;

/// <summary>
/// Сервис для отправки сообщений в Kafka
/// </summary>
public interface IKafkaProducerService
{
  /// <summary>
  /// Отправить заявку в Kafka (ApplicationProcessor)
  /// </summary>
  Task SendApplicationAsync(Application _application, CancellationToken _ct);
}
