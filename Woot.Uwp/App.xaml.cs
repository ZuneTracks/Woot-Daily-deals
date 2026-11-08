using System;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml;
using Woot.Uwp.Services;

namespace Woot.Uwp
{
    sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
        }

        public static void ApplyTheme()
        {
            var saved = ApplicationData.Current.LocalSettings.Values["WootDarkTheme"];
            var dark = saved == null || Convert.ToBoolean(saved);
            SetBrush("PhonePageBrush", dark ? "#181818" : "#F2F2F2");
            SetBrush("PhoneHeaderBrush", dark ? "#242424" : "#FFFFFF");
            SetBrush("PhoneCardBrush", dark ? "#292929" : "#FFFFFF");
            SetBrush("PhonePrimaryTextBrush", dark ? "#FFFFFF" : "#202020");
            SetBrush("PhoneMutedTextBrush", dark ? "#B8B8B8" : "#606060");
        }

        private static void SetBrush(string key, string hex)
        {
            var brush = Current.Resources[key] as SolidColorBrush;
            if (brush != null)
                brush.Color = Color.FromArgb(255,
                    Convert.ToByte(hex.Substring(1, 2), 16),
                    Convert.ToByte(hex.Substring(3, 2), 16),
                    Convert.ToByte(hex.Substring(5, 2), 16));
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            var rootFrame = Window.Current.Content as Windows.UI.Xaml.Controls.Frame;
            if (rootFrame == null)
            {
                rootFrame = new Windows.UI.Xaml.Controls.Frame();
                Window.Current.Content = rootFrame;
            }

            if (rootFrame.Content == null)
                rootFrame.Navigate(typeof(MainPage), e.Arguments);

            Window.Current.Activate();

            // Registration is awaited by nobody: the tile refreshing is not worth
            // delaying the first frame for.
            var ignored = WootBackgroundService.RegisterAsync();
        }

        /// <summary>
        /// The entry point for in-process background tasks. The tile refresh has no
        /// entry point of its own, so the platform delivers it here.
        /// </summary>
        protected override async void OnBackgroundActivated(BackgroundActivatedEventArgs args)
        {
            base.OnBackgroundActivated(args);

            // Without a deferral the task is considered finished the moment this
            // method returns, which for async work is before it has done anything.
            var deferral = args.TaskInstance.GetDeferral();
            try
            {
                var task = args.TaskInstance.Task;
                if (task != null && string.Equals(task.Name, WootBackgroundService.TileTaskName, StringComparison.Ordinal))
                    await WootBackgroundService.RunTileRefreshAsync();
            }
            catch (Exception)
            {
                // An in-process background task that throws takes the whole app down
                // with it. A tile that is briefly out of date is the better outcome.
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
        }
    }
}
