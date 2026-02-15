using Gateway.Data;

namespace Gateway.Interfaces;

/// <summary>
/// gRPC клиент для взаимодействия с FileStorageService
/// </summary>
public interface IGrpcFileStorageClient
{
  /// <summary>
  /// Отправить заявку и файлы в FileStorageService
  /// </summary>
  Task SendApplicationAsync(Application _application, CancellationToken _ct);
}
