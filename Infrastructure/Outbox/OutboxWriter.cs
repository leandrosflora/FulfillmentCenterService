using System.Text.Json;
using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Infrastructure.Persistence;

namespace FulfillmentCenterService.Infrastructure.Outbox;

public sealed class OutboxWriter : IOutboxWriter
{
    private readonly FulfillmentDbContext _dbContext;

    public OutboxWriter(FulfillmentDbContext dbContext) => _dbContext = dbContext;

    public Task AddAsync(string eventType, object payload, CancellationToken cancellationToken)
    {
        var envelope = new
        {
            EventId = Guid.NewGuid(),
            EventType = eventType,
            OccurredAt = DateTimeOffset.UtcNow,
            Payload = payload
        };
        var json = JsonSerializer.Serialize(envelope);
        _dbContext.OutboxMessages.Add(new OutboxMessage(eventType, json));
        return Task.CompletedTask;
    }
}
