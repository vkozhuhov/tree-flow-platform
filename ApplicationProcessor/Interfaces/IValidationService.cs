using ApplicationProcessor.Data;

namespace ApplicationProcessor.Interfaces;

/// <summary>
/// Сервис для валидации заявок
/// </summary>
public interface IValidationService
{
  /// <summary>
  /// Валидировать заявку
  /// </summary>
  ValidationResult ValidateApplication(Application _application);
}
