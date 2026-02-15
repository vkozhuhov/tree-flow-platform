using Amazon.S3.Model;
using FileStorageService.Interfaces;
using FileStorageService.Protos;
using Grpc.Core;
using MCDis256.Design.App.Interface.Log;
using MCDis256.Design.App.Interface.Log.Toolkit;

namespace FileStorageService.Services;

/// <summary>
/// Реализация gRPC сервиса FileStorage
/// </summary>
public class FileStorageGrpcService : Protos.FileStorage.FileStorageBase
{
  private readonly ILog p_log;
  private readonly ITemporalStorageService p_temporalStorage;
  private readonly IS3Service p_s3Service;

  public FileStorageGrpcService(
    ILog _log,
    ITemporalStorageService _temporalStorage,
    IS3Service _s3Service)
  {
    p_log = _log["grpc-service"];
    p_temporalStorage = _temporalStorage;
    p_s3Service = _s3Service;
  }

  public override async Task<TemporalStorageResponse> WriteToTemporalStorage(
    TemporalStorageRequest _request,
    ServerCallContext _context)
  {
    try
    {
      p_log.Info($"WriteToTemporalStorage called for application {_request.ApplicationId} with {_request.Files.Count} files");

      var fileIds = new List<string>();

      foreach (var file in _request.Files)
      {
        var fileId = await p_temporalStorage.SaveFileAsync(
          _request.ApplicationId,
          file.Filename,
          file.Content.ToByteArray(),
          file.ContentType,
          _context.CancellationToken);

        fileIds.Add(fileId);
      }

      return new TemporalStorageResponse
      {
        Success = true,
        Message = $"Successfully saved {fileIds.Count} files to temporal storage",
        TemporalFileIds = { fileIds }
      };
    }
    catch (Exception ex)
    {
      p_log.Error($"Error in WriteToTemporalStorage: {ex.Message}");
      return new TemporalStorageResponse
      {
        Success = false,
        Message = $"Error: {ex.Message}"
      };
    }
  }

  public override async Task<PermanentStorageResponse> MoveToPermanentStorage(
    PermanentStorageRequest _request,
    ServerCallContext _context)
  {
    try
    {
      p_log.Info($"MoveToPermanentStorage called for application {_request.ApplicationId} with {_request.TemporalFileIds.Count} files");

      var permanentFiles = new List<FileMetadata>();

      foreach (var temporalFileId in _request.TemporalFileIds)
      {
        // Получаем файл из временного хранилища
        var temporalFile = await p_temporalStorage.GetFileAsync(temporalFileId, _context.CancellationToken);

        if (temporalFile == null)
        {
          p_log.Warning($"Temporal file {temporalFileId} not found");
          continue;
        }

        // Загружаем в S3 и получаем PreSignedUrl
        var (s3Key, preSignedUrl) = await p_s3Service.UploadFileAsync(
          temporalFile.Filename,
          temporalFile.Content,
          temporalFile.ContentType,
          _context.CancellationToken);

        // Создаем метаданные файла
        var fileMetadata = new FileMetadata
        {
          FileId = Guid.NewGuid().ToString(),
          Filename = temporalFile.Filename,
          S3Key = s3Key,
          PresignedUrl = preSignedUrl,
          Size = temporalFile.Content.Length,
          ContentType = temporalFile.ContentType
        };

        permanentFiles.Add(fileMetadata);

        // Удаляем из временного хранилища
        await p_temporalStorage.DeleteFileAsync(temporalFileId, _context.CancellationToken);
      }

      return new PermanentStorageResponse
      {
        Success = true,
        Message = $"Successfully moved {permanentFiles.Count} files to permanent storage",
        Files = { permanentFiles }
      };
    }
    catch (Exception ex)
    {
      p_log.Error($"Error in MoveToPermanentStorage: {ex.Message}");
      return new PermanentStorageResponse
      {
        Success = false,
        Message = $"Error: {ex.Message}"
      };
    }
  }

  public override Task<HealthCheckResponse> HealthCheck(
    HealthCheckRequest _request,
    ServerCallContext _context)
  {
    p_log.Info("HealthCheck called");

    return Task.FromResult(new HealthCheckResponse
    {
      Healthy = true,
      Message = "FileStorageService is healthy",
      Version = "1.0.0"
    });
  }

  public override async Task<Protos.GetPreSignedUrlResponse> GetPreSignedUrl(
    Protos.GetPreSignedUrlRequest _request,
    ServerCallContext _context)
  {
    try
    {
      p_log.Info($"GetPreSignedUrl called for S3 key: {_request.S3Key}");

      if (string.IsNullOrWhiteSpace(_request.S3Key))
      {
        return new Protos.GetPreSignedUrlResponse
        {
          Success = false,
          Message = "S3 key is required"
        };
      }

      // Генерируем новую PreSigned URL через S3Service
      var preSignedUrl = await p_s3Service.GeneratePreSignedUrlAsync(_request.S3Key, _context.CancellationToken);

      return new Protos.GetPreSignedUrlResponse
      {
        Success = true,
        PresignedUrl = preSignedUrl,
        Message = "PreSigned URL generated successfully"
      };
    }
    catch (Exception ex)
    {
      p_log.Error($"Error in GetPreSignedUrl: {ex.Message}");
      return new Protos.GetPreSignedUrlResponse
      {
        Success = false,
        Message = $"Error: {ex.Message}"
      };
    }
  }
}
