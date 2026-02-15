using FileStorageService.Protos;
using Gateway.Config;
using Gateway.Data;
using Gateway.Interfaces;
using Google.Protobuf;
using Grpc.Net.Client;
using MCDis256.Design.App.Interface;
using MCDis256.Design.App.Interface.Log;
using MCDis256.Design.App.Interface.Log.Toolkit;

namespace Gateway.Services;

/// <summary>
/// gRPC клиент для взаимодействия с FileStorageService
/// </summary>
internal class GrpcFileStorageClient : IGrpcFileStorageClient, IAppComponent<IGrpcFileStorageClient>
{
  private readonly ILog p_log;
  private readonly GatewayConfig p_config;
  private readonly GrpcChannel p_channel;
  private readonly FileStorage.FileStorageClient p_client;

  public GrpcFileStorageClient(ILog _log, GatewayConfig _config)
  {
    p_log = _log["grpc-file-storage"];
    p_config = _config;

    // Создаем gRPC канал
    p_channel = GrpcChannel.ForAddress(_config.GrpcFileStorageUrl);
    p_client = new FileStorage.FileStorageClient(p_channel);

    p_log.Info($"gRPC FileStorage клиент инициализирован (url: {_config.GrpcFileStorageUrl})");
  }

  public static IGrpcFileStorageClient Activate(IAppContext _ctx) =>
    _ctx.Activate((ILog _log, GatewayConfig _config) => new GrpcFileStorageClient(_log, _config));

  public async Task SendApplicationAsync(Application _application, CancellationToken _ct)
  {
    try
    {
      p_log.Info($"Отправка заявки {_application.Id} в FileStorageService через gRPC");

      if (_application.Files != null && _application.Files.Count > 0)
      {
        // 1. Отправляем файлы во временное хранилище
        var temporalRequest = new TemporalStorageRequest
        {
          ApplicationId = _application.Id.ToString()
        };

        foreach (var fileName in _application.Files)
        {
          temporalRequest.Files.Add(new FileData
          {
            Filename = fileName,
            Content = ByteString.CopyFromUtf8($"File content for {fileName}"),
            ContentType = "application/octet-stream"
          });
        }

        var temporalResponse = await p_client.WriteToTemporalStorageAsync(temporalRequest, cancellationToken: _ct);

        if (!temporalResponse.Success)
        {
          p_log.Error($"Не удалось записать файлы во временное хранилище: {temporalResponse.Message}");
          return;
        }

        p_log.Info($"Файлы записаны во временное хранилище: {string.Join(", ", temporalResponse.TemporalFileIds)}");

        // 2. Переносим файлы в постоянное хранилище
        var permanentRequest = new PermanentStorageRequest
        {
          ApplicationId = _application.Id.ToString()
        };
        permanentRequest.TemporalFileIds.AddRange(temporalResponse.TemporalFileIds);

        var permanentResponse = await p_client.MoveToPermanentStorageAsync(permanentRequest, cancellationToken: _ct);

        if (!permanentResponse.Success)
        {
          p_log.Error($"Не удалось переместить файлы в постоянное хранилище: {permanentResponse.Message}");
          return;
        }

        p_log.Info($"Файлы перемещены в постоянное хранилище. PreSigned URLs:");
        foreach (var file in permanentResponse.Files)
        {
          p_log.Info($"  {file.Filename}: {file.PresignedUrl}");
        }
      }
      else
      {
        p_log.Info($"Заявка {_application.Id} не содержит файлов для отправки");
      }
    }
    catch (Exception ex)
    {
      p_log.Error($"Ошибка отправки приложения в FileStorageService: {ex.Message}");
      throw;
    }
  }
}
