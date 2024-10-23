using Nop.Core.Configuration;

namespace Nop.Plugin.Widgets.WhatsAppChat.Models;

public class PublicInfoModel : ISettings
{
    public string WhatsAppNumber { get; set; }
}