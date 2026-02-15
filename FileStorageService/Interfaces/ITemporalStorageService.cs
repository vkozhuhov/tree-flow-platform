using FileStorageService.Data;

namespace FileStorageService.Interfaces;

/// <summary>
/// Сервис для работы с временным хранилищем файлов
/// </summary>
public interface ITemporalStorageService
{
  /// <summary>
  /// Сохранить файл во временное хранилище
  /// </summary>
  Task<string> SaveFileAsync(
    string _applicationId,
    string _filename,
    byte[] _content,
    string _contentType,
    CancellationToken _ct);

  /// <summary>
  /// Получить файл из временного хранилища
  /// </summary>
  Task<TemporalFile?> GetFileAsync(string _fileId, CancellationToken _ct);

  /// <summary>
  /// Удалить файл из временного хранилища
  /// </summary>
  Task DeleteFileAsync(string _fileId, CancellationToken _ct);

  /// <summary>
  /// Очистить устаревшие файлы
  /// </summary>
  Task CleanupExpiredFilesAsync(CancellationToken _ct);
}
