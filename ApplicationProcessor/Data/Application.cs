namespace ApplicationProcessor.Data;

/// <summary>
/// Модель заявки
/// </summary>
public class Application
{
  public required Guid Id { get; init; }
  public required int Weight { get; init; }
  public required string Data { get; init; }
  public List<string>? Files { get; init; }
  public required string Channel { get; init; }
  public required DateTime CreatedAt { get; init; }
  public DateTime? ProcessedAt { get; init; }
  public string? Status { get; init; }
  public string? ErrorMessage { get; init; }
}
