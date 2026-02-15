namespace Gateway.Data;

/// <summary>
/// Запрос на создание заявки
/// </summary>
public record ApplicationRequest
{
  /// <summary>
  /// Вес заявки для распределения по каналам
  /// </summary>
  public required int Weight { get; init; }

  /// <summary>
  /// Данные заявки (произвольный JSON)
  /// </summary>
  public required string Data { get; init; }

  /// <summary>
  /// Файлы для отправки в FileStorageService
  /// </summary>
  public List<string>? Files { get; init; }
}
