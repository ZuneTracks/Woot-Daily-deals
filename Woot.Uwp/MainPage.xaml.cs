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
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Navigation;

namespace Woot.Uwp
{
    public sealed partial class MainPage : Page
    {
        private const string ApiKeySetting = "WootApiKey";
        private const string StartCategorySetting = "WootStartCategory";
        private const string SortSettingPrefix = "WootSort_";
        private static readonly TimeSpan FeedRefreshInterval = TimeSpan.FromMinutes(15);
        private readonly WootApiClient apiClient = new WootApiClient();
        private readonly HashSet<int> loadingFeeds = new HashSet<int>();
        private readonly DispatcherTimer refreshTimer;
        private bool hasLoadedOnce;
        private bool suppressSortSelection;
        private bool suppressSubcategorySelection;
        private const string AllSubcategoriesLabel = "All subcategories";
        private readonly string[] feedNames = { "Featured", "All", "Clearance", "Computers", "Electronics", "Home", "Gourmet", "Shirts", "Sports", "Tools", "Woot-Off" };

        public ObservableCollection<WootFeedViewModel> Feeds { get; private set; }

        public MainPage()
        {
            InitializeComponent();
            Feeds = new ObservableCollection<WootFeedViewModel>();
            foreach (var name in feedNames)
                Feeds.Add(new WootFeedViewModel(name) { Sort = LoadSort(name) });
            DataContext = this;
            RootGrid.RequestedTheme = IsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light;
            for (var index = 0; index < FeedPivot.Items.Count; index++)
                ((PivotItem)FeedPivot.Items[index]).Header = CreateCategoryHeader(feedNames[index], index == 0);
            SortSelector.ItemsSource = WootDealSortOption.All;
            var savedCategory = ApplicationData.Current.LocalSettings.Values[StartCategorySetting];
            var categoryIndex = savedCategory is int ? (int)savedCategory : 0;
            FeedPivot.SelectedIndex = categoryIndex >= 0 && categoryIndex < feedNames.Length ? categoryIndex : 0;
            UpdateCategoryHeaders(FeedPivot.SelectedIndex);
            SyncSortSelector();
            SyncSubcategorySelector();
            refreshTimer = new DispatcherTimer { Interval = FeedRefreshInterval };
            refreshTimer.Tick += RefreshTimer_Tick;
        }

        private static WootDealSort LoadSort(string feedName)
        {
            var stored = ApplicationData.Current.LocalSettings.Values[SortSettingPrefix + feedName];
            if (stored is int && WootDealSortOption.IsDefined((int)stored))
                return (WootDealSort)(int)stored;
            return WootDealSort.Default;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadSelectedFeedAsync(true);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            hasLoadedOnce = true;
            UpdateCategoryHeaders(FeedPivot.SelectedIndex);
            SyncSortSelector();
            SyncSubcategorySelector();
            refreshTimer.Start();
            _ = LoadFeedIfStaleAsync(FeedPivot.SelectedIndex < 0 ? 0 : FeedPivot.SelectedIndex);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            // Nothing on this page is visible once it is left, so stop spending
            // requests on it until the user comes back.
            refreshTimer.Stop();
            base.OnNavigatedFrom(e);
        }

        private async void FeedPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FeedPivot.SelectedIndex < 0 || FeedPivot.SelectedIndex >= Feeds.Count)
                return;
            UpdateCategoryHeaders(FeedPivot.SelectedIndex);
            SyncSortSelector();
            SyncSubcategorySelector();
            if (!hasLoadedOnce)
                return;
            await LoadFeedIfStaleAsync(FeedPivot.SelectedIndex);
        }

        private void SyncSortSelector()
        {
            var index = FeedPivot.SelectedIndex;
            if (index < 0 || index >= Feeds.Count)
                return;
            suppressSortSelection = true;
            SortSelector.SelectedIndex = WootDealSortOption.IndexOf(Feeds[index].Sort);
            suppressSortSelection = false;
        }

        /// <summary>
        /// Rebuilds the subcategory choices for the selected feed. The selector stays
        /// hidden for feeds Woot returns no subcategories for, so it never shows up as
        /// an empty control.
        /// </summary>
        private void SyncSubcategorySelector()
        {
            var index = FeedPivot.SelectedIndex;
            if (index < 0 || index >= Feeds.Count)
                return;
            var feed = Feeds[index];

            suppressSubcategorySelection = true;
            var choices = new List<string> { AllSubcategoriesLabel };
            foreach (var name in feed.Subcategories)
                choices.Add(name);
            SubcategorySelector.ItemsSource = choices;

            var selected = WootDealCategories.IndexOf(feed.Subcategories, feed.Subcategory);
            SubcategorySelector.SelectedIndex = selected < 0 ? 0 : selected + 1;
            SubcategorySelector.Visibility = feed.Subcategories.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            suppressSubcategorySelection = false;
        }

        private void SubcategorySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressSubcategorySelection)
                return;
            var index = FeedPivot.SelectedIndex;
            if (index < 0 || index >= Feeds.Count)
                return;
            var feed = Feeds[index];
            var selected = SubcategorySelector.SelectedItem as string;
            feed.Subcategory = selected == AllSubcategoriesLabel ? null : selected;
            UpdateFeedStatus(feed);
        }

        private void SortSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressSortSelection)
                return;
            var index = FeedPivot.SelectedIndex;
            var option = SortSelector.SelectedItem as WootDealSortOption;
            if (option == null || index < 0 || index >= Feeds.Count)
                return;
            var feed = Feeds[index];
            feed.Sort = option.Sort;
            ApplicationData.Current.LocalSettings.Values[SortSettingPrefix + feed.Name] = (int)option.Sort;
            UpdateFeedStatus(feed);
        }

        /// <summary>
        /// Keeps the feed caption in step with the deal count, the active filter and
        /// the active sort.
        /// </summary>
        private static void UpdateFeedStatus(WootFeedViewModel feed)
        {
            if (!feed.IsLoaded && !feed.CachedAt.HasValue)
                return;
            var total = feed.FeedOrderDeals.Count;
            if (total == 0)
            {
                // Woot answers for every category it documents, but some of them are
                // routinely empty: a Woot-Off only runs occasionally, and Gourmet has
                // no deals of its own any more.
                feed.StatusText = string.Equals(feed.Name, "Woot-Off", StringComparison.OrdinalIgnoreCase)
                    ? "No Woot-Off is running right now. Woot-Offs are occasional events, so this list is usually empty."
                    : "Woot is not listing any " + feed.Name + " deals right now.";
                return;
            }

            string status;
            if (feed.IsFiltered && feed.VisibleCount == 0)
                status = "No " + feed.Subcategory.ToLowerInvariant() + " deals in this category right now.";
            else if (feed.IsFiltered)
                status = feed.VisibleCount + " of " + total + " deals - " + feed.Subcategory.ToLowerInvariant();
            else
                status = total + " deals";

            if (feed.Sort != WootDealSort.Default && feed.VisibleCount > 0)
            {
                status += " - " + WootDealSortOption.All[WootDealSortOption.IndexOf(feed.Sort)].Label.ToLowerInvariant();
                if (!feed.IsSortSupported)
                    status += " (not available for this feed)";
            }

            // These deals came off the disk, not from Woot, so say so rather than let
            // them pass for current.
            if (feed.CachedAt.HasValue)
                status += " - saved " + WootFeedCache.DescribeAge(feed.CachedAt.Value);

            feed.StatusText = status;
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
            // Only the category on screen is refreshed. Refreshing every loaded feed
            // cost one request per feed per tick, which across eleven categories at
            // four ticks an hour exceeded Woot's 1000-request daily limit by itself.
            await LoadFeedAsync(FeedPivot.SelectedIndex, true);

            // The live tile is built from the Featured feed and nothing else updates
            // it, so keep that one current even while another category is on screen.
            if (FeedPivot.SelectedIndex != 0 && Feeds.Count > 0 && Feeds[0].IsLoaded)
                await LoadFeedAsync(0, true);
        }

        /// <summary>
        /// Loads a feed, re-fetching it only once its deals have aged past the refresh
        /// interval so that revisiting a category costs nothing.
        /// </summary>
        private async Task LoadFeedIfStaleAsync(int index)
        {
            if (index < 0 || index >= Feeds.Count)
                return;
            await LoadFeedAsync(index, Feeds[index].IsStale(FeedRefreshInterval));
        }

        /// <summary>
        /// Puts the deals saved from a previous session on screen so a cold start, or
        /// a phone with no signal, has something to show. They are explicitly not
        /// marked loaded, so the feed is still fetched and replaced the moment Woot
        /// answers.
        /// </summary>
        private static async Task ShowCachedDealsAsync(WootFeedViewModel feed)
        {
            if (feed.IsLoaded || feed.FeedOrderDeals.Count > 0)
                return;

            var cached = await WootFeedCache.LoadAsync(feed.Name);

            // The request may well have finished while the file was being read, in
            // which case the live deals are already on screen and must not be
            // replaced by older ones.
            if (cached == null || cached.Deals.Count == 0 || feed.IsLoaded || feed.FeedOrderDeals.Count > 0)
                return;

            feed.SetDeals(cached.Deals);
            feed.CachedAt = cached.SavedUtc;
        }

        /// <summary>
        /// What to say when a feed will not load. Cached deals are far more use than
        /// an error, so when there are some the failure is reported as staleness.
        /// </summary>
        private static string DescribeFailure(WootFeedViewModel feed, string error)
        {
            if (!feed.CachedAt.HasValue)
                return error;
            return "Can't reach Woot. These are the deals saved " +
                WootFeedCache.DescribeAge(feed.CachedAt.Value) + ".";
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

            // Something to look at while the request is in flight, and the only thing
            // there will be if it never arrives.
            await ShowCachedDealsAsync(feed);
            if (feed.CachedAt.HasValue)
            {
                if (index == FeedPivot.SelectedIndex)
                    SyncSubcategorySelector();
                feed.StatusText = "Deals saved " + WootFeedCache.DescribeAge(feed.CachedAt.Value) + ". Checking Woot...";
            }

            try
            {
                var deals = await apiClient.GetFeedAsync(feed.Name, key, loadCancellation.Token);
                feed.SetDeals(deals);
                feed.CachedAt = null;
                feed.IsLoaded = true;
                feed.LastLoaded = DateTimeOffset.UtcNow;
                if (index == FeedPivot.SelectedIndex)
                    SyncSubcategorySelector();
                UpdateFeedStatus(feed);
                if (index == 0)
                    WootTileService.Update(null, feed.FeedOrderDeals);
                await WootFeedCache.SaveAsync(feed.Name, deals);
            }
            catch (OperationCanceledException)
            {
                feed.StatusText = "Loading canceled.";
            }
            catch (HttpRequestException ex)
            {
                feed.StatusText = DescribeFailure(feed, "Unable to load " + feed.Name + ": " + ex.Message);
            }
            catch (FormatException ex)
            {
                feed.StatusText = DescribeFailure(feed, "Unable to read " + feed.Name + ": " + ex.Message);
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
            WootTileService.Update(deal, Feeds.Count == 0 ? null : Feeds[0].FeedOrderDeals);
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
            content.Children.Add(new TextBlock { Text = "Daily Deals for Woot!", FontSize = 24 });
            var version = Package.Current.Id.Version;
            content.Children.Add(new TextBlock { Text = string.Format("Build {0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision), Margin = new Thickness(0, 10, 0, 0) });
            content.Children.Add(new TextBlock { Text = "Daily Deals for Woot! is a Universal Windows app for Windows 10 Mobile/Desktop. This app allows you to browse the current deals found on Woot!", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
            content.Children.Add(new TextBlock { Text = "This app is not affiliated with Woot!, Amazon, or any of their affiliates.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
            var developer = new TextBlock { Margin = new Thickness(0, 10, 0, 0) };
            developer.Inlines.Add(new Run { Text = "Developed by " });
            var developerLink = new Hyperlink { NavigateUri = new Uri("https://github.com/ZuneTracks/Woot-Daily-deals") };
            developerLink.Inlines.Add(new Run { Text = "ZuneTracks" });
            developer.Inlines.Add(developerLink);
            content.Children.Add(developer);
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
