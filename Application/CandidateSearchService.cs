using FulfillmentCenterService.Application.Ports;
using FulfillmentCenterService.Contracts;

namespace FulfillmentCenterService.Application;

public sealed class CandidateSearchService
{
    private readonly IFulfillmentCenterRepository _centerRepository;
    private readonly ICapacityRepository _capacityRepository;
    private readonly OperationalCalendarService _calendarService;

    public CandidateSearchService(IFulfillmentCenterRepository centerRepository, ICapacityRepository capacityRepository, OperationalCalendarService calendarService)
    {
        _centerRepository = centerRepository;
        _capacityRepository = capacityRepository;
        _calendarService = calendarService;
    }

    public async Task<IReadOnlyList<FulfillmentCandidateResponse>> SearchAsync(SearchCandidatesRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var postalCode = NormalizePostalCode(request.DestinationPostalCode);
        var requestedAt = request.RequestedAtUtc ?? DateTimeOffset.UtcNow;
        var requiredCapacityUnits = CalculateCapacityUnits(request.Package);

        var eligibleCenters = await _centerRepository.FindEligibleAsync(request.SellerId, postalCode, request.Mode, cancellationToken);
        var candidates = new List<FulfillmentCandidateResponse>();

        foreach (var center in eligibleCenters)
        {
            if (!SupportsPackage(center, request.Package)) continue;

            var operationalWindow = await _calendarService.ResolveAsync(center, requestedAt, cancellationToken);
            if (operationalWindow is null) continue;

            var capacity = await _capacityRepository.GetAvailabilityAsync(center.Id, operationalWindow.OperationDate, request.Mode, cancellationToken);
            if (capacity is null || capacity.AvailableCapacityUnits < requiredCapacityUnits) continue;

            var score = CalculateScore(center.CoveragePriority, capacity.UtilizationPercentage, operationalWindow.OperationDate, requestedAt);
            candidates.Add(new FulfillmentCandidateResponse(center.Id, center.Code, center.Name, center.Region, request.Mode, operationalWindow.OperationDate, operationalWindow.CutoffAt, capacity.AvailableCapacityUnits, capacity.UtilizationPercentage, score));
        }

        return candidates.OrderBy(x => x.Score).ThenByDescending(x => x.AvailableCapacityUnits).ToList();
    }

    private static bool SupportsPackage(EligibleCenter center, PackageProfileDto package)
    {
        if (package.WeightKg > center.MaximumWeightKg) return false;
        if (package.CubicWeightKg > center.MaximumCubicWeightKg) return false;
        if (package.IsFragile && !center.SupportsFragileItems) return false;
        if (package.IsRestricted && !center.SupportsRestrictedItems) return false;
        return true;
    }

    private static int CalculateCapacityUnits(PackageProfileDto package)
    {
        var chargeableWeight = Math.Max(package.WeightKg, package.CubicWeightKg);
        var handlingFactor = package.IsFragile ? 1.5m : 1m;
        return Math.Max(1, (int)Math.Ceiling(chargeableWeight * handlingFactor));
    }

    private static int CalculateScore(int coveragePriority, decimal utilizationPercentage, DateOnly processingDate, DateTimeOffset requestedAt)
    {
        var requestedDate = DateOnly.FromDateTime(requestedAt.UtcDateTime);
        var processingDelay = processingDate.DayNumber - requestedDate.DayNumber;
        return coveragePriority * 100 + (int)utilizationPercentage + processingDelay * 200;
    }

    private static long NormalizePostalCode(string postalCode)
    {
        var digits = new string(postalCode.Where(char.IsDigit).ToArray());
        if (digits.Length != 8 || !long.TryParse(digits, out var value)) throw new ArgumentException("Invalid postal code");
        return value;
    }

    private static void Validate(SearchCandidatesRequest request)
    {
        if (request.SellerId == Guid.Empty) throw new ArgumentException("SellerId is required");
        if (string.IsNullOrWhiteSpace(request.DestinationPostalCode)) throw new ArgumentException("DestinationPostalCode is required");
        if (request.Package.WeightKg <= 0) throw new ArgumentException("Weight must be positive");
        if (request.Package.CubicWeightKg < 0) throw new ArgumentException("Cubic weight cannot be negative");
    }
}
