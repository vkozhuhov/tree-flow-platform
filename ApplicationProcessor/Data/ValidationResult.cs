namespace ApplicationProcessor.Data;

/// <summary>
/// Результат валидации заявки
/// </summary>
public class ValidationResult
{
  public required bool IsValid { get; init; }
  public List<string> Errors { get; init; } = [];

  public static ValidationResult Success() => new ValidationResult { IsValid = true };

  public static ValidationResult Failure(List<string> _errors) => new ValidationResult
  {
    IsValid = false,
    Errors = _errors
  };
}
