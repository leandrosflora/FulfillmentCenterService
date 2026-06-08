using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Domain;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentCenterService.Infrastructure.Persistence;

public sealed class CapacityReservationRepository : ICapacityReservationRepository
{
    private readonly FulfillmentDbContext _dbContext;
    public CapacityReservationRepository(FulfillmentDbContext dbContext) => _dbContext = dbContext;

    public Task<CapacityReservation?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        _dbContext.CapacityReservations.FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<CapacityReservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken) =>
        _dbContext.CapacityReservations.FirstOrDefaultAsync(x => x.Id == reservationId, cancellationToken);

    public Task AddAsync(CapacityReservation reservation, CancellationToken cancellationToken)
    {
        _dbContext.CapacityReservations.Add(reservation);
        return Task.CompletedTask;
    }
}
