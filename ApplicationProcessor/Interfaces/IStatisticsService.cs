using ApplicationProcessor.Data;

namespace ApplicationProcessor.Interfaces;

/// <summary>
/// Сервис для работы со статистикой
/// </summary>
public interface IStatisticsService
{
  /// <summary>
  /// Получить текущую статистику
  /// </summary>
  ProcessingStatistics GetStatistics();

  /// <summary>
  /// Сохранить статистику в БД
  /// </summary>
  Task SaveStatisticsAsync(CancellationToken _ct);

  /// <summary>
  /// Запустить периодическое сохранение статистики
  /// </summary>
  void StartPeriodicSave(CancellationToken _ct);
}
