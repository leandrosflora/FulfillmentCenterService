namespace FulfillmentCenterService.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = default!;
    public string PayloadJson { get; private set; } = default!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(string eventType, string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("Event type is required", nameof(eventType));
        Id = Guid.NewGuid();
        EventType = eventType;
        PayloadJson = payloadJson;
        OccurredAt = DateTimeOffset.UtcNow;
    }
}
