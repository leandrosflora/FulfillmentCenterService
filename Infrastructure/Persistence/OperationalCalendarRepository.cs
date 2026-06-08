using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Domain;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentCenterService.Infrastructure.Persistence;

public sealed class OperationalCalendarRepository : IOperationalCalendarRepository
{
    private readonly FulfillmentDbContext _dbContext;
    public OperationalCalendarRepository(FulfillmentDbContext dbContext) => _dbContext = dbContext;

    public Task<OperationSchedule?> GetScheduleAsync(Guid fulfillmentCenterId, DateOnly date, FulfillmentMode mode, CancellationToken cancellationToken) =>
        _dbContext.OperationSchedules.AsNoTracking()
            .Where(x => x.FulfillmentCenterId == fulfillmentCenterId && x.OperationDate == date && x.Mode == mode)
            .Select(x => new OperationSchedule(x.IsOpen, x.OpeningTime, x.CutoffTime, x.ClosingTime))
            .FirstOrDefaultAsync(cancellationToken);
}
