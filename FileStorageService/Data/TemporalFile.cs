namespace FileStorageService.Data;

/// <summary>
/// Информация о файле во временном хранилище
/// </summary>
public class TemporalFile
{
  public required string Id { get; init; }
  public required string ApplicationId { get; init; }
  public required string Filename { get; init; }
  public required string ContentType { get; init; }
  public required byte[] Content { get; init; }
  public required DateTime CreatedAt { get; init; }
  public required DateTime ExpiresAt { get; init; }
}
