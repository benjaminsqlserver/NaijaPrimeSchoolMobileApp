using NaijaPrimeSchool.Application.Common;
using NaijaPrimeSchool.Application.Inventory;
using NaijaPrimeSchool.Application.Inventory.Dtos;
using NaijaPrimeSchool.Mobile.Contracts.Common;

namespace NaijaPrimeSchool.Mobile.Api.Endpoints;

// Storeroom: stock look-up and recording movements where the goods are.
// Deleting a movement is the web app's audit-safe soft delete, which also
// reverses its effect on QuantityOnHand.
internal static class StoreEndpoints
{
    public static void MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/store").WithTags("Store").RequireAuthorization(Policies.Store);

        group.MapGet("/summary", (IStockMovementService s, CancellationToken ct) => s.GetStoreSummaryAsync(ct));

        group.MapGet("/items", (QueryFilter<StoreItemFilter> query, IStoreItemService s, CancellationToken ct) =>
            s.ListAsync(query.Value, ct));
        group.MapGet("/items/{id:guid}", async (Guid id, IStoreItemService s, CancellationToken ct) =>
            ApiResults.OkOrNotFound(await s.GetByIdAsync(id, ct)));

        group.MapGet("/movements", (QueryFilter<StockMovementFilter> query, IStockMovementService s, CancellationToken ct) =>
            s.ListAsync(query.Value, ct));
        group.MapPost("/movements", async (RecordStockMovementRequest body, IStockMovementService s, ICurrentUser user, CancellationToken ct) =>
        {
            body.PerformedById ??= user.UserId;
            return (await s.RecordAsync(body, ct)).ToIdHttp();
        });
        group.MapDelete("/movements/{id:guid}", async (Guid id, IStockMovementService s, CancellationToken ct) =>
            (await s.SoftDeleteAsync(id, ct)).ToHttp());

        group.MapGet("/suppliers", (QueryFilter<SupplierFilter> query, ISupplierService s, CancellationToken ct) =>
            s.ListAsync(query.Value, ct));
    }
}
