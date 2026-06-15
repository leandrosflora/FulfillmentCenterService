using FulfillmentCenterService.Application;
using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.UnitTests;

public sealed class OperationalCalendarServiceTests
{
    [Fact]
    public async Task ResolveAsync_returns_same_day_window_when_center_is_open_before_cutoff()
    {
        var centerId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var date = new DateOnly(2026, 6, 14);
        var repository = new StubOperationalCalendarRepository(new Dictionary<DateOnly, OperationSchedule?>
        {
            [date] = new(true, new TimeOnly(8, 0), new TimeOnly(16, 0), new TimeOnly(22, 0))
        });
        var service = new OperationalCalendarService(repository);

        var window = await service.ResolveAsync(CreateCenter(centerId), new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero), CancellationToken.None);

        Assert.NotNull(window);
        Assert.Equal(date, window.OperationDate);
        Assert.Equal(new DateTimeOffset(2026, 6, 14, 16, 0, 0, TimeSpan.Zero), window.CutoffAt);
    }

    [Fact]
    public async Task ResolveAsync_skips_closed_or_past_cutoff_days_and_uses_next_open_day()
    {
        var centerId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var repository = new StubOperationalCalendarRepository(new Dictionary<DateOnly, OperationSchedule?>
        {
            [new DateOnly(2026, 6, 14)] = new(true, new TimeOnly(8, 0), new TimeOnly(11, 0), new TimeOnly(22, 0)),
            [new DateOnly(2026, 6, 15)] = new(false, new TimeOnly(8, 0), new TimeOnly(16, 0), new TimeOnly(22, 0)),
            [new DateOnly(2026, 6, 16)] = new(true, new TimeOnly(8, 0), new TimeOnly(17, 0), new TimeOnly(22, 0))
        });
        var service = new OperationalCalendarService(repository);

        var window = await service.ResolveAsync(CreateCenter(centerId), new DateTimeOffset(2026, 6, 14, 12, 0, 0, TimeSpan.Zero), CancellationToken.None);

        Assert.NotNull(window);
        Assert.Equal(new DateOnly(2026, 6, 16), window.OperationDate);
        Assert.Equal(new DateTimeOffset(2026, 6, 16, 17, 0, 0, TimeSpan.Zero), window.CutoffAt);
    }

    private static EligibleCenter CreateCenter(Guid centerId) => new(centerId, "BR-SP01", "São Paulo 01", "SP", "UTC", FulfillmentMode.Fulfillment, 1, 30m, 40m, true, true);
}
