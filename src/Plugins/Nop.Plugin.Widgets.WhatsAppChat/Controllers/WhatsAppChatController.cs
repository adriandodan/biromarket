using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.WhatsAppChat.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Widgets.WhatsAppChat.Controllers
{
    public class WhatsAppChatController(ISettingService settingService, ILocalizationService localizationService, INotificationService notificationService) : BasePluginController
    {
        [HttpGet]
        public async Task<IActionResult> Configure()
        {
            var settings = settingService.LoadSetting<PublicInfoModel>();
            var model = new ConfigurationModel
            {
                WhatsAppNumber = settings.WhatsAppNumber,
                TextMessage = settings.TextMessage
            };

            return View("~/Plugins/Widgets.WhatsAppChat/Views/Configure.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            var settings = settingService.LoadSetting<PublicInfoModel>();
            settings.WhatsAppNumber = model.WhatsAppNumber;
            settings.TextMessage = model.TextMessage;

            await settingService.SaveSettingAsync(settings);

            notificationService.SuccessNotification(await localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }
    }
}