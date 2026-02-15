using ApplicationProcessor.Config;
using ApplicationProcessor.Interfaces;
using ApplicationProcessor.Repositories;
using ApplicationProcessor.Services;
using MCDis256.Design.App;
using MCDis256.Design.App.Conf.Toolkit;
using MCDis256.Design.App.Modules.Log;
using MCDis256.Design.App.Toolkit;
using MCDis256.Design.IO;

public class Program
{
  public static Task Main()
  {
    // В DEBUG режиме конфиг копируется в bin/Debug/net10.0/conf при сборке
    DirectoryPath? appConfRoot = null;
#if !DEBUG
    appConfRoot = DirectoryPath.FromString("/vault/secrets/");
#endif

    return AppHome.New()
      .UseConsoleLogListener()
      .UseLogAppInfo()
      .UseAppConfigDirectory(appConfRoot)
      .UseConfFile<ProcessorConfig>()
      // Регистрация сервисов
      .UseExport<IApplicationRepository, ApplicationRepository>()
      .UseExport<IValidationService, ValidationService>()
      .UseExport<IStatisticsService, StatisticsService>()
      .UseExport<KafkaConsumerService, KafkaConsumerService>()
      .ActivateOnStart<KafkaConsumerService>(_service =>
      {
        _ = Task.Run(() => _service.StartAsync(CancellationToken.None));
      })
      .RunAsync();
  }
}
