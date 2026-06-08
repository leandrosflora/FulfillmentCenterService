namespace FulfillmentCenterService.Domain;

public sealed class CenterCoverage
{
    public Guid Id { get; private set; }
    public Guid FulfillmentCenterId { get; private set; }
    public long PostalCodeFrom { get; private set; }
    public long PostalCodeTo { get; private set; }
    public FulfillmentMode Mode { get; private set; }
    public int Priority { get; private set; }

    private CenterCoverage() { }

    public CenterCoverage(Guid fulfillmentCenterId, long postalCodeFrom, long postalCodeTo, FulfillmentMode mode, int priority)
    {
        if (fulfillmentCenterId == Guid.Empty) throw new ArgumentException("FulfillmentCenterId is required", nameof(fulfillmentCenterId));
        if (postalCodeFrom > postalCodeTo) throw new ArgumentException("Postal code range is invalid", nameof(postalCodeFrom));
        Id = Guid.NewGuid();
        FulfillmentCenterId = fulfillmentCenterId;
        PostalCodeFrom = postalCodeFrom;
        PostalCodeTo = postalCodeTo;
        Mode = mode;
        Priority = priority;
    }
}
