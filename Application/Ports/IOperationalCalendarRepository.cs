using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.Application.Ports;

public interface IOperationalCalendarRepository
{
    Task<OperationSchedule?> GetScheduleAsync(Guid fulfillmentCenterId, DateOnly date, FulfillmentMode mode, CancellationToken cancellationToken);
}

public sealed record OperationSchedule(bool IsOpen, TimeOnly OpeningTime, TimeOnly CutoffTime, TimeOnly ClosingTime);
