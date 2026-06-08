using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.Contracts;

public sealed record ConfigureCapacityRequest(DateOnly OperationDate, FulfillmentMode Mode, int TotalCapacityUnits);

public sealed record ChangeFulfillmentCenterStatusRequest(FulfillmentCenterStatus Status);
