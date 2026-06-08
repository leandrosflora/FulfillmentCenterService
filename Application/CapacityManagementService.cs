using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Contracts;
using FulfillmentCenterService.Domain;
using FulfillmentCenterService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FulfillmentCenterService.Application;

public sealed class CapacityManagementService
{
    private readonly FulfillmentDbContext _dbContext;
    private readonly IOutboxWriter _outboxWriter;

    public CapacityManagementService(FulfillmentDbContext dbContext, IOutboxWriter outboxWriter)
    {
        _dbContext = dbContext;
        _outboxWriter = outboxWriter;
    }

    public async Task ConfigureCapacityAsync(Guid fulfillmentCenterId, ConfigureCapacityRequest request, CancellationToken cancellationToken)
    {
        if (fulfillmentCenterId == Guid.Empty) throw new ArgumentException("FulfillmentCenterId is required");
        var exists = await _dbContext.FulfillmentCenters.AnyAsync(x => x.Id == fulfillmentCenterId, cancellationToken);
        if (!exists) throw new KeyNotFoundException("Fulfillment center not found");

        var slot = await _dbContext.CapacitySlots.FirstOrDefaultAsync(x => x.FulfillmentCenterId == fulfillmentCenterId && x.OperationDate == request.OperationDate && x.Mode == request.Mode, cancellationToken);
        if (slot is null)
        {
            slot = new CapacitySlot(fulfillmentCenterId, request.OperationDate, request.Mode, request.TotalCapacityUnits);
            _dbContext.CapacitySlots.Add(slot);
        }
        else
        {
            slot.Reconfigure(request.TotalCapacityUnits);
        }

        await _outboxWriter.AddAsync("FulfillmentCapacityConfigured", new { fulfillmentCenterId, request.OperationDate, request.Mode, request.TotalCapacityUnits }, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeStatusAsync(Guid fulfillmentCenterId, ChangeFulfillmentCenterStatusRequest request, CancellationToken cancellationToken)
    {
        var center = await _dbContext.FulfillmentCenters.FirstOrDefaultAsync(x => x.Id == fulfillmentCenterId, cancellationToken) ?? throw new KeyNotFoundException("Fulfillment center not found");
        center.ChangeStatus(request.Status);
        await _outboxWriter.AddAsync("FulfillmentCenterStatusChanged", new { fulfillmentCenterId, request.Status }, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
