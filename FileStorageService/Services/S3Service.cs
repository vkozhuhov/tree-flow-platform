using Amazon.S3;
using Amazon.S3.Model;
using FileStorageService.Config;
using FileStorageService.Interfaces;
using MCDis256.Design.App.Interface;
using MCDis256.Design.App.Interface.Log;
using MCDis256.Design.App.Interface.Log.Toolkit;

namespace FileStorageService.Services;

/// <summary>
/// Сервис для работы с S3/MinIO
/// </summary>
internal class S3Service : IS3Service, IAppComponent<IS3Service>
{
  private readonly ILog p_log;
  private readonly FileStorageConfig p_config;
  private readonly IAmazonS3 p_s3Client;

  public S3Service(ILog _log, FileStorageConfig _config)
  {
    p_log = _log["s3-service"];
    p_config = _config;

    var s3Config = new AmazonS3Config
    {
      ServiceURL = _config.S3Endpoint,
      ForcePathStyle = true,
      UseHttp = !_config.S3UseSSL
    };

    p_s3Client = new AmazonS3Client(_config.S3AccessKey, _config.S3SecretKey, s3Config);
    p_log.Info($"S3 service initialized (endpoint: {_config.S3Endpoint}, bucket: {_config.S3BucketName})");
  }

  public static IS3Service Activate(IAppContext _ctx) =>
    _ctx.Activate((ILog _log, FileStorageConfig _config) => new S3Service(_log, _config));

  public async Task EnsureBucketExistsAsync(CancellationToken _ct)
  {
    try
    {
      var bucketsResponse = await p_s3Client.ListBucketsAsync(_ct);
      var bucketExists = bucketsResponse.Buckets.Any(_b => _b.BucketName == p_config.S3BucketName);

      if (!bucketExists)
      {
        p_log.Info($"Bucket {p_config.S3BucketName} does not exist, creating...");
        await p_s3Client.PutBucketAsync(p_config.S3BucketName, _ct);
        p_log.Info($"Bucket {p_config.S3BucketName} created successfully");
      }
    }
    catch (Exception ex)
    {
      p_log.Error($"Error ensuring bucket exists: {ex.Message}");
      throw;
    }
  }

  public async Task<(string s3Key, string preSignedUrl)> UploadFileAsync(
    string _filename,
    byte[] _content,
    string _contentType,
    CancellationToken _ct)
  {
    try
    {
      // Генерируем уникальный ключ для файла
      var s3Key = $"{Guid.NewGuid()}/{_filename}";

      // Загружаем файл в S3
      using var stream = new MemoryStream(_content);
      var putRequest = new PutObjectRequest
      {
        BucketName = p_config.S3BucketName,
        Key = s3Key,
        InputStream = stream,
        ContentType = _contentType
      };

      await p_s3Client.PutObjectAsync(putRequest, _ct);

      // Генерируем PreSigned URL
      var preSignedRequest = new GetPreSignedUrlRequest
      {
        BucketName = p_config.S3BucketName,
        Key = s3Key,
        Expires = DateTime.UtcNow.AddHours(p_config.PreSignedUrlExpirationHours)
      };

      var preSignedUrl = await p_s3Client.GetPreSignedURLAsync(preSignedRequest);

      p_log.Info($"File uploaded to S3 (key: {s3Key}, size: {_content.Length} bytes)");

      return (s3Key, preSignedUrl);
    }
    catch (Exception ex)
    {
      p_log.Error($"Error uploading file to S3: {ex.Message}");
      throw;
    }
  }

  public async Task DeleteFileAsync(string _s3Key, CancellationToken _ct)
  {
    try
    {
      await p_s3Client.DeleteObjectAsync(p_config.S3BucketName, _s3Key, _ct);
      p_log.Info($"File deleted from S3 (key: {_s3Key})");
    }
    catch (Exception ex)
    {
      p_log.Error($"Error deleting file from S3: {ex.Message}");
      throw;
    }
  }

  public Task<string> GeneratePreSignedUrlAsync(string _s3Key, CancellationToken _ct)
  {
    try
    {
      var preSignedRequest = new GetPreSignedUrlRequest
      {
        BucketName = p_config.S3BucketName,
        Key = _s3Key,
        Expires = DateTime.UtcNow.AddHours(p_config.PreSignedUrlExpirationHours)
      };

      var preSignedUrl = p_s3Client.GetPreSignedURL(preSignedRequest);
      p_log.Info($"Generated PreSigned URL for key: {_s3Key}");

      return Task.FromResult(preSignedUrl);
    }
    catch (Exception ex)
    {
      p_log.Error($"Error generating PreSigned URL: {ex.Message}");
      throw;
    }
  }
}
