using Gateway.Config;
using Gateway.Interfaces;
using Gateway.Services;
using MCDis256.Design.App;
using MCDis256.Design.App.Conf.Toolkit;
using MCDis256.Design.App.Modules.Log;
using MCDis256.Design.App.Toolkit;
using MCDis256.Design.App.Web.Parts.Interfaces;
using MCDis256.Design.IO;

namespace Gateway;

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
      .UseConfFile<GatewayConfig>()
      // Регистрация сервисов
      .UseExport<IKafkaProducerService, KafkaProducerService>()
      .UseExport<IGrpcFileStorageClient, GrpcFileStorageClient>()
      .UseExport<IApplicationChannelService, ApplicationChannelService>()
      .UseExport<IWebServer, WebServer>()
      .ActivateOnStart<IWebServer>()
      .ActivateOnStart<IApplicationChannelService>(_service =>
      {
        // Запускаем обработку каналов при старте
        _ = Task.Run(() => _service.StartProcessingAsync(CancellationToken.None));
      })
      .RunAsync();
  }
}
