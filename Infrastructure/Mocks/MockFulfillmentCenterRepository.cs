using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.Infrastructure.Mocks;

public sealed class MockFulfillmentCenterRepository : IFulfillmentCenterRepository
{
    private static readonly IReadOnlyList<MockCenter> Centers =
    [
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "BR-SP-FC-01",
            "Fulfillment Center São Paulo",
            "SP",
            "America/Sao_Paulo",
            01000000,
            19999999,
            1,
            30m,
            45m,
            supportsFragileItems: true,
            supportsRestrictedItems: false),
        new(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "BR-RJ-FC-01",
            "Fulfillment Center Rio de Janeiro",
            "RJ",
            "America/Sao_Paulo",
            20000000,
            28999999,
            1,
            25m,
            35m,
            supportsFragileItems: false,
            supportsRestrictedItems: false),
        new(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "BR-MG-FC-01",
            "Fulfillment Center Minas Gerais",
            "MG",
            "America/Sao_Paulo",
            30000000,
            39999999,
            2,
            35m,
            50m,
            supportsFragileItems: true,
            supportsRestrictedItems: true)
    ];

    public Task<IReadOnlyList<EligibleCenter>> FindEligibleAsync(Guid sellerId, long destinationPostalCode, FulfillmentMode mode, CancellationToken cancellationToken)
    {
        var eligibleCenters = Centers
            .Where(center => destinationPostalCode >= center.PostalCodeFrom && destinationPostalCode <= center.PostalCodeTo)
            .Select(center => new EligibleCenter(
                center.Id,
                center.Code,
                center.Name,
                center.Region,
                center.TimeZoneId,
                mode,
                center.CoveragePriority,
                center.MaximumWeightKg,
                center.MaximumCubicWeightKg,
                center.SupportsFragileItems,
                center.SupportsRestrictedItems))
            .ToList();

        return Task.FromResult<IReadOnlyList<EligibleCenter>>(eligibleCenters);
    }

    private sealed record MockCenter(
        Guid Id,
        string Code,
        string Name,
        string Region,
        string TimeZoneId,
        long PostalCodeFrom,
        long PostalCodeTo,
        int CoveragePriority,
        decimal MaximumWeightKg,
        decimal MaximumCubicWeightKg,
        bool SupportsFragileItems,
        bool SupportsRestrictedItems);
}
