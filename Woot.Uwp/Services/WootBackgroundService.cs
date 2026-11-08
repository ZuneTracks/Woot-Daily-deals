using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.Storage;

namespace Woot.Uwp.Services
{
    /// <summary>
    /// Keeps the live tile current while the app is closed.
    /// <para>
    /// Nothing else updates the tile, so until now it showed whatever was on screen
    /// the last time the app happened to be open. The task refreshes the Featured
    /// feed on a timer and writes both the tile and the on-disk cache, so the tile
    /// stays honest and the next cold start opens on recent deals.
    /// </para>
    /// <para>
    /// This is an in-process task: no entry point is set on the builder, so it is
    /// delivered to <c>App.OnBackgroundActivated</c>. That needs no declaration in
    /// the package manifest and no separate Windows Runtime Component.
    /// </para>
    /// </summary>
    public static class WootBackgroundService
    {
        public const string TileTaskName = "WootTileRefresh";
        private const string RegisteredVersionSetting = "WootBackgroundVersion";

        /// <summary>
        /// Woot allows 1000 requests a day. One request every half hour is 48 of
        /// them, which leaves the foreground app the rest of the budget. The
        /// platform treats this as a floor rather than a promise and will run the
        /// task less often to save battery.
        /// </summary>
        private const uint IntervalMinutes = 30;

        public static async Task RegisterAsync()
        {
            try
            {
                var version = CurrentVersion();
                var existing = FindRegistration();

                // Background access has to be requested again after an update or the
                // registration made by the previous version stops firing, so the
                // version that registered is recorded and compared.
                if (existing != null &&
                    string.Equals(ApplicationData.Current.LocalSettings.Values[RegisteredVersionSetting] as string,
                        version, StringComparison.Ordinal))
                    return;

                if (existing != null)
                    existing.Unregister(true);

                BackgroundExecutionManager.RemoveAccess();
                var access = await BackgroundExecutionManager.RequestAccessAsync();
                if (access == BackgroundAccessStatus.DeniedBySystemPolicy ||
                    access == BackgroundAccessStatus.DeniedByUser ||
                    access == BackgroundAccessStatus.Unspecified)
                    return;

                var builder = new BackgroundTaskBuilder { Name = TileTaskName };
                builder.SetTrigger(new TimeTrigger(IntervalMinutes, false));

                // Waking only to fail on a dead connection wastes the user's battery.
                builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
                builder.Register();

                ApplicationData.Current.LocalSettings.Values[RegisteredVersionSetting] = version;
            }
            catch (Exception)
            {
                // Registration can fail for reasons entirely outside the app's control,
                // such as policy on a managed device. The tile is a convenience, so a
                // failure here is swallowed rather than allowed to break startup.
            }
        }

        /// <summary>
        /// Fetches the Featured feed and refreshes the tile and the cache from it.
        /// </summary>
        public static async Task RunTileRefreshAsync()
        {
            var key = WootApiKeyProvider.Get();
            if (string.IsNullOrWhiteSpace(key))
                return;

            var client = new WootApiClient();

            // Background work is killed outright if it overruns, so the request is
            // given a deadline well inside the time the platform allows.
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                var deals = await client.GetFeedAsync("Featured", key, timeout.Token);
                await WootFeedCache.SaveAsync("Featured", deals);
                WootTileService.Update(null, deals);
            }
        }

        private static IBackgroundTaskRegistration FindRegistration()
        {
            foreach (var registration in BackgroundTaskRegistration.AllTasks.Values)
            {
                if (string.Equals(registration.Name, TileTaskName, StringComparison.Ordinal))
                    return registration;
            }
            return null;
        }

        private static string CurrentVersion()
        {
            var version = Package.Current.Id.Version;
            return version.Major + "." + version.Minor + "." + version.Build + "." + version.Revision;
        }
    }
}
