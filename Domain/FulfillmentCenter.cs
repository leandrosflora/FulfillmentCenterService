namespace FulfillmentCenterService.Domain;

public sealed class FulfillmentCenter
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Region { get; private set; } = default!;
    public string TimeZoneId { get; private set; } = default!;
    public FulfillmentCenterStatus Status { get; private set; }
    public decimal MaximumWeightKg { get; private set; }
    public decimal MaximumCubicWeightKg { get; private set; }
    public bool SupportsFragileItems { get; private set; }
    public bool SupportsRestrictedItems { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private FulfillmentCenter() { }

    public FulfillmentCenter(string code, string name, string region, string timeZoneId, decimal maximumWeightKg, decimal maximumCubicWeightKg, bool supportsFragileItems, bool supportsRestrictedItems)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code is required", nameof(code));
        if (string.IsNullOrWhiteSpace(timeZoneId)) throw new ArgumentException("TimeZoneId is required", nameof(timeZoneId));
        if (maximumWeightKg <= 0) throw new ArgumentException("Maximum weight must be positive", nameof(maximumWeightKg));
        if (maximumCubicWeightKg <= 0) throw new ArgumentException("Maximum cubic weight must be positive", nameof(maximumCubicWeightKg));

        Id = Guid.NewGuid();
        Code = code;
        Name = name;
        Region = region;
        TimeZoneId = timeZoneId;
        MaximumWeightKg = maximumWeightKg;
        MaximumCubicWeightKg = maximumCubicWeightKg;
        SupportsFragileItems = supportsFragileItems;
        SupportsRestrictedItems = supportsRestrictedItems;
        Status = FulfillmentCenterStatus.Active;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool Supports(PackageProfile package)
    {
        if (Status != FulfillmentCenterStatus.Active) return false;
        if (package.WeightKg > MaximumWeightKg) return false;
        if (package.CubicWeightKg > MaximumCubicWeightKg) return false;
        if (package.IsFragile && !SupportsFragileItems) return false;
        if (package.IsRestricted && !SupportsRestrictedItems) return false;
        return true;
    }

    public void ChangeStatus(FulfillmentCenterStatus status)
    {
        Status = status;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public sealed record PackageProfile(decimal WeightKg, decimal CubicWeightKg, bool IsFragile, bool IsRestricted);
