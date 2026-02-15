using System.Collections.Concurrent;
using FileStorageService.Config;
using FileStorageService.Data;
using FileStorageService.Interfaces;
using MCDis256.Design.App.Interface;
using MCDis256.Design.App.Interface.Log;
using MCDis256.Design.App.Interface.Log.Toolkit;

namespace FileStorageService.Services;

/// <summary>
/// Сервис для работы с временным хранилищем файлов (in-memory)
/// </summary>
internal class TemporalStorageService : ITemporalStorageService, IAppComponent<ITemporalStorageService>
{
  private readonly ILog p_log;
  private readonly FileStorageConfig p_config;
  private readonly ConcurrentDictionary<string, TemporalFile> p_storage;

  public TemporalStorageService(ILog _log, FileStorageConfig _config)
  {
    p_log = _log["temporal-storage"];
    p_config = _config;
    p_storage = new ConcurrentDictionary<string, TemporalFile>();
    p_log.Info("Temporal storage service initialized");
  }

  public static ITemporalStorageService Activate(IAppContext _ctx) =>
    _ctx.Activate((ILog _log, FileStorageConfig _config) => new TemporalStorageService(_log, _config));

  public Task<string> SaveFileAsync(
    string _applicationId,
    string _filename,
    byte[] _content,
    string _contentType,
    CancellationToken _ct)
  {
    var fileId = Guid.NewGuid().ToString();
    var now = DateTime.UtcNow;

    var temporalFile = new TemporalFile
    {
      Id = fileId,
      ApplicationId = _applicationId,
      Filename = _filename,
      ContentType = _contentType,
      Content = _content,
      CreatedAt = now,
      ExpiresAt = now.AddMinutes(p_config.TemporalStorageExpirationMinutes)
    };

    if (!p_storage.TryAdd(fileId, temporalFile))
    {
      p_log.Error($"Failed to add file {fileId} to temporal storage");
      throw new InvalidOperationException($"Failed to save file {fileId}");
    }

    p_log.Info($"File saved to temporal storage (id: {fileId}, size: {_content.Length} bytes, expires: {temporalFile.ExpiresAt})");
    return Task.FromResult(fileId);
  }

  public Task<TemporalFile?> GetFileAsync(string _fileId, CancellationToken _ct)
  {
    if (p_storage.TryGetValue(_fileId, out var file))
    {
      if (file.ExpiresAt < DateTime.UtcNow)
      {
        p_log.Warning($"File {_fileId} has expired, removing from storage");
        p_storage.TryRemove(_fileId, out _);
        return Task.FromResult<TemporalFile?>(null);
      }

      return Task.FromResult<TemporalFile?>(file);
    }

    return Task.FromResult<TemporalFile?>(null);
  }

  public Task DeleteFileAsync(string _fileId, CancellationToken _ct)
  {
    if (p_storage.TryRemove(_fileId, out _))
    {
      p_log.Info($"File {_fileId} removed from temporal storage");
    }

    return Task.CompletedTask;
  }

  public Task CleanupExpiredFilesAsync(CancellationToken _ct)
  {
    var now = DateTime.UtcNow;
    var expiredFiles = p_storage.Where(_kvp => _kvp.Value.ExpiresAt < now).Select(_kvp => _kvp.Key).ToList();

    foreach (var fileId in expiredFiles)
    {
      if (p_storage.TryRemove(fileId, out _))
      {
        p_log.Info($"Expired file {fileId} removed from temporal storage");
      }
    }

    if (expiredFiles.Count > 0)
    {
      p_log.Info($"Cleaned up {expiredFiles.Count} expired files");
    }

    return Task.CompletedTask;
  }
}
