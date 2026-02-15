using FileStorageService;
using FileStorageService.Config;
using FileStorageService.Interfaces;
using FileStorageService.Services;
using MCDis256.Design.App;
using MCDis256.Design.App.Conf.Toolkit;
using MCDis256.Design.App.Modules.Log;
using MCDis256.Design.App.Toolkit;
using MCDis256.Design.IO;

public class Program
{
  public static Task Main()
  {
    DirectoryPath? appConfRoot = null;
#if !DEBUG
    appConfRoot = DirectoryPath.FromString("/vault/secrets/");
#elif DEBUG
    appConfRoot = DirectoryPath.FromString("./conf/");
#endif

    return AppHome.New()
      .UseConsoleLogListener()
      .UseLogAppInfo()
      .UseAppConfigDirectory(appConfRoot)
      .UseConfFile<FileStorageConfig>()
      // Регистрация сервисов
      .UseExport<IS3Service, S3Service>()
      .UseExport<ITemporalStorageService, TemporalStorageService>()
      .UseExport<IGrpcServer, GrpcServer>()
      .ActivateOnStart<IGrpcServer>()
      .RunAsync();
  }
}
