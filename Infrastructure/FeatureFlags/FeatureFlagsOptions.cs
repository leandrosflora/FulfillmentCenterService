namespace FulfillmentCenterService.Infrastructure.FeatureFlags;

public sealed class FeatureFlagsOptions
{
    public const string SectionName = "FeatureFlags";

    public bool UseMockRepositories { get; init; }
}
