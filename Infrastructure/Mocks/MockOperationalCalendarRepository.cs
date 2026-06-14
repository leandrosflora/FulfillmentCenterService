using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.Infrastructure.Mocks;

public sealed class MockOperationalCalendarRepository : IOperationalCalendarRepository
{
    public Task<OperationSchedule?> GetScheduleAsync(Guid fulfillmentCenterId, DateOnly date, FulfillmentMode mode, CancellationToken cancellationToken)
    {
        var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        OperationSchedule? schedule = isWeekend
            ? new OperationSchedule(false, new TimeOnly(0, 0), new TimeOnly(0, 0), new TimeOnly(0, 0))
            : new OperationSchedule(true, new TimeOnly(8, 0), new TimeOnly(17, 0), new TimeOnly(22, 0));

        return Task.FromResult(schedule);
    }
}
