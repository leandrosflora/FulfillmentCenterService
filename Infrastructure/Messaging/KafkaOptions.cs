namespace FulfillmentCenterService.Infrastructure.Messaging;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = "localhost:9092";
    public string ConsumerGroupId { get; init; } = "fulfillment-center-service";
    public KafkaTopics Topics { get; init; } = new();
}

public sealed class KafkaTopics
{
    public string FulfillmentCommands { get; init; } = "fulfillment.commands";
    public string FulfillmentCapacityReserved { get; init; } = "fulfillment.capacity.reserved";
    public string FulfillmentCapacityConfirmed { get; init; } = "fulfillment.capacity.confirmed";
    public string FulfillmentCapacityFailed { get; init; } = "fulfillment.capacity.failed";
}
