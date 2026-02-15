namespace FileStorageService.Interfaces;

/// <summary>
/// Сервис для работы с S3/MinIO
/// </summary>
public interface IS3Service
{
  /// <summary>
  /// Загрузить файл в S3 и получить PreSignedUrl
  /// </summary>
  Task<(string s3Key, string preSignedUrl)> UploadFileAsync(
    string _filename,
    byte[] _content,
    string _contentType,
    CancellationToken _ct);

  /// <summary>
  /// Удалить файл из S3
  /// </summary>
  Task DeleteFileAsync(string _s3Key, CancellationToken _ct);

  /// <summary>
  /// Проверить существование bucket и создать при необходимости
  /// </summary>
  Task EnsureBucketExistsAsync(CancellationToken _ct);

  /// <summary>
  /// Сгенерировать PreSignedUrl для существующего файла в S3
  /// </summary>
  Task<string> GeneratePreSignedUrlAsync(string _s3Key, CancellationToken _ct);
}
