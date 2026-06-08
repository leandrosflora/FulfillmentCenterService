namespace FulfillmentCenterService.Domain;

public sealed class SellerCenterEnrollment
{
    public Guid Id { get; private set; }
    public Guid SellerId { get; private set; }
    public Guid FulfillmentCenterId { get; private set; }
    public FulfillmentMode Mode { get; private set; }
    public bool IsActive { get; private set; }

    private SellerCenterEnrollment() { }

    public SellerCenterEnrollment(Guid sellerId, Guid fulfillmentCenterId, FulfillmentMode mode, bool isActive = true)
    {
        if (sellerId == Guid.Empty) throw new ArgumentException("SellerId is required", nameof(sellerId));
        if (fulfillmentCenterId == Guid.Empty) throw new ArgumentException("FulfillmentCenterId is required", nameof(fulfillmentCenterId));
        Id = Guid.NewGuid();
        SellerId = sellerId;
        FulfillmentCenterId = fulfillmentCenterId;
        Mode = mode;
        IsActive = isActive;
    }
}
