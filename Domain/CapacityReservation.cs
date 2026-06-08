namespace FulfillmentCenterService.Domain;

public sealed class CapacityReservation
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid FulfillmentCenterId { get; private set; }
    public DateOnly OperationDate { get; private set; }
    public FulfillmentMode Mode { get; private set; }
    public int ReservedCapacityUnits { get; private set; }
    public string IdempotencyKey { get; private set; } = default!;
    public CapacityReservationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }

    private CapacityReservation() { }

    public static CapacityReservation Create(Guid orderId, Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, int capacityUnits, string idempotencyKey)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("OrderId is required", nameof(orderId));
        if (fulfillmentCenterId == Guid.Empty) throw new ArgumentException("FulfillmentCenterId is required", nameof(fulfillmentCenterId));
        if (capacityUnits <= 0) throw new ArgumentException("Capacity units must be positive", nameof(capacityUnits));
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency key is required", nameof(idempotencyKey));

        var now = DateTimeOffset.UtcNow;
        return new CapacityReservation
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            FulfillmentCenterId = fulfillmentCenterId,
            OperationDate = operationDate,
            Mode = mode,
            ReservedCapacityUnits = capacityUnits,
            IdempotencyKey = idempotencyKey,
            Status = CapacityReservationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(15)
        };
    }

    public void Confirm()
    {
        if (Status == CapacityReservationStatus.Confirmed) return;
        if (Status != CapacityReservationStatus.Pending) throw new InvalidOperationException("Reservation is not pending");
        if (ExpiresAt <= DateTimeOffset.UtcNow) throw new InvalidOperationException("Reservation expired");
        Status = CapacityReservationStatus.Confirmed;
        ConfirmedAt = DateTimeOffset.UtcNow;
    }

    public void Release()
    {
        if (Status is CapacityReservationStatus.Released or CapacityReservationStatus.Expired) return;
        if (Status == CapacityReservationStatus.Confirmed) throw new InvalidOperationException("Confirmed capacity cannot be released by this operation");
        Status = CapacityReservationStatus.Released;
        ReleasedAt = DateTimeOffset.UtcNow;
    }

    public void Expire()
    {
        if (Status != CapacityReservationStatus.Pending) return;
        Status = CapacityReservationStatus.Expired;
        ReleasedAt = DateTimeOffset.UtcNow;
    }
}
