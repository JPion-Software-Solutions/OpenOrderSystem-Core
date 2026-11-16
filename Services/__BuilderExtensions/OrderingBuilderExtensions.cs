using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenOrderSystem.Core.Services.Catalog;
using OpenOrderSystem.Core.Services.Catalog.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.CartServices;
using OpenOrderSystem.Core.Services.Ordering.CartServices.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.OrderNumbers;
using OpenOrderSystem.Core.Services.Ordering.OrderNumbers.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.Placement;
using OpenOrderSystem.Core.Services.Ordering.Placement.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.Stages;
using OpenOrderSystem.Core.Services.Ordering.Stages.Interfaces;
using OpenOrderSystem.Core.Services.Ordering.Timekeeping;
using OpenOrderSystem.Core.Services.Ordering.Timekeeping.Interfaces;

namespace OpenOrderSystem.Core.Services.__BuilderExtensions;

/// <summary>
/// The catalog and ordering service group. Ordering processes every order, whatever channel it comes
/// from, and builds on the catalog, so <see cref="AddOosOrderingServices"/> includes it.
/// <see cref="AddOosCatalogServices"/> is separate only for boot modes that need product data without
/// order processing.
/// </summary>
/// <remarks>
/// Both need <see cref="DatabaseBuilderExtensions.AddOosDatabaseServices"/>. Ordering also needs
/// <see cref="ConfigurationBuilderExtensions.AddOosConfigurationServices"/>, and the event bus group
/// (<see cref="EventBusBuilderExtensions.AddOosEventBusServices"/>): order placement raises
/// <c>order.placed</c> through <see cref="EventBus.Interfaces.ISystemEventBus"/>. Neither is registered
/// here; a boot mode using ordering adds them itself.
/// </remarks>
public static class OrderingBuilderExtensions
{
    /// <summary>Registers the product catalog. Safe to call more than once.</summary>
    public static WebApplicationBuilder AddOosCatalogServices(this WebApplicationBuilder bob)
    {
        // One catalog per scope; readers and the manager share it.
        bob.Services.TryAddScoped<CatalogService>();
        bob.Services.TryAddScoped<ICatalogManager>(sp => sp.GetRequiredService<CatalogService>());
        bob.Services.TryAddScoped<IReadOnlyCatalog>(sp => sp.GetRequiredService<CatalogService>());

        return bob;
    }

    /// <summary>Registers carts, pickup timekeeping, order placement and order stages, plus the catalog.</summary>
    public static WebApplicationBuilder AddOosOrderingServices(this WebApplicationBuilder bob)
    {
        bob.AddOosCatalogServices();

        // Singletons: in-memory state shared across requests (cart cache, pickup slot holds).
        bob.Services.AddSingleton<ICartCacheService, CartCacheService>();
        bob.Services.AddSingleton<ITimekeepingService, TimekeepingService>();

        // Scoped: per-request business logic.
        bob.Services.AddScoped<ICartService, CartService>();
        bob.Services.AddScoped<IConfirmationNumberCodec, ConfirmationNumberCodec>();
        bob.Services.AddScoped<IOrderPlacementService, OrderPlacementService>();
        bob.Services.AddScoped<IOrderStageService, OrderStageService>();

        return bob;
    }
}
