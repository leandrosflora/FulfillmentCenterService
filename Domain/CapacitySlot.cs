namespace FulfillmentCenterService.Domain;

public sealed class CapacitySlot
{
    public Guid Id { get; private set; }
    public Guid FulfillmentCenterId { get; private set; }
    public DateOnly OperationDate { get; private set; }
    public FulfillmentMode Mode { get; private set; }
    public int TotalCapacityUnits { get; private set; }
    public int ReservedCapacityUnits { get; private set; }
    public int ConsumedCapacityUnits { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private CapacitySlot() { }

    public int AvailableCapacityUnits => TotalCapacityUnits - ReservedCapacityUnits - ConsumedCapacityUnits;
    public decimal UtilizationPercentage => TotalCapacityUnits == 0 ? 100 : decimal.Round((ReservedCapacityUnits + ConsumedCapacityUnits) * 100m / TotalCapacityUnits, 2);

    public CapacitySlot(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, int totalCapacityUnits)
    {
        if (fulfillmentCenterId == Guid.Empty) throw new ArgumentException("FulfillmentCenterId is required", nameof(fulfillmentCenterId));
        if (totalCapacityUnits < 0) throw new ArgumentException("Capacity cannot be negative", nameof(totalCapacityUnits));
        Id = Guid.NewGuid();
        FulfillmentCenterId = fulfillmentCenterId;
        OperationDate = operationDate;
        Mode = mode;
        TotalCapacityUnits = totalCapacityUnits;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reconfigure(int totalCapacityUnits)
    {
        if (totalCapacityUnits < ReservedCapacityUnits + ConsumedCapacityUnits)
            throw new InvalidOperationException("Capacity cannot be lower than already allocated capacity");
        TotalCapacityUnits = totalCapacityUnits;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
