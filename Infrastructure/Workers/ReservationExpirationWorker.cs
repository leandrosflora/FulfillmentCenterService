using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Domain;
using FulfillmentCenterService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentCenterService.Infrastructure.Workers;

public sealed class ReservationExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReservationExpirationWorker> _logger;

    public ReservationExpirationWorker(IServiceScopeFactory scopeFactory, ILogger<ReservationExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ExpireReservationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error expiring fulfillment capacity reservations");
            }
        }
    }

    private async Task ExpireReservationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FulfillmentDbContext>();
        var capacityRepository = scope.ServiceProvider.GetRequiredService<ICapacityRepository>();
        var outboxWriter = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

        var reservations = await dbContext.CapacityReservations
            .Where(x => x.Status == CapacityReservationStatus.Pending && x.ExpiresAt <= DateTimeOffset.UtcNow)
            .OrderBy(x => x.ExpiresAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var reservation in reservations)
        {
            var released = await capacityRepository.ReleaseAsync(reservation.FulfillmentCenterId, reservation.OperationDate, reservation.Mode, reservation.ReservedCapacityUnits, cancellationToken);
            if (!released) continue;

            reservation.Expire();
            await outboxWriter.AddAsync("FulfillmentCapacityReservationExpired", new { ReservationId = reservation.Id, reservation.OrderId, reservation.FulfillmentCenterId, reservation.OperationDate, reservation.ReservedCapacityUnits, reservation.ReleasedAt }, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
