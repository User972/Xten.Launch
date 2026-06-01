using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.Midtrans.Infrastructure;

/// <summary>
/// Registers the plugin's admin configuration route and the public webhook/return routes
/// </summary>
public class RouteProvider : IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        // Admin configuration page
        endpointRouteBuilder.MapControllerRoute(name: MidtransDefaults.ConfigurationRouteName,
            pattern: "Admin/PaymentMidtrans/Configure",
            defaults: new { controller = "PaymentMidtrans", action = "Configure", area = AreaNames.ADMIN });

        // Server-to-server payment notification (webhook) — set this URL in the Midtrans dashboard
        endpointRouteBuilder.MapControllerRoute(name: MidtransDefaults.NotifyRouteName,
            pattern: "Plugins/PaymentMidtrans/Notify",
            defaults: new { controller = "PaymentMidtransWebhook", action = "Notify" });

        // Customer return URL after completing payment on Midtrans (callbacks.finish)
        endpointRouteBuilder.MapControllerRoute(name: MidtransDefaults.ReturnRouteName,
            pattern: "Plugins/PaymentMidtrans/Return",
            defaults: new { controller = "PaymentMidtransWebhook", action = "Return" });
    }

    public int Priority => 0;
}
