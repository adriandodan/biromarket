using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core.Domain.Cms;
using Nop.Plugin.Widgets.WhatsAppChat.Components;
using Nop.Plugin.Widgets.WhatsAppChat.Models;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.WhatsAppChat
{
    /// <summary>
    /// Represents the WhatsApp Chat plugin
    /// </summary>
    public class WhatsAppChatPlugin : BasePlugin, IWidgetPlugin
    {
        #region Fields

        protected readonly IActionContextAccessor _actionContextAccessor;
        protected readonly ILocalizationService _localizationService;
        protected readonly ISettingService _settingService;
        protected readonly IUrlHelperFactory _urlHelperFactory;
        protected readonly WidgetSettings _widgetSettings;

        #endregion

        #region Ctor

        public WhatsAppChatPlugin(IActionContextAccessor actionContextAccessor,
            ILocalizationService localizationService,
            ISettingService settingService,
            IUrlHelperFactory urlHelperFactory,
            WidgetSettings widgetSettings)
        {
            _actionContextAccessor = actionContextAccessor;
            _localizationService = localizationService;
            _settingService = settingService;
            _urlHelperFactory = urlHelperFactory;
            _widgetSettings = widgetSettings;
        }

        #endregion

        #region Methods

        public async Task<IList<string>> GetWidgetZonesAsync()
        {
            return new List<string> { PublicWidgetZones.Footer };
        }

        public override string GetConfigurationPageUrl()
        {
            return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext).RouteUrl(WhatsAppChatDefaults.ConfigurationRouteName);
        }

       
        public Type GetWidgetViewComponent(string widgetZone)
        {
            return typeof(WhatsAppChatViewComponent);
        }

        public override async Task InstallAsync()
        {
            await _settingService.SaveSettingAsync(new PublicInfoModel());
            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall plugin
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public override async Task UninstallAsync()
        {
            // Remove settings
            await _settingService.DeleteSettingAsync<PublicInfoModel>();

            await base.UninstallAsync();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether to hide this plugin on the widget list page in the admin area
        /// </summary>
        public bool HideInWidgetList => false;

        #endregion
    }
}
