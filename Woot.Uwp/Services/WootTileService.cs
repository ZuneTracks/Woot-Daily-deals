using System;
using System.Collections.Generic;
using System.Linq;
using Woot.Uwp.Models;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.UI.Notifications;

namespace Woot.Uwp.Services
{
    public static class WootTileService
    {
        private const string LastViewedTitle = "WootLastViewedTitle";
        private const string LastViewedPrice = "WootLastViewedPrice";
        private const string NotificationSetting = "WootTileNotificationSetting";

        public static void Update(WootDeal lastViewed, IEnumerable<WootDeal> featuredDeals)
        {
            var settings = ApplicationData.Current.LocalSettings.Values;
            if (lastViewed != null)
            {
                settings[LastViewedTitle] = lastViewed.Title ?? string.Empty;
                settings[LastViewedPrice] = lastViewed.SalePrice ?? string.Empty;
            }

            var title = lastViewed == null ? settings[LastViewedTitle] as string : lastViewed.Title;
            var price = lastViewed == null ? settings[LastViewedPrice] as string : lastViewed.SalePrice;
            var featured = featuredDeals == null ? new List<WootDeal>() : featuredDeals.Take(2).ToList();

            var document = new XmlDocument();
            document.LoadXml(WootTileContent.Build(title, price, featured));
            var updater = TileUpdateManager.CreateTileUpdaterForApplication();

            // If notifications are switched off for the app the update is accepted
            // and then silently dropped, which looks exactly like a background task
            // that never ran. Recording it lets the settings page tell them apart.
            settings[NotificationSetting] = updater.Setting.ToString();
            updater.Update(new TileNotification(document));
        }

        /// <summary>
        /// What the system last told us about whether it is accepting tile updates
        /// for this app, or null if the tile has never been written.
        /// </summary>
        public static string LastNotificationSetting
        {
            get { return ApplicationData.Current.LocalSettings.Values[NotificationSetting] as string; }
        }
    }
}
