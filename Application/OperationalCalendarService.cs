using FulfillmentCenterService.Application.Ports;

namespace FulfillmentCenterService.Application;

public sealed record OperationalWindow(DateOnly OperationDate, DateTimeOffset CutoffAt);

public sealed class OperationalCalendarService
{
    private readonly IOperationalCalendarRepository _repository;

    public OperationalCalendarService(IOperationalCalendarRepository repository) => _repository = repository;

    public async Task<OperationalWindow?> ResolveAsync(EligibleCenter center, DateTimeOffset requestedAtUtc, CancellationToken cancellationToken)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(center.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(requestedAtUtc, timeZone);
        var initialDate = DateOnly.FromDateTime(localNow.DateTime);

        for (var offset = 0; offset <= 14; offset++)
        {
            var date = initialDate.AddDays(offset);
            var schedule = await _repository.GetScheduleAsync(center.Id, date, center.Mode, cancellationToken);
            if (schedule is null || !schedule.IsOpen) continue;

            if (offset == 0)
            {
                var localTime = TimeOnly.FromDateTime(localNow.DateTime);
                if (localTime > schedule.CutoffTime) continue;
            }

            var localCutoffDateTime = date.ToDateTime(schedule.CutoffTime, DateTimeKind.Unspecified);
            var utcOffset = timeZone.GetUtcOffset(localCutoffDateTime);
            return new OperationalWindow(date, new DateTimeOffset(localCutoffDateTime, utcOffset));
        }

        return null;
    }
}
