using Gateway.Data;

namespace Gateway.Interfaces;

/// <summary>
/// Сервис управления каналами обработки заявок
/// </summary>
public interface IApplicationChannelService
{
  /// <summary>
  /// Отправить заявку в соответствующий канал на основе веса
  /// </summary>
  Task<ApplicationResponse> SubmitApplicationAsync(ApplicationRequest _request, CancellationToken _ct);

  /// <summary>
  /// Запустить обработку всех каналов
  /// </summary>
  Task StartProcessingAsync(CancellationToken _ct);
}
