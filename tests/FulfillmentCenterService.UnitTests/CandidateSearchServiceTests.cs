using FulfillmentCenterService.Application;
using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Contracts;
using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.UnitTests;

public sealed class CandidateSearchServiceTests
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset RequestedAt = new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SearchAsync_returns_candidates_ordered_by_contract_score_without_external_dependencies()
    {
        var firstCenterId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var secondCenterId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var centerRepository = new StubFulfillmentCenterRepository(
        [
            new EligibleCenter(secondCenterId, "BR-SP02", "São Paulo 02", "SP", "UTC", FulfillmentMode.Fulfillment, 2, 30m, 40m, true, true),
            new EligibleCenter(firstCenterId, "BR-SP01", "São Paulo 01", "SP", "UTC", FulfillmentMode.Fulfillment, 1, 30m, 40m, true, true)
        ]);
        var capacityRepository = new StubCapacityRepository(new Dictionary<Guid, CapacityAvailability>
        {
            [firstCenterId] = new(100, 20, 10),
            [secondCenterId] = new(100, 5, 10)
        });
        var calendarRepository = new StubOperationalCalendarRepository(new OperationSchedule(true, new TimeOnly(8, 0), new TimeOnly(18, 0), new TimeOnly(22, 0)));
        var service = new CandidateSearchService(centerRepository, capacityRepository, new OperationalCalendarService(calendarRepository));

        var request = new SearchCandidatesRequest(
            SellerId,
            "01001-000",
            FulfillmentMode.Fulfillment,
            new PackageProfileDto(2.0m, 3.0m, IsFragile: false, IsRestricted: false),
            RequestedAt);

        var candidates = await service.SearchAsync(request, CancellationToken.None);

        Assert.Equal(new[] { firstCenterId, secondCenterId }, candidates.Select(x => x.FulfillmentCenterId));
        Assert.All(candidates, candidate =>
        {
            Assert.Equal(FulfillmentMode.Fulfillment, candidate.Mode);
            Assert.Equal(new DateOnly(2026, 6, 14), candidate.ProcessingDate);
            Assert.True(candidate.AvailableCapacityUnits >= 3);
        });
        Assert.Equal(1000, centerRepository.LastDestinationPostalCode);
        Assert.Equal(SellerId, centerRepository.LastSellerId);
    }

    [Fact]
    public async Task SearchAsync_filters_centers_that_do_not_support_package_capacity_or_operational_calendar()
    {
        var unsupportedPackageCenterId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var insufficientCapacityCenterId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var eligibleCenterId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var centerRepository = new StubFulfillmentCenterRepository(
        [
            new EligibleCenter(unsupportedPackageCenterId, "BR-RJ01", "Rio 01", "RJ", "UTC", FulfillmentMode.CrossDocking, 1, 5m, 5m, false, false),
            new EligibleCenter(insufficientCapacityCenterId, "BR-RJ02", "Rio 02", "RJ", "UTC", FulfillmentMode.CrossDocking, 1, 50m, 50m, true, true),
            new EligibleCenter(eligibleCenterId, "BR-RJ03", "Rio 03", "RJ", "UTC", FulfillmentMode.CrossDocking, 1, 50m, 50m, true, true)
        ]);
        var capacityRepository = new StubCapacityRepository(new Dictionary<Guid, CapacityAvailability>
        {
            [insufficientCapacityCenterId] = new(10, 9, 0),
            [eligibleCenterId] = new(10, 1, 0)
        });
        var calendarRepository = new StubOperationalCalendarRepository(new OperationSchedule(true, new TimeOnly(8, 0), new TimeOnly(18, 0), new TimeOnly(22, 0)));
        var service = new CandidateSearchService(centerRepository, capacityRepository, new OperationalCalendarService(calendarRepository));

        var request = new SearchCandidatesRequest(
            SellerId,
            "20040-020",
            FulfillmentMode.CrossDocking,
            new PackageProfileDto(2.0m, 3.0m, IsFragile: true, IsRestricted: true),
            RequestedAt);

        var candidate = Assert.Single(await service.SearchAsync(request, CancellationToken.None));

        Assert.Equal(eligibleCenterId, candidate.FulfillmentCenterId);
        Assert.Equal("BR-RJ03", candidate.Code);
    }

    [Theory]
    [InlineData("", "DestinationPostalCode is required")]
    [InlineData("123", "Invalid postal code")]
    public async Task SearchAsync_rejects_invalid_postal_code_requests(string postalCode, string expectedMessage)
    {
        var service = new CandidateSearchService(
            new StubFulfillmentCenterRepository([]),
            new StubCapacityRepository(new Dictionary<Guid, CapacityAvailability>()),
            new OperationalCalendarService(new StubOperationalCalendarRepository((OperationSchedule?)null)));
        var request = new SearchCandidatesRequest(SellerId, postalCode, FulfillmentMode.Fulfillment, new PackageProfileDto(1m, 0m, false, false), RequestedAt);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync(request, CancellationToken.None));

        Assert.Contains(expectedMessage, exception.Message);
    }
}
