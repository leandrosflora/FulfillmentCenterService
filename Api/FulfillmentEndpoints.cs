using FulfillmentCenterService.Application;
using FulfillmentCenterService.Contracts;

namespace FulfillmentCenterService.Api;

public static class FulfillmentEndpoints
{
    public static IEndpointRouteBuilder MapFulfillmentEndpoints(this IEndpointRouteBuilder app)
    {
        var centers = app.MapGroup("/fulfillment-centers").WithTags("Fulfillment Centers");

        centers.MapPost("/candidates/search", async (SearchCandidatesRequest request, CandidateSearchService service, CancellationToken cancellationToken) =>
        {
            var candidates = await service.SearchAsync(request, cancellationToken);
            return Results.Ok(candidates);
        });

        centers.MapPut("/{fulfillmentCenterId:guid}/capacity", async (Guid fulfillmentCenterId, ConfigureCapacityRequest request, CapacityManagementService service, CancellationToken cancellationToken) =>
        {
            await service.ConfigureCapacityAsync(fulfillmentCenterId, request, cancellationToken);
            return Results.NoContent();
        });

        centers.MapPatch("/{fulfillmentCenterId:guid}/status", async (Guid fulfillmentCenterId, ChangeFulfillmentCenterStatusRequest request, CapacityManagementService service, CancellationToken cancellationToken) =>
        {
            await service.ChangeStatusAsync(fulfillmentCenterId, request, cancellationToken);
            return Results.NoContent();
        });

        var reservations = app.MapGroup("/capacity-reservations").WithTags("Capacity Reservations");

        reservations.MapPost("/", async (CreateCapacityReservationRequest request, HttpContext context, CapacityReservationService service, CancellationToken cancellationToken) =>
        {
            var idempotencyKey = context.Request.Headers["Idempotency-Key"].ToString();
            if (string.IsNullOrWhiteSpace(idempotencyKey)) return Results.BadRequest(new { error = "Idempotency-Key header is required" });

            var response = await service.CreateAsync(request, idempotencyKey, cancellationToken);
            return Results.Created($"/capacity-reservations/{response.ReservationId}", response);
        });

        reservations.MapPost("/{reservationId:guid}/confirm", async (Guid reservationId, CapacityReservationService service, CancellationToken cancellationToken) =>
        {
            var response = await service.ConfirmAsync(reservationId, cancellationToken);
            return Results.Ok(response);
        });

        reservations.MapPost("/{reservationId:guid}/release", async (Guid reservationId, CapacityReservationService service, CancellationToken cancellationToken) =>
        {
            var response = await service.ReleaseAsync(reservationId, cancellationToken);
            return Results.Ok(response);
        });

        return app;
    }
}
