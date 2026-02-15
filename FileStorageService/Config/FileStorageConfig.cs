using MCDis256.Design.App.Conf.Interfaces.Attributes;

namespace FileStorageService.Config;

[AppConfig(Name = "file-storage-config.json")]
public class FileStorageConfig
{
  // gRPC Server settings
  public int Port { get; set; } = 5001;

  // S3/MinIO settings
  public string S3Endpoint { get; set; } = "http://localhost:9000";
  public string S3AccessKey { get; set; } = "minioadmin";
  public string S3SecretKey { get; set; } = "minioadmin";
  public string S3BucketName { get; set; } = "files";
  public bool S3UseSSL { get; set; } = false;

  // Temporal storage settings
  public string TemporalStoragePath { get; set; } = "./temp-storage";
  public int TemporalStorageExpirationMinutes { get; set; } = 60;

  // PreSigned URL settings
  public int PreSignedUrlExpirationHours { get; set; } = 24;
}
