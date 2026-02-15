namespace Gateway.Data;

/// <summary>
/// Ответ на создание заявки
/// </summary>
public record ApplicationResponse
{
  /// <summary>
  /// Уникальный ID заявки
  /// </summary>
  public required Guid Id { get; init; }

  /// <summary>
  /// Канал, в который попала заявка
  /// </summary>
  public required ChannelType Channel { get; init; }

  /// <summary>
  /// Вес заявки
  /// </summary>
  public required int Weight { get; init; }

  /// <summary>
  /// Время создания заявки
  /// </summary>
  public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// Тип канала для обработки заявки
/// </summary>
public enum ChannelType
{
  /// <summary>
  /// Вторичный канал (вес < 40)
  /// </summary>
  Secondary = 0,

  /// <summary>
  /// Основной канал (вес 40-80)
  /// </summary>
  Main = 1,

  /// <summary>
  /// Приоритетный канал (вес > 80)
  /// </summary>
  Priority = 2
}
