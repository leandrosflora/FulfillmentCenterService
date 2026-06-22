using System.Text.Json;
using System.Text.Json.Serialization;
using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Infrastructure.Persistence;

namespace FulfillmentCenterService.Infrastructure.Outbox;

public sealed class OutboxWriter : IOutboxWriter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private readonly FulfillmentDbContext _dbContext;

    public OutboxWriter(FulfillmentDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(string eventType, object payload, CancellationToken cancellationToken)
    {
        var payloadElement = JsonSerializer.SerializeToElement(payload, Options);
        var envelope = new CanonicalEnvelope(
            Guid.NewGuid(),
            eventType,
            "1.0",
            DateTimeOffset.UtcNow,
            Guid.NewGuid().ToString(),
            "fulfillment-center-service",
            payloadElement);
        var json = JsonSerializer.Serialize(envelope, Options);
        _dbContext.OutboxMessages.Add(new OutboxMessage(eventType, json));
        return Task.CompletedTask;
    }
}

internal sealed record CanonicalEnvelope(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("producer")] string Producer,
    [property: JsonPropertyName("payload")] JsonElement Payload);
