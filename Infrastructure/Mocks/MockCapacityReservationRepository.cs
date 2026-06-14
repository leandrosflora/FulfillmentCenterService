using System.Collections.Concurrent;
using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.Infrastructure.Mocks;

public sealed class MockCapacityReservationRepository : ICapacityReservationRepository
{
    private static readonly ConcurrentDictionary<Guid, CapacityReservation> ReservationsById = new();
    private static readonly ConcurrentDictionary<string, Guid> ReservationIdsByIdempotencyKey = new(StringComparer.Ordinal);

    public Task<CapacityReservation?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        if (ReservationIdsByIdempotencyKey.TryGetValue(idempotencyKey, out var reservationId) && ReservationsById.TryGetValue(reservationId, out var reservation))
        {
            return Task.FromResult<CapacityReservation?>(reservation);
        }

        return Task.FromResult<CapacityReservation?>(null);
    }

    public Task<CapacityReservation?> GetByIdAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        ReservationsById.TryGetValue(reservationId, out var reservation);
        return Task.FromResult<CapacityReservation?>(reservation);
    }

    public Task AddAsync(CapacityReservation reservation, CancellationToken cancellationToken)
    {
        ReservationsById[reservation.Id] = reservation;
        ReservationIdsByIdempotencyKey[reservation.IdempotencyKey] = reservation.Id;
        return Task.CompletedTask;
    }
}
