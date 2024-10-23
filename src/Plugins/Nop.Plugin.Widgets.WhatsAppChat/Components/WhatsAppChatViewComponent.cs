using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.WhatsAppChat.Models;
using Nop.Services.Configuration;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Widgets.WhatsAppChat.Components
{
    [ViewComponent(Name = "WhatsAppChat")]
    public class WhatsAppChatViewComponent(ISettingService settingService) : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            var settings = settingService.LoadSetting<PublicInfoModel>();
            var model = new PublicInfoModel
            {
                WhatsAppNumber = settings.WhatsAppNumber
            };
            return View("~/Plugins/Widgets.WhatsAppChat/Views/PublicInfo.cshtml", model);
        }
    }
}