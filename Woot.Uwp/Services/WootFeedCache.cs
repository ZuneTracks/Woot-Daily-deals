using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Woot.Uwp.Models;
using Windows.Data.Json;
using Windows.Storage;

namespace Woot.Uwp.Services
{
    /// <summary>
    /// The deals Woot last returned for a feed, as they were read back from disk.
    /// </summary>
    public sealed class WootCachedFeed
    {
        public IList<WootDeal> Deals { get; set; }

        /// <summary>
        /// When these deals were saved, used to tell the user how old they are.
        /// </summary>
        public DateTimeOffset SavedUtc { get; set; }
    }

    /// <summary>
    /// Keeps the last deals seen for each feed in local storage.
    /// <para>
    /// Without this the app has nothing at all to show until the network answers,
    /// which on a phone means a blank list on every cold start and an error and
    /// nothing else when there is no signal. Deals are only a cache, never the
    /// truth: a fetch is still attempted every time, and whatever it returns
    /// replaces what was saved.
    /// </para>
    /// </summary>
    public static class WootFeedCache
    {
        private const string FolderName = "feeds";
        private const string SavedKey = "SavedUtc";
        private const string DealsKey = "Deals";

        public static async Task SaveAsync(string feedName, IList<WootDeal> deals)
        {
            if (string.IsNullOrWhiteSpace(feedName) || deals == null)
                return;

            var text = Serialize(deals, DateTimeOffset.UtcNow);
            try
            {
                var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                    FolderName, CreationCollisionOption.OpenIfExists);
                var file = await folder.CreateFileAsync(FileNameFor(feedName), CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, text);
            }
            catch (Exception)
            {
                // The cache is an optimisation. If the disk refuses it the app still
                // works from the network, so a failure here must not reach the user.
            }
        }

        public static async Task<WootCachedFeed> LoadAsync(string feedName)
        {
            if (string.IsNullOrWhiteSpace(feedName))
                return null;

            string text;
            try
            {
                var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                    FolderName, CreationCollisionOption.OpenIfExists);
                var file = await folder.GetFileAsync(FileNameFor(feedName));
                text = await FileIO.ReadTextAsync(file);
            }
            catch (Exception)
            {
                // Nothing saved yet, or it is unreadable. Either way there is no cache.
                return null;
            }

            return Deserialize(text);
        }

        private static string Serialize(IList<WootDeal> deals, DateTimeOffset savedUtc)
        {
            var document = new JsonObject();
            document.SetNamedValue(SavedKey, JsonValue.CreateStringValue(
                savedUtc.ToString("o", CultureInfo.InvariantCulture)));
            var items = new JsonArray();
            foreach (var deal in deals)
            {
                if (deal != null)
                    items.Add(ToJson(deal));
            }
            document.SetNamedValue(DealsKey, items);
            return document.Stringify();
        }

        private static WootCachedFeed Deserialize(string text)
        {
            JsonValue root;
            if (!JsonValue.TryParse(text, out root) || root.ValueType != JsonValueType.Object)
                return null;

            var document = root.GetObject();
            IJsonValue savedValue;
            DateTimeOffset saved;
            if (!document.TryGetValue(SavedKey, out savedValue) ||
                savedValue.ValueType != JsonValueType.String ||
                !DateTimeOffset.TryParse(savedValue.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out saved))
                return null;

            IJsonValue dealsValue;
            if (!document.TryGetValue(DealsKey, out dealsValue) || dealsValue.ValueType != JsonValueType.Array)
                return null;

            var deals = new List<WootDeal>();
            foreach (var item in dealsValue.GetArray())
            {
                if (item.ValueType == JsonValueType.Object)
                    deals.Add(FromJson(item.GetObject()));
            }

            return new WootCachedFeed { Deals = deals, SavedUtc = saved };
        }

        /// <summary>
        /// A short description of how long ago the cache was written, for the status
        /// line. Deliberately vague: the exact minute does not help anyone decide
        /// whether a deal is still live.
        /// </summary>
        public static string DescribeAge(DateTimeOffset savedUtc)
        {
            var age = DateTimeOffset.UtcNow - savedUtc;
            if (age < TimeSpan.Zero)
                return "just now";
            if (age < TimeSpan.FromMinutes(2))
                return "just now";
            if (age < TimeSpan.FromHours(1))
                return (int)age.TotalMinutes + " minutes ago";
            if (age < TimeSpan.FromHours(2))
                return "an hour ago";
            if (age < TimeSpan.FromDays(1))
                return (int)age.TotalHours + " hours ago";
            if (age < TimeSpan.FromDays(2))
                return "yesterday";
            return (int)age.TotalDays + " days ago";
        }

        private static JsonObject ToJson(WootDeal deal)
        {
            var obj = new JsonObject();
            SetString(obj, "Title", deal.Title);
            SetString(obj, "OfferId", deal.OfferId);
            SetString(obj, "Subtitle", deal.Subtitle);
            SetString(obj, "SalePrice", deal.SalePrice);
            SetString(obj, "ListPrice", deal.ListPrice);
            SetString(obj, "ImageUrl", deal.ImageUrl);
            SetString(obj, "OfferUrl", deal.OfferUrl);
            obj.SetNamedValue("IsSoldOut", JsonValue.CreateBooleanValue(deal.IsSoldOut));
            obj.SetNamedValue("IsFeatured", JsonValue.CreateBooleanValue(deal.IsFeatured));
            obj.SetNamedValue("FeedOrder", JsonValue.CreateNumberValue(deal.FeedOrder));
            SetNumber(obj, "SalePriceValue", deal.SalePriceValue);
            SetNumber(obj, "ListPriceValue", deal.ListPriceValue);
            SetDate(obj, "StartDate", deal.StartDate);
            SetDate(obj, "EndDate", deal.EndDate);

            var categories = new JsonArray();
            if (deal.Categories != null)
            {
                foreach (var category in deal.Categories)
                {
                    if (category != null)
                        categories.Add(JsonValue.CreateStringValue(category));
                }
            }
            obj.SetNamedValue("Categories", categories);
            return obj;
        }

        private static WootDeal FromJson(JsonObject obj)
        {
            return new WootDeal
            {
                Title = GetString(obj, "Title") ?? "Untitled deal",
                OfferId = GetString(obj, "OfferId"),
                Subtitle = GetString(obj, "Subtitle"),
                SalePrice = GetString(obj, "SalePrice"),
                ListPrice = GetString(obj, "ListPrice"),
                ImageUrl = GetString(obj, "ImageUrl"),
                OfferUrl = GetString(obj, "OfferUrl"),
                IsSoldOut = GetBool(obj, "IsSoldOut"),
                IsFeatured = GetBool(obj, "IsFeatured"),
                FeedOrder = (int)GetNumber(obj, "FeedOrder").GetValueOrDefault(),
                SalePriceValue = GetNumber(obj, "SalePriceValue"),
                ListPriceValue = GetNumber(obj, "ListPriceValue"),
                StartDate = GetDate(obj, "StartDate"),
                EndDate = GetDate(obj, "EndDate"),
                Categories = GetStringList(obj, "Categories")
            };
        }

        private static void SetString(JsonObject obj, string name, string value)
        {
            obj.SetNamedValue(name, value == null
                ? JsonValue.CreateNullValue()
                : JsonValue.CreateStringValue(value));
        }

        private static void SetNumber(JsonObject obj, string name, double? value)
        {
            obj.SetNamedValue(name, value.HasValue
                ? JsonValue.CreateNumberValue(value.Value)
                : JsonValue.CreateNullValue());
        }

        private static void SetDate(JsonObject obj, string name, DateTimeOffset? value)
        {
            obj.SetNamedValue(name, value.HasValue
                ? JsonValue.CreateStringValue(value.Value.ToString("o", CultureInfo.InvariantCulture))
                : JsonValue.CreateNullValue());
        }

        private static string GetString(JsonObject obj, string name)
        {
            IJsonValue value;
            if (!obj.TryGetValue(name, out value) || value.ValueType != JsonValueType.String)
                return null;
            return value.GetString();
        }

        private static bool GetBool(JsonObject obj, string name)
        {
            IJsonValue value;
            return obj.TryGetValue(name, out value) && value.ValueType == JsonValueType.Boolean && value.GetBoolean();
        }

        private static double? GetNumber(JsonObject obj, string name)
        {
            IJsonValue value;
            if (!obj.TryGetValue(name, out value) || value.ValueType != JsonValueType.Number)
                return null;
            return value.GetNumber();
        }

        private static DateTimeOffset? GetDate(JsonObject obj, string name)
        {
            var text = GetString(obj, name);
            DateTimeOffset parsed;
            if (text == null || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out parsed))
                return null;
            return parsed;
        }

        private static IList<string> GetStringList(JsonObject obj, string name)
        {
            IJsonValue value;
            if (!obj.TryGetValue(name, out value) || value.ValueType != JsonValueType.Array)
                return null;
            var items = new List<string>();
            foreach (var entry in value.GetArray())
            {
                if (entry.ValueType == JsonValueType.String)
                    items.Add(entry.GetString());
            }
            return items;
        }

        /// <summary>
        /// Feed names reach the file system, and one of them ("Woot-Off") is not a
        /// plain word, so anything that is not a letter or digit is replaced rather
        /// than trusted.
        /// </summary>
        private static string FileNameFor(string feedName)
        {
            var builder = new StringBuilder(feedName.Length + 5);
            foreach (var character in feedName)
                builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '_');
            return builder + ".json";
        }
    }
}
