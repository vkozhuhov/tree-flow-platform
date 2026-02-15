using ApplicationProcessor.Config;
using Npgsql;

namespace ApplicationProcessor.Repositories;

public abstract class BaseRepository
{
  private readonly string p_connectionString;

  protected BaseRepository(ProcessorConfig _config)
  {
    p_connectionString = _config.PostgresConnectionString;
    Console.WriteLine($"[DEBUG] PostgreSQL Connection String: {p_connectionString}");
  }

  protected async Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken _ct)
  {
    Console.WriteLine("[DEBUG] Attempting to connect to PostgreSQL...");
    var connection = new NpgsqlConnection(p_connectionString);
    await connection.OpenAsync(_ct);
    Console.WriteLine($"[DEBUG] Connected successfully! Database: {connection.Database}");
    return connection;
  }
}
