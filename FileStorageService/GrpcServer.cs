using System.Net;
using System.Reactive.Linq;
using FileStorageService.Config;
using FileStorageService.Interfaces;
using FileStorageService.Services;
using MCDis256.Design.App.Conf.Interfaces;
using MCDis256.Design.App.Interface;
using MCDis256.Design.App.Interface.Log;
using MCDis256.Design.App.Interface.Log.Toolkit;
using MCDis256.Design.Rx;
using MCDis256.Design.Rx.Interfaces;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace FileStorageService;

public interface IGrpcServer;

public class GrpcServer : IGrpcServer, IAppComponent<IGrpcServer>
{
  private readonly ILog p_log;
  private readonly ILifetime p_lifetime;
  private readonly IAppConfigProvider p_cfg;
  private readonly ITemporalStorageService p_temporalStorage;
  private readonly IS3Service p_s3Service;

  public static IGrpcServer Activate(IAppContext _context) =>
    _context.Activate((ILog _log, ILifetime _lifetime, IAppConfigProvider _cfg,
        ITemporalStorageService _temporalStorage, IS3Service _s3Service) =>
      new GrpcServer(_log, _lifetime, _cfg, _temporalStorage, _s3Service));

  private GrpcServer(
    ILog _log,
    ILifetime _lifetime,
    IAppConfigProvider _cfg,
    ITemporalStorageService _temporalStorage,
    IS3Service _s3Service)
  {
    p_log = _log["grpc-server"];
    p_lifetime = _lifetime;
    p_cfg = _cfg;
    p_temporalStorage = _temporalStorage;
    p_s3Service = _s3Service;

    var config = p_cfg.ResolveValidValue<FileStorageConfig>();

    config
      .Throttle(TimeSpan.FromSeconds(1), _lifetime.Scheduler)
      .AssignIndex()
      .HotAlive(_lifetime, (_indexedCfg, _life) =>
      {
        var index = _indexedCfg.Index;
        var cfg = _indexedCfg.Value;

        var serverTask = Task.Run(async () =>
        {
          try
          {
            p_log.Info($"[{index}] FileStorage gRPC server is starting...");

            // Убедимся, что bucket существует
            await p_s3Service.EnsureBucketExistsAsync(_lifetime.Cancellation);

            using var host = CreateGrpcHost(cfg);
            p_log.Info($"[{index}] FileStorage gRPC server created on port {cfg.Port}");

            await host.RunAsync(_lifetime.Cancellation).ConfigureAwait(false);
            p_log.Info($"[{index}] FileStorage gRPC server is stopped");
          }
          catch (Exception e)
          {
            p_log.Error($"[{index}] Error in FileStorage gRPC Server thread: {e}");
          }
        });

        // Запускаем фоновую задачу очистки устаревших файлов
        StartCleanupTask(_life);
      });
  }

  private IHost CreateGrpcHost(FileStorageConfig _config)
  {
    var builder = WebApplication.CreateSlimBuilder();

    // DI
    builder.Services.AddSingleton<ITemporalStorageService>(_ => p_temporalStorage);
    builder.Services.AddSingleton<IS3Service>(_ => p_s3Service);
    builder.Services.AddSingleton<ILog>(_ => p_log);

    // gRPC
    builder.Services.AddGrpc();

    builder.WebHost.ConfigureKestrel(_options =>
    {
      _options.Listen(IPAddress.Any, _config.Port, _listenOptions =>
      {
        _listenOptions.Protocols = HttpProtocols.Http2;
      });
    });

    var app = builder.Build();

    // Регистрация gRPC сервиса
    app.MapGrpcService<FileStorageGrpcService>();

    // Health check endpoint
    app.MapGet("/", () => "FileStorage gRPC service is running. Use gRPC client to communicate.");

    return app;
  }

  private void StartCleanupTask(ILifetime _lifetime)
  {
    Task.Run(async () =>
    {
      p_log.Info("Starting cleanup task for expired temporal files...");

      while (!_lifetime.Cancellation.IsCancellationRequested)
      {
        try
        {
          await Task.Delay(TimeSpan.FromMinutes(5), _lifetime.Cancellation);
          await p_temporalStorage.CleanupExpiredFilesAsync(_lifetime.Cancellation);
        }
        catch (OperationCanceledException)
        {
          break;
        }
        catch (Exception ex)
        {
          p_log.Error($"Error in cleanup task: {ex.Message}");
        }
      }

      p_log.Info("Cleanup task stopped");
    });
  }
}
