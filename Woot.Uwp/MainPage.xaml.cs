using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Woot.Uwp.Models;
using Woot.Uwp.Services;
using Woot.Uwp.Views;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Woot.Uwp
{
    public sealed partial class MainPage : Page
    {
        private const string ApiKeySetting = "WootApiKey";
        private const string StartCategorySetting = "WootStartCategory";
        private static readonly TimeSpan FeedRefreshInterval = TimeSpan.FromMinutes(15);
        private readonly WootApiClient apiClient = new WootApiClient();
        private readonly HashSet<int> loadingFeeds = new HashSet<int>();
        private readonly DispatcherTimer refreshTimer;
        private bool hasLoadedOnce;
        private readonly string[] feedNames = { "Featured", "All", "Clearance", "Computers", "Electronics", "Home", "Gourmet", "Shirts", "Sports", "Tools", "Woot-Off" };

        public ObservableCollection<WootFeedViewModel> Feeds { get; private set; }

        public MainPage()
        {
            InitializeComponent();
            Feeds = new ObservableCollection<WootFeedViewModel>();
            foreach (var name in feedNames)
                Feeds.Add(new WootFeedViewModel(name));
            DataContext = this;
            RootGrid.RequestedTheme = IsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light;
            for (var index = 0; index < FeedPivot.Items.Count; index++)
                ((PivotItem)FeedPivot.Items[index]).Header = CreateCategoryHeader(feedNames[index], index == 0);
            var savedCategory = ApplicationData.Current.LocalSettings.Values[StartCategorySetting];
            var categoryIndex = savedCategory is int ? (int)savedCategory : 0;
            FeedPivot.SelectedIndex = categoryIndex >= 0 && categoryIndex < feedNames.Length ? categoryIndex : 0;
            refreshTimer = new DispatcherTimer { Interval = FeedRefreshInterval };
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadSelectedFeedAsync(true);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            hasLoadedOnce = true;
            _ = LoadSelectedFeedAsync(false);
        }

        private async void FeedPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!hasLoadedOnce || FeedPivot.SelectedIndex < 0 || FeedPivot.SelectedIndex >= Feeds.Count)
                return;
            UpdateCategoryHeaders(FeedPivot.SelectedIndex);
            await LoadFeedAsync(FeedPivot.SelectedIndex, false);
        }

        private void UpdateCategoryHeaders(int selectedIndex)
        {
            for (var index = 0; index < FeedPivot.Items.Count; index++)
            {
                var pivotItem = FeedPivot.Items[index] as PivotItem;
                var header = pivotItem == null ? null : pivotItem.Header as StackPanel;
                if (header != null && header.Children.Count > 1)
                    ((Border)header.Children[1]).Visibility = index == selectedIndex ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private static StackPanel CreateCategoryHeader(string title, bool isSelected)
        {
            var header = new StackPanel { Margin = new Thickness(16, 0, 16, 0) };
            header.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                Foreground = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["PhonePrimaryTextBrush"],
                VerticalAlignment = VerticalAlignment.Center
            });
            header.Children.Add(new Border
            {
                Height = 3,
                Background = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["PhoneAccentBrush"],
                Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed
            });
            return header;
        }

        private async Task LoadSelectedFeedAsync(bool forceRefresh = false)
        {
            var index = FeedPivot.SelectedIndex < 0 ? 0 : FeedPivot.SelectedIndex;
            if (index >= Feeds.Count)
                return;
            await LoadFeedAsync(index, forceRefresh);
        }

        private async void RefreshTimer_Tick(object sender, object e)
        {
            for (var index = 0; index < Feeds.Count; index++)
            {
                if (Feeds[index].IsLoaded)
                    await LoadFeedAsync(index, true);
            }
        }

        private async Task LoadFeedAsync(int index, bool forceRefresh)
        {
            if (index < 0 || index >= Feeds.Count || loadingFeeds.Contains(index))
                return;
            var feed = Feeds[index];
            if (feed.IsLoaded && !forceRefresh)
                return;

            var key = WootApiKeyProvider.Get();
            if (string.IsNullOrWhiteSpace(key))
            {
                feed.StatusText = "Deals are unavailable right now. Please try again later.";
                return;
            }

            loadingFeeds.Add(index);
            using (var loadCancellation = new CancellationTokenSource())
            {
            feed.StatusText = "Loading " + feed.Name + "...";
            try
            {
                var deals = await apiClient.GetFeedAsync(feed.Name, key, loadCancellation.Token);
                feed.Deals.Clear();
                foreach (var deal in deals)
                    feed.Deals.Add(deal);
                feed.IsLoaded = true;
                feed.StatusText = deals.Count == 0 ? "No deals were returned." : deals.Count + " deals";
                if (index == 0)
                    WootTileService.Update(null, feed.Deals);
            }
            catch (OperationCanceledException)
            {
                feed.StatusText = "Loading canceled.";
            }
            catch (HttpRequestException ex)
            {
                feed.StatusText = "Unable to load " + feed.Name + ": " + ex.Message;
            }
            catch (FormatException ex)
            {
                feed.StatusText = "Unable to read " + feed.Name + ": " + ex.Message;
            }
            catch (InvalidOperationException ex)
            {
                feed.StatusText = ex.Message;
            }
            finally
            {
                loadingFeeds.Remove(index);
            }
            }
        }

        private void ShowSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        private void Deal_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            var border = sender as FrameworkElement;
            var deal = border == null ? null : border.DataContext as WootDeal;
            if (deal == null)
                return;
            WootTileService.Update(deal, Feeds.Count == 0 ? null : Feeds[0].Deals);
            Frame.Navigate(typeof(OfferDetailsPage), deal);
        }

        private void ShowAboutButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null)
                return;
            var flyout = new Flyout { Content = CreateAboutContent() };
            flyout.ShowAt(button);
        }

        private static bool IsDarkTheme()
        {
            var value = ApplicationData.Current.LocalSettings.Values["WootDarkTheme"];
            return value == null || (value is bool && (bool)value);
        }

        private static StackPanel CreateAboutContent()
        {
            var content = new StackPanel { Width = 260, Padding = new Thickness(8) };
            content.Children.Add(new TextBlock { Text = "Woot! UWP", FontSize = 24 });
            content.Children.Add(new TextBlock { Text = "Woot! UWP is a Universal Windows app for Windows 10 Mobile", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
            content.Children.Add(new TextBlock { Text = "This app is not affiliated with Woot!, Amazon, or any of their affiliates.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
            var version = Package.Current.Id.Version;
            content.Children.Add(new TextBlock { Text = string.Format("Build {0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision), Margin = new Thickness(0, 10, 0, 0) });
            content.Children.Add(new TextBlock { Text = "Developed by ZuneTracks" });
            content.Children.Add(new HyperlinkButton
            {
                Content = "github.com/ZuneTracks",
                NavigateUri = new Uri("https://github.com/ZuneTracks"),
                Margin = new Thickness(0, 4, 0, 0)
            });
            return content;
        }

        private void DealImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var image = sender as Image;
            if (image != null)
            {
                image.Source = null;
                image.Opacity = 0.45;
            }
        }
    }
}
