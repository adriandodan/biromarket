using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Widgets.WhatsAppChat.Infrastructure;

public class RouteProvider : IRouteProvider
{
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(name: WhatsAppChatDefaults.ConfigurationRouteName,
            pattern: "Admin/WhatsAppChat/Configure",
            defaults: new { controller = "WhatsAppChat", action = "Configure" });
    }

    public int Priority => 0;
}