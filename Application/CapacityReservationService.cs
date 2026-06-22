using System.Text.Json.Serialization;
using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Contracts;
using FulfillmentCenterService.Domain;
using FulfillmentCenterService.Infrastructure.Persistence;

namespace FulfillmentCenterService.Application;

public sealed class CapacityReservationService
{
    private readonly FulfillmentDbContext _dbContext;
    private readonly ICapacityRepository _capacityRepository;
    private readonly ICapacityReservationRepository _reservationRepository;
    private readonly IOutboxWriter _outboxWriter;

    public CapacityReservationService(FulfillmentDbContext dbContext, ICapacityRepository capacityRepository, ICapacityReservationRepository reservationRepository, IOutboxWriter outboxWriter)
    {
        _dbContext = dbContext;
        _capacityRepository = capacityRepository;
        _reservationRepository = reservationRepository;
        _outboxWriter = outboxWriter;
    }

    public async Task<CapacityReservationResponse> CreateAsync(CreateCapacityReservationRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        Validate(request);
        var existing = await _reservationRepository.FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null) return Map(existing);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var reserved = await _capacityRepository.TryReserveAsync(request.FulfillmentCenterId, request.OperationDate, request.Mode, request.RequiredCapacityUnits, cancellationToken);
        if (!reserved) throw new InvalidOperationException("Fulfillment center has insufficient capacity");

        var reservation = CapacityReservation.Create(request.OrderId, request.FulfillmentCenterId, request.OperationDate, request.Mode, request.RequiredCapacityUnits, idempotencyKey);
        await _reservationRepository.AddAsync(reservation, cancellationToken);
        await _outboxWriter.AddAsync("fulfillment.capacity.reserved", new FulfillmentCapacityReservedPayload(reservation.OrderId, reservation.Id), cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(reservation);
    }

    public async Task<CapacityReservationResponse> ConfirmAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var reservation = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken) ?? throw new KeyNotFoundException("Reservation not found");
        if (reservation.Status == CapacityReservationStatus.Confirmed) return Map(reservation);

        reservation.Confirm();
        var confirmed = await _capacityRepository.ConfirmAsync(reservation.FulfillmentCenterId, reservation.OperationDate, reservation.Mode, reservation.ReservedCapacityUnits, cancellationToken);
        if (!confirmed) throw new InvalidOperationException("Could not confirm fulfillment capacity");

        await _outboxWriter.AddAsync("fulfillment.capacity.confirmed", new FulfillmentCapacityConfirmedPayload(reservation.OrderId, reservation.Id), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(reservation);
    }

    public async Task<CapacityReservationResponse> ReleaseAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var reservation = await _reservationRepository.GetByIdAsync(reservationId, cancellationToken) ?? throw new KeyNotFoundException("Reservation not found");
        if (reservation.Status is CapacityReservationStatus.Released or CapacityReservationStatus.Expired) return Map(reservation);

        reservation.Release();
        var released = await _capacityRepository.ReleaseAsync(reservation.FulfillmentCenterId, reservation.OperationDate, reservation.Mode, reservation.ReservedCapacityUnits, cancellationToken);
        if (!released) throw new InvalidOperationException("Could not release fulfillment capacity");

        await _outboxWriter.AddAsync("fulfillment.capacity.released", new FulfillmentCapacityReleasedPayload(reservation.OrderId, reservation.Id), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(reservation);
    }

    private static void Validate(CreateCapacityReservationRequest request)
    {
        if (request.OrderId == Guid.Empty) throw new ArgumentException("OrderId is required");
        if (request.FulfillmentCenterId == Guid.Empty) throw new ArgumentException("FulfillmentCenterId is required");
        if (request.RequiredCapacityUnits <= 0) throw new ArgumentException("RequiredCapacityUnits must be positive");
    }

    private static CapacityReservationResponse Map(CapacityReservation reservation) => new(reservation.Id, reservation.OrderId, reservation.FulfillmentCenterId, reservation.OperationDate, reservation.Mode, reservation.ReservedCapacityUnits, reservation.Status.ToString(), reservation.ExpiresAt);
}

internal sealed record FulfillmentCapacityReservedPayload(
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("reservationId")] Guid ReservationId);

internal sealed record FulfillmentCapacityConfirmedPayload(
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("reservationId")] Guid ReservationId);

internal sealed record FulfillmentCapacityReleasedPayload(
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("reservationId")] Guid ReservationId);

internal sealed record FulfillmentCapacityFailedPayload(
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("reason")] string Reason);
