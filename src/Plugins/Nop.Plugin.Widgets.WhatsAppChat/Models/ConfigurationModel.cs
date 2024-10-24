using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.WhatsAppChat.Models;

public record ConfigurationModel : BaseNopModel
{
    public string WhatsAppNumber { get; set; }
    public string TextMessage { get; set; }
}