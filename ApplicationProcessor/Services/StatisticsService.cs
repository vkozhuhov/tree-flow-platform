using ApplicationProcessor.Config;
using ApplicationProcessor.Data;
using ApplicationProcessor.Interfaces;
using MCDis256.Design.App.Interface;
using MCDis256.Design.App.Interface.Log;
using MCDis256.Design.App.Interface.Log.Toolkit;

namespace ApplicationProcessor.Services;

/// <summary>
/// Сервис для работы со статистикой обработки
/// </summary>
internal class StatisticsService : IStatisticsService, IAppComponent<IStatisticsService>
{
  private readonly ILog p_log;
  private readonly ProcessorConfig p_config;
  private readonly ProcessingStatistics p_statistics;

  public StatisticsService(ILog _log, ProcessorConfig _config)
  {
    p_log = _log["statistics"];
    p_config = _config;
    p_statistics = new ProcessingStatistics();
  }

  public static IStatisticsService Activate(IAppContext _ctx) =>
    _ctx.Activate((ILog _log, ProcessorConfig _config) => new StatisticsService(_log, _config));

  public ProcessingStatistics GetStatistics() => p_statistics;

  public async Task SaveStatisticsAsync(CancellationToken _ct)
  {
    // TODO: Здесь можно сохранять статистику в БД через функцию processor.f_save_statistics
    p_log.Info($"Statistics: Total={p_statistics.TotalProcessed}, Failed={p_statistics.TotalFailed}, " +
               $"ValidationErrors={p_statistics.TotalValidationErrors}");

    foreach (var (channel, count) in p_statistics.ProcessingByChannel)
    {
      p_log.Info($"Channel {channel}: {count} applications");
    }

    await Task.CompletedTask;
  }

  public void StartPeriodicSave(CancellationToken _ct)
  {
    Task.Run(async () =>
    {
      p_log.Info("Начало периодического сохранения статистики...");

      while (!_ct.IsCancellationRequested)
      {
        try
        {
          await Task.Delay(TimeSpan.FromSeconds(p_config.StatisticsSaveIntervalSeconds), _ct);
          await SaveStatisticsAsync(_ct);
        }
        catch (OperationCanceledException)
        {
          break;
        }
        catch (Exception ex)
        {
          p_log.Error($"Ошибка в сохранении периодической статистики: {ex.Message}");
        }
      }

      p_log.Info("Периодическое сохранение статистики остановлено");
    }, _ct);
  }
}
