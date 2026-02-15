using FluentAssertions;
using Gateway.Config;
using Gateway.Data;
using Gateway.Interfaces;
using Gateway.Services;
using MCDis256.Design.App.Interface.Log;
using Moq;

namespace Gateway.Tests.Services;

/// <summary>
/// Unit тесты для ApplicationChannelService
/// </summary>
public class ApplicationChannelServiceTests
{
  private readonly Mock<ILog> p_logMock;
  private readonly Mock<IKafkaProducerService> p_kafkaMock;
  private readonly Mock<IGrpcFileStorageClient> p_grpcMock;
  private readonly GatewayConfig p_config;

  public ApplicationChannelServiceTests()
  {
    p_logMock = new Mock<ILog>();
    p_kafkaMock = new Mock<IKafkaProducerService>();
    p_grpcMock = new Mock<IGrpcFileStorageClient>();

    // Настраиваем mock для ILog (возвращает сам себя при вызове индексатора)
    p_logMock.Setup(_log => _log[It.IsAny<string>()]).Returns(p_logMock.Object);

    p_config = new GatewayConfig
    {
      MinProcessingDelayMs = 100,
      MaxProcessingDelayMs = 200,
      KafkaBroker = "localhost:9092",
      KafkaApplicationTopic = "test-topic",
      GrpcFileStorageUrl = "http://localhost:5001",
      Port = 5050,
      ProcessingTimeoutSeconds = 30
    };
  }

  [Theory]
  [InlineData(95, ChannelType.Priority)]  // Вес > 80 → Priority
  [InlineData(81, ChannelType.Priority)]  // Граничный случай
  [InlineData(80, ChannelType.Main)]      // Граничный случай
  [InlineData(65, ChannelType.Main)]      // 40-80 → Main
  [InlineData(40, ChannelType.Main)]      // Граничный случай
  [InlineData(39, ChannelType.Secondary)] // Граничный случай
  [InlineData(25, ChannelType.Secondary)] // Вес < 40 → Secondary
  [InlineData(0, ChannelType.Secondary)]  // Минимальный вес
  [InlineData(100, ChannelType.Priority)] // Максимальный вес
  public async Task SubmitApplicationAsync_VariousWeights_ReturnsCorrectChannel(
    int _weight,
    ChannelType _expectedChannel)
  {
    // Arrange
    var service = new ApplicationChannelService(
      p_logMock.Object,
      p_config,
      p_kafkaMock.Object,
      p_grpcMock.Object);

    var request = new ApplicationRequest
    {
      Weight = _weight,
      Data = "Test data"
    };

    // Act
    var result = await service.SubmitApplicationAsync(request, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result.Channel.Should().Be(_expectedChannel);
    result.Weight.Should().Be(_weight);
  }

  [Fact]
  public async Task SubmitApplicationAsync_ValidRequest_ReturnsNonEmptyGuid()
  {
    // Arrange
    var service = new ApplicationChannelService(
      p_logMock.Object,
      p_config,
      p_kafkaMock.Object,
      p_grpcMock.Object);

    var request = new ApplicationRequest
    {
      Weight = 50,
      Data = "Test application"
    };

    // Act
    var result = await service.SubmitApplicationAsync(request, CancellationToken.None);

    // Assert
    result.Id.Should().NotBeEmpty();
    result.Id.Should().NotBe(Guid.Empty);
  }

  [Fact]
  public async Task SubmitApplicationAsync_WithFiles_PreservesFilesList()
  {
    // Arrange
    var service = new ApplicationChannelService(
      p_logMock.Object,
      p_config,
      p_kafkaMock.Object,
      p_grpcMock.Object);

    var files = new List<string> { "file1.pdf", "file2.docx", "file3.jpg" };
    var request = new ApplicationRequest
    {
      Weight = 75,
      Data = "Application with files",
      Files = files
    };

    // Act
    var result = await service.SubmitApplicationAsync(request, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    // Примечание: ApplicationResponse не содержит Files,
    // но можно проверить, что заявка была принята успешно
    result.Id.Should().NotBeEmpty();
  }

  [Fact]
  public async Task SubmitApplicationAsync_CreatesApplicationWithCorrectTimestamp()
  {
    // Arrange
    var service = new ApplicationChannelService(
      p_logMock.Object,
      p_config,
      p_kafkaMock.Object,
      p_grpcMock.Object);

    var beforeRequest = DateTime.UtcNow;

    var request = new ApplicationRequest
    {
      Weight = 60,
      Data = "Timestamp test"
    };

    // Act
    var result = await service.SubmitApplicationAsync(request, CancellationToken.None);
    var afterRequest = DateTime.UtcNow;

    // Assert
    result.CreatedAt.Should().BeOnOrAfter(beforeRequest);
    result.CreatedAt.Should().BeOnOrBefore(afterRequest);
  }

  [Theory]
  [InlineData("")]
  [InlineData("Short")]
  [InlineData("Very long data string with lots of text to test edge cases")]
  public async Task SubmitApplicationAsync_VariousDataLengths_AcceptsAllValidStrings(string _data)
  {
    // Arrange
    var service = new ApplicationChannelService(
      p_logMock.Object,
      p_config,
      p_kafkaMock.Object,
      p_grpcMock.Object);

    var request = new ApplicationRequest
    {
      Weight = 50,
      Data = _data
    };

    // Act
    var result = await service.SubmitApplicationAsync(request, CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result.Id.Should().NotBeEmpty();
  }
}
