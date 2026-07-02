using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using FulfillmentCenterService.Application;
using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Contracts;
using FulfillmentCenterService.Domain;
using FulfillmentCenterService.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace FulfillmentCenterService.Infrastructure.Messaging;

public sealed class FulfillmentCommandsConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<FulfillmentCommandsConsumer> _logger;

    public FulfillmentCommandsConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<FulfillmentCommandsConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(_options.Topics.FulfillmentCommands);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                var discriminator = JsonSerializer.Deserialize<CommandDiscriminator>(result.Message.Value, JsonOptions);

                if (discriminator is null)
                {
                    consumer.Commit(result);
                    continue;
                }

                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<CapacityReservationService>();

                switch (discriminator.CommandType)
                {
                    case "ReserveFulfillmentCapacity":
                    {
                        var cmd = JsonSerializer.Deserialize<ReserveFulfillmentCapacityCommand>(result.Message.Value, JsonOptions);
                        if (cmd is not null)
                        {
                            var idempotencyKey = $"reserve:{cmd.OrderId}";
                            var request = new CreateCapacityReservationRequest(
                                cmd.OrderId,
                                cmd.FulfillmentCenterId,
                                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                                FulfillmentMode.Fulfillment,
                                cmd.CapacityUnits);
                            try
                            {
                                await service.CreateAsync(request, idempotencyKey, stoppingToken);
                            }
                            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                            {
                                // Both business-rule rejections (no capacity) and malformed
                                // commands mean this reservation can never succeed; treat them
                                // the same so a bad message doesn't permanently block this
                                // consumer's offset from advancing.
                                using var failScope = _scopeFactory.CreateScope();
                                var writer = failScope.ServiceProvider.GetRequiredService<IOutboxWriter>();
                                var dbCtx = failScope.ServiceProvider.GetRequiredService<FulfillmentDbContext>();
                                await writer.AddAsync(
                                    "fulfillment.capacity.failed",
                                    new FulfillmentCapacityFailedPayload(cmd.OrderId, ex.Message),
                                    stoppingToken);
                                await dbCtx.SaveChangesAsync(stoppingToken);
                                _logger.LogWarning("Fulfillment capacity reservation failed for order {OrderId}: {Reason}", cmd.OrderId, ex.Message);
                            }
                        }
                        break;
                    }
                    case "ConfirmFulfillmentCapacity":
                    {
                        var cmd = JsonSerializer.Deserialize<ConfirmFulfillmentCapacityCommand>(result.Message.Value, JsonOptions);
                        if (cmd is not null)
                            await service.ConfirmAsync(cmd.ReservationId, stoppingToken);
                        break;
                    }
                    case "ReleaseFulfillmentCapacity":
                    {
                        var cmd = JsonSerializer.Deserialize<ReleaseFulfillmentCapacityCommand>(result.Message.Value, JsonOptions);
                        if (cmd is not null)
                            await service.ReleaseAsync(cmd.ReservationId, stoppingToken);
                        break;
                    }
                    default:
                        _logger.LogWarning("Unknown fulfillment command type: {CommandType}", discriminator.CommandType);
                        break;
                }

                consumer.Commit(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to consume topic {Topic}", _options.Topics.FulfillmentCommands);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        consumer.Close();
    }
}

internal sealed record CommandDiscriminator(
    [property: JsonPropertyName("commandType")] string CommandType);

internal sealed record ReserveFulfillmentCapacityCommand(
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("fulfillmentCenterId")] Guid FulfillmentCenterId,
    [property: JsonPropertyName("capacityUnits")] int CapacityUnits);

internal sealed record ConfirmFulfillmentCapacityCommand(
    [property: JsonPropertyName("reservationId")] Guid ReservationId);

internal sealed record ReleaseFulfillmentCapacityCommand(
    [property: JsonPropertyName("reservationId")] Guid ReservationId);
