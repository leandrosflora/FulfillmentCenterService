using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.Application.Ports;

public interface IFulfillmentCenterRepository
{
    Task<IReadOnlyList<EligibleCenter>> FindEligibleAsync(Guid sellerId, long destinationPostalCode, FulfillmentMode mode, CancellationToken cancellationToken);
}

public sealed record EligibleCenter(Guid Id, string Code, string Name, string Region, string TimeZoneId, FulfillmentMode Mode, int CoveragePriority, decimal MaximumWeightKg, decimal MaximumCubicWeightKg, bool SupportsFragileItems, bool SupportsRestrictedItems);
