namespace FileStorageService.Data;

/// <summary>
/// Информация о файле в постоянном хранилище (S3)
/// </summary>
public class PermanentFile
{
  public required string Id { get; init; }
  public required string ApplicationId { get; init; }
  public required string Filename { get; init; }
  public required string ContentType { get; init; }
  public required string S3Key { get; init; }
  public required string PreSignedUrl { get; init; }
  public required long Size { get; init; }
  public required DateTime UploadedAt { get; init; }
}
