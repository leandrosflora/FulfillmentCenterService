using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.Contracts;

public sealed record CreateCapacityReservationRequest(Guid OrderId, Guid FulfillmentCenterId, DateOnly OperationDate, FulfillmentMode Mode, int RequiredCapacityUnits);

public sealed record CapacityReservationResponse(Guid ReservationId, Guid OrderId, Guid FulfillmentCenterId, DateOnly OperationDate, FulfillmentMode Mode, int ReservedCapacityUnits, string Status, DateTimeOffset ExpiresAt);
