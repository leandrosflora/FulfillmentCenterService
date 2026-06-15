using FulfillmentCenterService.Domain;

namespace FulfillmentCenterService.UnitTests;

public sealed class DomainTests
{
    [Fact]
    public void CapacityReservation_Create_generates_pending_reservation_with_contract_fields()
    {
        var orderId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var centerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var reservation = CapacityReservation.Create(orderId, centerId, new DateOnly(2026, 6, 14), FulfillmentMode.Fulfillment, 7, "idem-001");

        Assert.NotEqual(Guid.Empty, reservation.Id);
        Assert.Equal(orderId, reservation.OrderId);
        Assert.Equal(centerId, reservation.FulfillmentCenterId);
        Assert.Equal(CapacityReservationStatus.Pending, reservation.Status);
        Assert.Equal(7, reservation.ReservedCapacityUnits);
        Assert.True(reservation.ExpiresAt > reservation.CreatedAt);
    }

    [Fact]
    public void CapacityReservation_Release_rejects_confirmed_reservation_to_preserve_capacity_contract()
    {
        var reservation = CapacityReservation.Create(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 6, 14), FulfillmentMode.Fulfillment, 1, "idem-002");
        reservation.Confirm();

        var exception = Assert.Throws<InvalidOperationException>(reservation.Release);

        Assert.Contains("Confirmed capacity cannot be released", exception.Message);
    }

    [Fact]
    public void FulfillmentCenter_Supports_respects_status_package_limits_and_capabilities()
    {
        var center = new FulfillmentCenter("BR-SP01", "São Paulo 01", "SP", "UTC", 10m, 15m, supportsFragileItems: false, supportsRestrictedItems: true);

        Assert.True(center.Supports(new PackageProfile(5m, 8m, IsFragile: false, IsRestricted: true)));
        Assert.False(center.Supports(new PackageProfile(11m, 8m, IsFragile: false, IsRestricted: false)));
        Assert.False(center.Supports(new PackageProfile(5m, 8m, IsFragile: true, IsRestricted: false)));

        center.ChangeStatus(FulfillmentCenterStatus.Maintenance);

        Assert.False(center.Supports(new PackageProfile(5m, 8m, IsFragile: false, IsRestricted: false)));
    }

    [Fact]
    public void CapacitySlot_Reconfigure_updates_total_capacity_when_not_below_allocated_units()
    {
        var slot = new CapacitySlot(Guid.NewGuid(), new DateOnly(2026, 6, 14), FulfillmentMode.Fulfillment, 10);

        slot.Reconfigure(5);

        Assert.Equal(5, slot.TotalCapacityUnits);
    }
}
