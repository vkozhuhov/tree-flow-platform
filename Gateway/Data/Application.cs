namespace Gateway.Data;

/// <summary>
/// Внутренняя модель заявки для обработки
/// </summary>
public record Application
{
  /// <summary>
  /// Уникальный ID заявки
  /// </summary>
  public required Guid Id { get; init; }

  /// <summary>
  /// Вес заявки
  /// </summary>
  public required int Weight { get; init; }

  /// <summary>
  /// Канал обработки
  /// </summary>
  public required ChannelType Channel { get; init; }

  /// <summary>
  /// Данные заявки
  /// </summary>
  public required string Data { get; init; }

  /// <summary>
  /// Файлы для отправки
  /// </summary>
  public List<string>? Files { get; init; }

  /// <summary>
  /// Время создания заявки
  /// </summary>
  public required DateTime CreatedAt { get; init; }
}
