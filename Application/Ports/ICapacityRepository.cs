using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.Application.Ports;

public interface ICapacityRepository
{
    Task<CapacityAvailability?> GetAvailabilityAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, CancellationToken cancellationToken);
    Task<bool> TryReserveAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, int capacityUnits, CancellationToken cancellationToken);
    Task<bool> ConfirmAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, int capacityUnits, CancellationToken cancellationToken);
    Task<bool> ReleaseAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, int capacityUnits, CancellationToken cancellationToken);
}

public sealed record CapacityAvailability(int TotalCapacityUnits, int ReservedCapacityUnits, int ConsumedCapacityUnits)
{
    public int AvailableCapacityUnits => TotalCapacityUnits - ReservedCapacityUnits - ConsumedCapacityUnits;
    public decimal UtilizationPercentage => TotalCapacityUnits == 0 ? 100 : decimal.Round((ReservedCapacityUnits + ConsumedCapacityUnits) * 100m / TotalCapacityUnits, 2);
}
