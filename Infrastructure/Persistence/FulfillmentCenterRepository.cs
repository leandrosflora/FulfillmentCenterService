using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Domain;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentCenterService.Infrastructure.Persistence;

public sealed class FulfillmentCenterRepository : IFulfillmentCenterRepository
{
    private readonly FulfillmentDbContext _dbContext;
    public FulfillmentCenterRepository(FulfillmentDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<EligibleCenter>> FindEligibleAsync(Guid sellerId, long destinationPostalCode, FulfillmentMode mode, CancellationToken cancellationToken)
    {
        var query = from center in _dbContext.FulfillmentCenters
                    join enrollment in _dbContext.SellerCenterEnrollments on center.Id equals enrollment.FulfillmentCenterId
                    join coverage in _dbContext.CenterCoverages on center.Id equals coverage.FulfillmentCenterId
                    where center.Status == FulfillmentCenterStatus.Active
                    where enrollment.SellerId == sellerId
                    where enrollment.Mode == mode
                    where enrollment.IsActive
                    where coverage.Mode == mode
                    where destinationPostalCode >= coverage.PostalCodeFrom
                    where destinationPostalCode <= coverage.PostalCodeTo
                    select new EligibleCenter(center.Id, center.Code, center.Name, center.Region, center.TimeZoneId, mode, coverage.Priority, center.MaximumWeightKg, center.MaximumCubicWeightKg, center.SupportsFragileItems, center.SupportsRestrictedItems);

        return await query.AsNoTracking().ToListAsync(cancellationToken);
    }
}
