using ApplicationProcessor.Data;

namespace ApplicationProcessor.Interfaces;

/// <summary>
/// Репозиторий для работы с заявками в PostgreSQL
/// </summary>
public interface IApplicationRepository
{
  /// <summary>
  /// Сохранить заявку в БД (через функцию f_insert_application)
  /// </summary>
  Task<bool> SaveApplicationAsync(Application _application, CancellationToken _ct);

  /// <summary>
  /// Обновить статус заявки (через функцию f_update_application_status)
  /// </summary>
  Task<bool> UpdateApplicationStatusAsync(Guid _applicationId, string _status, string? _errorMessage, CancellationToken _ct);

  /// <summary>
  /// Получить заявку по ID (через функцию f_get_application_by_id)
  /// </summary>
  Task<Application?> GetApplicationByIdAsync(Guid _applicationId, CancellationToken _ct);
}
