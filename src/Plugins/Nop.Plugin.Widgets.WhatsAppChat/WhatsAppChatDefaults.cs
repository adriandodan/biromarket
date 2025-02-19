using Nop.Core;

namespace Nop.Plugin.Widgets.WhatsAppChat
{
    /// <summary>
    /// Represents plugin constants for WhatsApp Chat
    /// </summary>
    public static class WhatsAppChatDefaults
    {
        /// <summary>
        /// Gets the system name
        /// </summary>
        public static string SystemName => "Widgets.WhatsAppChat";

        /// <summary>
        /// Gets the user agent used to request third-party services
        /// </summary>
        public static string UserAgent => $"nopCommerce-{NopVersion.CURRENT_VERSION}";

        /// <summary>
        /// Gets the configuration route name
        /// </summary>
        public static string ConfigurationRouteName => "Plugin.Widgets.WhatsAppChat.Configure";

        /// <summary>
        /// Gets the name of the chat component
        /// </summary>
        public static string ComponentName => "WhatsAppChat";
    }
}