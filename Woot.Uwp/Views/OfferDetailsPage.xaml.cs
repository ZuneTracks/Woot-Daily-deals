using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Woot.Uwp.Models;
using Woot.Uwp.Services;

namespace Woot.Uwp.Views
{
    public sealed partial class OfferDetailsPage : Page
    {
        private readonly WootApiClient apiClient = new WootApiClient();
        private CancellationTokenSource detailsCancellation;
        private bool pageActive;
        private WootDeal deal;

        public OfferDetailsPage()
        {
            InitializeComponent();
            RootGrid.RequestedTheme = IsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            pageActive = true;
            detailsCancellation = new CancellationTokenSource();
            deal = e.Parameter as WootDeal;
            if (deal == null)
            {
                StatusText.Text = "The selected deal could not be opened.";
                return;
            }
            TitleText.Text = deal.Title ?? string.Empty;
            StatusText.Text = "Loading deal details...";
            try
            {
                var key = WootApiKeyProvider.Get();
                var details = await apiClient.GetOfferAsync(deal.OfferId, key, detailsCancellation.Token);
                if (!pageActive)
                    return;
                TitleText.Text = details.FullTitle ?? details.Title ?? string.Empty;
                SubtitleText.Text = details.Subtitle ?? details.Teaser ?? string.Empty;
                SalePriceText.Text = details.SalePrice ?? string.Empty;
                ListPriceText.Text = details.ListPrice ?? string.Empty;
                ContentText.Text = FirstContent(details) ?? string.Empty;
                if (details.PercentageRemaining.HasValue)
                {
                    RemainingText.Text = "Woot estimates about "
                        + Math.Round(details.PercentageRemaining.Value).ToString(CultureInfo.CurrentCulture)
                        + "% of stock remaining.";
                    RemainingText.Visibility = Visibility.Visible;
                }
                else
                {
                    RemainingText.Visibility = Visibility.Collapsed;
                }
                PurchaseButton.Visibility = string.IsNullOrEmpty(details.OfferUrl) ? Visibility.Collapsed : Visibility.Visible;
                if (!string.IsNullOrEmpty(details.ImageUrl))
                {
                    Uri imageUri;
                    if (Uri.TryCreate(details.ImageUrl, UriKind.Absolute, out imageUri))
                    {
                        try
                        {
                            // The image is drawn 220 pixels tall, so there is no reason to
                            // decode Woot's full-size photo into memory on a phone.
                            OfferImage.Source = new BitmapImage(imageUri)
                            {
                                DecodePixelType = DecodePixelType.Logical,
                                DecodePixelHeight = 220
                            };
                        }
                        catch (ArgumentException)
                        {
                            OfferImage.Source = null;
                        }
                    }
                }
                StatusText.Text = string.Empty;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (HttpRequestException ex)
            {
                StatusText.Text = "Unable to load deal details: " + ex.Message;
            }

            catch (FormatException ex)
            {
                StatusText.Text = "Unable to read deal details: " + ex.Message;
            }
            catch (InvalidOperationException ex)
            {
                StatusText.Text = ex.Message;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            pageActive = false;
            detailsCancellation?.Cancel();
            detailsCancellation?.Dispose();
            detailsCancellation = null;
            base.OnNavigatedFrom(e);
        }

        private static string FirstContent(WootOfferDetails details)
        {
            if (!string.IsNullOrWhiteSpace(details.Features))
                return details.Features;
            if (!string.IsNullOrWhiteSpace(details.WriteUp))
                return details.WriteUp;
            return details.Specs;
        }

        private async void PurchaseButton_Click(object sender, RoutedEventArgs e)
        {
            if (deal != null && !string.IsNullOrEmpty(deal.OfferUrl))
                await Launcher.LaunchUriAsync(new Uri(deal.OfferUrl));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        private void OfferImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            OfferImage.Source = null;
            OfferImage.Opacity = 0.45;
        }

        private static bool IsDarkTheme()
        {
            var value = ApplicationData.Current.LocalSettings.Values["WootDarkTheme"];
            return value == null || (value is bool && (bool)value);
        }
    }
}
