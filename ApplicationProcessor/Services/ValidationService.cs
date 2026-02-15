using ApplicationProcessor.Data;
using ApplicationProcessor.Interfaces;
using MCDis256.Design.App.Interface;
using MCDis256.Design.App.Interface.Log;
using MCDis256.Design.App.Interface.Log.Toolkit;

namespace ApplicationProcessor.Services;

/// <summary>
/// Сервис валидации заявок
/// </summary>
internal class ValidationService : IValidationService, IAppComponent<IValidationService>
{
  private readonly ILog p_log;

  public ValidationService(ILog _log)
  {
    p_log = _log["validation"];
  }

  public static IValidationService Activate(IAppContext _ctx) =>
    _ctx.Activate((ILog _log) => new ValidationService(_log));

  public ValidationResult ValidateApplication(Application _application)
  {
    var errors = new List<string>();

    // Проверка веса
    if (_application.Weight < 0 || _application.Weight > 100)
    {
      errors.Add("Вес должен быть от 0 до 100");
    }

    // Проверка данных
    if (string.IsNullOrWhiteSpace(_application.Data))
    {
      errors.Add("Данные не могут быть пустыми");
    }

    // Проверка канала
    if (string.IsNullOrWhiteSpace(_application.Channel))
    {
      errors.Add("Канал не может быть пустым");
    }

    if (errors.Count > 0)
    {
      p_log.Warning($"Не удалось проверить заявку {_application.Id}: {string.Join(", ", errors)}");
      return ValidationResult.Failure(errors);
    }

    return ValidationResult.Success();
  }
}
