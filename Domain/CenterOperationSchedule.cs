namespace FulfillmentCenterService.Domain;

public sealed class CenterOperationSchedule
{
    public Guid Id { get; private set; }
    public Guid FulfillmentCenterId { get; private set; }
    public DateOnly OperationDate { get; private set; }
    public FulfillmentMode Mode { get; private set; }
    public bool IsOpen { get; private set; }
    public TimeOnly OpeningTime { get; private set; }
    public TimeOnly CutoffTime { get; private set; }
    public TimeOnly ClosingTime { get; private set; }

    private CenterOperationSchedule() { }

    public CenterOperationSchedule(Guid fulfillmentCenterId, DateOnly operationDate, FulfillmentMode mode, bool isOpen, TimeOnly openingTime, TimeOnly cutoffTime, TimeOnly closingTime)
    {
        Id = Guid.NewGuid();
        FulfillmentCenterId = fulfillmentCenterId;
        OperationDate = operationDate;
        Mode = mode;
        IsOpen = isOpen;
        OpeningTime = openingTime;
        CutoffTime = cutoffTime;
        ClosingTime = closingTime;
    }
}
