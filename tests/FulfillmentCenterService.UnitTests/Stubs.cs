using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.UnitTests;

internal sealed class StubFulfillmentCenterRepository : IFulfillmentCenterRepository
{
    private readonly IReadOnlyList<EligibleCenter> _centers;

    public StubFulfillmentCenterRepository(IReadOnlyList<EligibleCenter> centers) => _centers = centers;

    public Guid LastSellerId { get; private set; }
    public long LastDestinationPostalCode { get; private set; }

    public Task<IReadOnlyList<EligibleCenter>> FindEligibleAsync(Guid sellerId, long destinationPostalCode, FulfillmentMode mode, CancellationToken cancellationToken)
    {
        LastSellerId = sellerId;
        LastDestinationPostalCode = destinationPostalCode;
        return Task.FromResult<IReadOnlyList<EligibleCenter>>(_centers.Where(x => x.Mode == mode).ToList());
    }
}

internal sealed class StubCapacityRepository : ICapacityRepository
{
    private readonly IReadOnlyDictionary<Guid, CapacityAvailability> _availabilityByCenterId;

    public StubCapacityRepository(IReadOnlyDictionary<Guid, CapacityAvailability> availabilityByCenterId) => _availabilityByCenterId = availabilityByCenterId;

    public Task<CapacityAvailability?> GetAvailabilityAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, CancellationToken cancellationToken) =>
        Task.FromResult(_availabilityByCenterId.GetValueOrDefault(fulfillmentCenterId));

    public Task<bool> TryReserveAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, int capacityUnits, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<bool> ConfirmAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, int capacityUnits, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<bool> ReleaseAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, int capacityUnits, CancellationToken cancellationToken) => Task.FromResult(true);
}

internal sealed class StubOperationalCalendarRepository : IOperationalCalendarRepository
{
    private readonly IReadOnlyDictionary<DateOnly, OperationSchedule?>? _schedulesByDate;
    private readonly OperationSchedule? _defaultSchedule;

    public StubOperationalCalendarRepository(IReadOnlyDictionary<DateOnly, OperationSchedule?> schedulesByDate) => _schedulesByDate = schedulesByDate;

    public StubOperationalCalendarRepository(OperationSchedule? defaultSchedule) => _defaultSchedule = defaultSchedule;

    public Task<OperationSchedule?> GetScheduleAsync(Guid fulfillmentCenterId, DateOnly date, FulfillmentMode mode, CancellationToken cancellationToken)
    {
        if (_schedulesByDate is not null)
        {
            return Task.FromResult(_schedulesByDate.GetValueOrDefault(date));
        }

        return Task.FromResult(_defaultSchedule);
    }
}
