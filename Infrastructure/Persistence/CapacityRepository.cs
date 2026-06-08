using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Domain;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentCenterService.Infrastructure.Persistence;

public sealed class CapacityRepository : ICapacityRepository
{
    private readonly FulfillmentDbContext _dbContext;
    public CapacityRepository(FulfillmentDbContext dbContext) => _dbContext = dbContext;

    public Task<CapacityAvailability?> GetAvailabilityAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, CancellationToken cancellationToken) =>
        _dbContext.CapacitySlots.AsNoTracking()
            .Where(x => x.FulfillmentCenterId == fulfillmentCenterId && x.OperationDate == operationDate && x.Mode == mode)
            .Select(x => new CapacityAvailability(x.TotalCapacityUnits, x.ReservedCapacityUnits, x.ConsumedCapacityUnits))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryReserveAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, int capacityUnits, CancellationToken cancellationToken)
    {
        var affectedRows = await _dbContext.CapacitySlots
            .Where(x => x.FulfillmentCenterId == fulfillmentCenterId && x.OperationDate == operationDate && x.Mode == mode && x.TotalCapacityUnits - x.ReservedCapacityUnits - x.ConsumedCapacityUnits >= capacityUnits)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ReservedCapacityUnits, x => x.ReservedCapacityUnits + capacityUnits)
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);
        return affectedRows == 1;
    }

    public async Task<bool> ConfirmAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, int capacityUnits, CancellationToken cancellationToken)
    {
        var affectedRows = await _dbContext.CapacitySlots
            .Where(x => x.FulfillmentCenterId == fulfillmentCenterId && x.OperationDate == operationDate && x.Mode == mode && x.ReservedCapacityUnits >= capacityUnits)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ReservedCapacityUnits, x => x.ReservedCapacityUnits - capacityUnits)
                .SetProperty(x => x.ConsumedCapacityUnits, x => x.ConsumedCapacityUnits + capacityUnits)
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);
        return affectedRows == 1;
    }

    public async Task<bool> ReleaseAsync(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, int capacityUnits, CancellationToken cancellationToken)
    {
        var affectedRows = await _dbContext.CapacitySlots
            .Where(x => x.FulfillmentCenterId == fulfillmentCenterId && x.OperationDate == operationDate && x.Mode == mode && x.ReservedCapacityUnits >= capacityUnits)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ReservedCapacityUnits, x => x.ReservedCapacityUnits - capacityUnits)
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);
        return affectedRows == 1;
    }
}
