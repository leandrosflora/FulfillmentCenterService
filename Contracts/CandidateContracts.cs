using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.Contracts;

public sealed record SearchCandidatesRequest(Guid SellerId, string DestinationPostalCode, FulfillmentMode Mode, PackageProfileDto Package, DateTimeOffset? RequestedAtUtc);

public sealed record PackageProfileDto(decimal WeightKg, decimal CubicWeightKg, bool IsFragile, bool IsRestricted);

public sealed record FulfillmentCandidateResponse(Guid FulfillmentCenterId, string Code, string Name, string Region, FulfillmentMode Mode, DateOnly ProcessingDate, DateTimeOffset CutoffAt, int AvailableCapacityUnits, decimal UtilizationPercentage, int Score);
