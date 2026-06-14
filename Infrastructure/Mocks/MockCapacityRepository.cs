using System.Collections.Concurrent;
using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.Infrastructure.Mocks;

public sealed class MockCapacityRepository : ICapacityRepository
{
    private static readonly ConcurrentDictionary<CapacityKey, CapacityState> Capacities = new();

    public Task<CapacityAvailability?> GetAvailabilityAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, CancellationToken cancellationToken)
    {
        var capacity = GetOrCreate(fulfillmentCenterId, operationDate, mode);
        CapacityAvailability availability = new(capacity.TotalCapacityUnits, capacity.ReservedCapacityUnits, capacity.ConsumedCapacityUnits);
        return Task.FromResult<CapacityAvailability?>(availability);
    }

    public Task<bool> TryReserveAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, int capacityUnits, CancellationToken cancellationToken)
    {
        var capacity = GetOrCreate(fulfillmentCenterId, operationDate, mode);
        lock (capacity)
        {
            if (capacity.TotalCapacityUnits - capacity.ReservedCapacityUnits - capacity.ConsumedCapacityUnits < capacityUnits)
            {
                return Task.FromResult(false);
            }

            capacity.ReservedCapacityUnits += capacityUnits;
            return Task.FromResult(true);
        }
    }

    public Task<bool> ConfirmAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, int capacityUnits, CancellationToken cancellationToken)
    {
        var capacity = GetOrCreate(fulfillmentCenterId, operationDate, mode);
        lock (capacity)
        {
            if (capacity.ReservedCapacityUnits < capacityUnits)
            {
                return Task.FromResult(false);
            }

            capacity.ReservedCapacityUnits -= capacityUnits;
            capacity.ConsumedCapacityUnits += capacityUnits;
            return Task.FromResult(true);
        }
    }

    public Task<bool> ReleaseAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, int capacityUnits, CancellationToken cancellationToken)
    {
        var capacity = GetOrCreate(fulfillmentCenterId, operationDate, mode);
        lock (capacity)
        {
            if (capacity.ReservedCapacityUnits < capacityUnits)
            {
                return Task.FromResult(false);
            }

            capacity.ReservedCapacityUnits -= capacityUnits;
            return Task.FromResult(true);
        }
    }

    private static CapacityState GetOrCreate(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode) =>
        Capacities.GetOrAdd(new CapacityKey(fulfillmentCenterId, operationDate, mode), _ => new CapacityState(120, 12, 18));

    private sealed record CapacityKey(Guid FulfillmentCenterId, DateOnly OperationDate, FulfillmentMode Mode);

    private sealed class CapacityState(int totalCapacityUnits, int reservedCapacityUnits, int consumedCapacityUnits)
    {
        public int TotalCapacityUnits { get; } = totalCapacityUnits;
        public int ReservedCapacityUnits { get; set; } = reservedCapacityUnits;
        public int ConsumedCapacityUnits { get; set; } = consumedCapacityUnits;
    }
}
