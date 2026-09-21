using System;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Woot.Uwp.Views
{
    public sealed partial class SettingsPage : Page
    {
        private const string StartCategorySetting = "WootStartCategory";

        public SettingsPage()
        {
            InitializeComponent();
            var category = ApplicationData.Current.LocalSettings.Values[StartCategorySetting];
            var categoryIndex = category is int ? (int)category : 0;
            StartCategorySelector.SelectedIndex = categoryIndex >= 0 && categoryIndex < StartCategorySelector.Items.Count ? categoryIndex : 0;
            StatusText.Text = "Startup category: " + ((ComboBoxItem)StartCategorySelector.SelectedItem).Content + ".";
            var theme = ApplicationData.Current.LocalSettings.Values["WootDarkTheme"];
            DarkThemeSwitch.IsOn = theme == null || (theme is bool && (bool)theme);
            RootGrid.RequestedTheme = DarkThemeSwitch.IsOn ? ElementTheme.Dark : ElementTheme.Light;
            App.ApplyTheme();
        }

        private void StartCategorySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StartCategorySelector.SelectedIndex < 0)
                return;
            ApplicationData.Current.LocalSettings.Values[StartCategorySetting] = StartCategorySelector.SelectedIndex;
            StatusText.Text = "Startup category saved.";
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null)
                return;
            var flyout = new Flyout();
            var content = new StackPanel { Width = 260, Padding = new Windows.UI.Xaml.Thickness(8) };
            content.Children.Add(new TextBlock { Text = "Daily Deals for Woot!", FontSize = 24 });
            content.Children.Add(new TextBlock { Text = "Daily Deals for Woot! is a Universal Windows app for Windows 10 Mobile/Desktop. This app allows you to browse the current deals found on Woot!", TextWrapping = TextWrapping.Wrap, Margin = new Windows.UI.Xaml.Thickness(0, 10, 0, 0) });
            content.Children.Add(new TextBlock { Text = "This app is not affiliated with Woot!, Amazon, or any of their affiliates.", TextWrapping = TextWrapping.Wrap, Margin = new Windows.UI.Xaml.Thickness(0, 10, 0, 0) });
            var version = Package.Current.Id.Version;
            content.Children.Add(new TextBlock { Text = string.Format("Build {0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision), Margin = new Windows.UI.Xaml.Thickness(0, 10, 0, 0) });
            content.Children.Add(new TextBlock { Text = "Developed by ZuneTracks" });
            content.Children.Add(new HyperlinkButton
            {
                Content = "github.com/ZuneTracks",
                NavigateUri = new Uri("https://github.com/ZuneTracks"),
                Margin = new Windows.UI.Xaml.Thickness(0, 4, 0, 0)
            });
            flyout.Content = content;
            flyout.ShowAt(button);
        }

        private void DarkThemeSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["WootDarkTheme"] = DarkThemeSwitch.IsOn;
            RootGrid.RequestedTheme = DarkThemeSwitch.IsOn ? ElementTheme.Dark : ElementTheme.Light;
            App.ApplyTheme();
        }
    }
}
