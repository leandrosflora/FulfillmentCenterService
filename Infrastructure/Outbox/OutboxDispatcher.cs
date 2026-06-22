using Confluent.Kafka;
using FulfillmentCenterService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FulfillmentCenterService.Infrastructure.Messaging;

namespace FulfillmentCenterService.Infrastructure.Outbox;

public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<OutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var producerConfig = new ProducerConfig { BootstrapServers = _options.BootstrapServers };

        using var producer = new ProducerBuilder<string, string>(producerConfig).Build();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await DispatchBatchAsync(producer, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox dispatch cycle failed");
            }
        }
    }

    private async Task DispatchBatchAsync(IProducer<string, string> producer, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FulfillmentDbContext>();

        var messages = await dbContext.OutboxMessages
            .Where(x => x.ProcessedAt == null)
            .OrderBy(x => x.OccurredAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            await producer.ProduceAsync(
                message.EventType,
                new Message<string, string> { Key = message.Id.ToString(), Value = message.PayloadJson },
                cancellationToken);

            message.MarkProcessed();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
