using ApplicationProcessor.Config;
using ApplicationProcessor.Data;
using ApplicationProcessor.Interfaces;
using MCDis256.Design.App.Interface;
using Npgsql;

namespace ApplicationProcessor.Repositories;

/// <summary>
/// Репозиторий для работы с заявками через PostgreSQL функции
/// </summary>
internal class ApplicationRepository : BaseRepository, IApplicationRepository, IAppComponent<IApplicationRepository>
{
  public ApplicationRepository(ProcessorConfig _config) : base(_config)
  {
  }

  public static IApplicationRepository Activate(IAppContext _ctx) =>
    _ctx.Activate((ProcessorConfig _config) => new ApplicationRepository(_config));

  public async Task<bool> SaveApplicationAsync(Application _application, CancellationToken _ct)
  {
    var sql = @"
      SELECT processor.f_insert_application(
        @p_id,
        @p_weight,
        @p_data,
        @p_files,
        @p_channel,
        @p_created_at
      );";

    await using var connection = await CreateConnectionAsync(_ct);
    await using var command = new NpgsqlCommand(sql, connection);

    command.Parameters.AddWithValue("p_id", _application.Id);
    command.Parameters.AddWithValue("p_weight", _application.Weight);
    command.Parameters.AddWithValue("p_data", _application.Data);

    // Явно указываем тип для массива
    var filesParam = command.Parameters.Add("p_files", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text);
    filesParam.Value = _application.Files?.ToArray() ?? (object)DBNull.Value;

    command.Parameters.AddWithValue("p_channel", _application.Channel);

    // Используем DateTime без timezone
    command.Parameters.AddWithValue("p_created_at", DateTime.SpecifyKind(_application.CreatedAt, DateTimeKind.Unspecified));

    try
    {
      var result = await command.ExecuteScalarAsync(_ct);
      return result is bool success && success;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[ERROR] Failed to save application {_application.Id}: {ex.Message}");
      Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
      return false;
    }
  }

  public async Task<bool> UpdateApplicationStatusAsync(
    Guid _applicationId,
    string _status,
    string? _errorMessage,
    CancellationToken _ct)
  {
    var sql = @"
      SELECT processor.f_update_application_status(
        @p_id,
        @p_status,
        @p_error_message,
        @p_processed_at
      );";

    await using var connection = await CreateConnectionAsync(_ct);
    await using var command = new NpgsqlCommand(sql, connection);

    command.Parameters.AddWithValue("p_id", _applicationId);
    command.Parameters.AddWithValue("p_status", _status);
    command.Parameters.AddWithValue("p_error_message", _errorMessage ?? (object)DBNull.Value);
    command.Parameters.AddWithValue("p_processed_at", DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified));

    try
    {
      Console.WriteLine($"[DEBUG] Updating status for {_applicationId} to '{_status}'");
      var result = await command.ExecuteScalarAsync(_ct);
      var success = result is bool s && s;
      Console.WriteLine($"[DEBUG] Status update result: {success} (raw result: {result})");
      return success;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[ERROR] Failed to update status for {_applicationId}: {ex.Message}");
      return false;
    }
  }

  public async Task<Application?> GetApplicationByIdAsync(Guid _applicationId, CancellationToken _ct)
  {
    var sql = @"
      SELECT *
      FROM processor.f_get_application_by_id(@p_id);";

    await using var connection = await CreateConnectionAsync(_ct);
    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("p_id", _applicationId);

    try
    {
      await using var reader = await command.ExecuteReaderAsync(_ct);

      if (!reader.HasRows) return null;

      await reader.ReadAsync(_ct);
      return MapToApplication(reader);
    }
    catch (Exception)
    {
      return null;
    }
  }

  private Application MapToApplication(NpgsqlDataReader _reader)
  {
    return new Application
    {
      Id = _reader.GetGuid(_reader.GetOrdinal("id")),
      Weight = _reader.GetInt32(_reader.GetOrdinal("weight")),
      Data = _reader.GetString(_reader.GetOrdinal("data")),
      Files = _reader.IsDBNull(_reader.GetOrdinal("files"))
        ? null
        : _reader.GetFieldValue<string[]>(_reader.GetOrdinal("files")).ToList(),
      Channel = _reader.GetString(_reader.GetOrdinal("channel")),
      CreatedAt = _reader.GetDateTime(_reader.GetOrdinal("created_at")),
      ProcessedAt = _reader.IsDBNull(_reader.GetOrdinal("processed_at"))
        ? null
        : _reader.GetDateTime(_reader.GetOrdinal("processed_at")),
      Status = _reader.IsDBNull(_reader.GetOrdinal("status"))
        ? null
        : _reader.GetString(_reader.GetOrdinal("status")),
      ErrorMessage = _reader.IsDBNull(_reader.GetOrdinal("error_message"))
        ? null
        : _reader.GetString(_reader.GetOrdinal("error_message"))
    };
  }
}
