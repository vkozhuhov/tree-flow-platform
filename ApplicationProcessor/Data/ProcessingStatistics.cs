using System.Collections.Concurrent;

namespace ApplicationProcessor.Data;

/// <summary>
/// Статистика обработки заявок (in-memory)
/// </summary>
public class ProcessingStatistics
{
  private long p_totalProcessed;
  private long p_totalFailed;
  private long p_totalValidationErrors;
  private readonly ConcurrentDictionary<string, long> p_processingByChannel = new();

  public long TotalProcessed => Interlocked.Read(ref p_totalProcessed);
  public long TotalFailed => Interlocked.Read(ref p_totalFailed);
  public long TotalValidationErrors => Interlocked.Read(ref p_totalValidationErrors);

  public IReadOnlyDictionary<string, long> ProcessingByChannel => p_processingByChannel;

  public void IncrementProcessed()
  {
    Interlocked.Increment(ref p_totalProcessed);
  }

  public void IncrementFailed()
  {
    Interlocked.Increment(ref p_totalFailed);
  }

  public void IncrementValidationErrors()
  {
    Interlocked.Increment(ref p_totalValidationErrors);
  }

  public void IncrementChannel(string _channel)
  {
    p_processingByChannel.AddOrUpdate(_channel, 1, (_key, _oldValue) => _oldValue + 1);
  }
}
