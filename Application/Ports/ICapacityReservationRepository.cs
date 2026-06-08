using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.Application.Ports;

public interface ICapacityReservationRepository
{
    Task<CapacityReservation?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<CapacityReservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken);
    Task AddAsync(CapacityReservation reservation, CancellationToken cancellationToken);
}
