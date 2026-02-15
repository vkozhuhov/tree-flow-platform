using MCDis256.Design.App.Conf.Interfaces.Attributes;

namespace ApplicationProcessor.Config;

[AppConfig(Name = "processor-config.json")]
public class ProcessorConfig
{
  // Kafka settings
  public string KafkaBroker { get; set; } = "localhost:9092";
  public string KafkaApplicationTopic { get; set; } = "applications";
  public string KafkaResultTopic { get; set; } = "application-results";
  public string KafkaGroupId { get; set; } = "application-processor-group";

  // PostgreSQL settings
  public string PostgresConnectionString { get; set; } = "Host=localhost;Port=5433;Database=threeflow;Username=postgres;Password=12341234";

  // Processing settings
  public int MaxParallelTasks { get; set; } = 10;
  public int StatisticsSaveIntervalSeconds { get; set; } = 60;
}
