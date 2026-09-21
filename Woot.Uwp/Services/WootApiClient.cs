using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Woot.Uwp.Models;
using Windows.Data.Json;

namespace Woot.Uwp.Services
{
    public sealed class WootApiClient
    {
        private const string BaseUrl = "https://developer.woot.com/feed/";
        private const string OffersUrl = "https://developer.woot.com/getoffers";
        private readonly HttpClient client = new HttpClient();

        public async Task<IList<WootDeal>> GetFeedAsync(string feedName, string apiKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Deals are unavailable right now. Please try again later.");

            var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + Uri.EscapeDataString(feedName));
            request.Headers.Add("x-api-key", apiKey.Trim());
            request.Headers.Add("Accept", "application/json");

            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException("Woot returned HTTP " + (int)response.StatusCode + ".");

                var json = await response.Content.ReadAsStringAsync();
                return ParseDeals(json);
            }
        }

        private static IList<WootDeal> ParseDeals(string json)
        {
            var deals = new List<WootDeal>();
            JsonValue root;
            if (!JsonValue.TryParse(json, out root))
                throw new FormatException("The Woot response was not valid JSON.");

            JsonArray items = null;
            if (root.ValueType == JsonValueType.Array)
                items = root.GetArray();
            else if (root.ValueType == JsonValueType.Object)
            {
                var obj = root.GetObject();
                foreach (var key in new[] { "Items", "items", "Offers", "offers" })
                {
                    IJsonValue value;
                    if (obj.TryGetValue(key, out value) && value.ValueType == JsonValueType.Array)
                    {
                        items = value.GetArray();
                        break;
                    }
                }
            }

            if (items == null)
                throw new FormatException("The Woot response did not contain a deal list.");

            foreach (var item in items)
            {
                if (item.ValueType != JsonValueType.Object)
                    continue;
                var obj = item.GetObject();
                deals.Add(new WootDeal
                {
                    OfferId = ReadString(obj, "OfferId", "offerId"),
                    Title = ReadString(obj, "Title", "title") ?? "Untitled deal",
                    Subtitle = ReadString(obj, "Subtitle", "subtitle"),
                    SalePrice = FormatPrice(obj, "Price", "price", "SalePrice", "salePrice"),
                    ListPrice = FormatPrice(obj, "ListPrice", "listPrice", "OriginalPrice", "originalPrice"),
                    ImageUrl = ReadString(obj, "Photo", "photo", "Image", "image", "ImageUrl", "imageUrl"),
                    OfferUrl = ReadString(obj, "Url", "url", "SaleUrl", "saleUrl"),
                    IsSoldOut = ReadBool(obj, "IsSoldOut", "isSoldOut", "SoldOut", "soldOut"),
                    IsFeatured = ReadBool(obj, "IsFeatured", "isFeatured", "Featured", "featured")
                });
            }
            return deals;
        }

        public async Task<WootOfferDetails> GetOfferAsync(string offerId, string apiKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(offerId))
                throw new InvalidOperationException("This deal does not have a detail identifier.");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Deal details are unavailable right now. Please try again later.");

            var ids = new JsonArray();
            ids.Add(JsonValue.CreateStringValue(offerId));
            using (var request = new HttpRequestMessage(HttpMethod.Post, OffersUrl))
            {
                request.Content = new StringContent(ids.Stringify(), System.Text.Encoding.UTF8, "application/json");
                request.Headers.Add("x-api-key", apiKey.Trim());
                request.Headers.Add("Accept", "application/json");
                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new HttpRequestException("Woot returned HTTP " + (int)response.StatusCode + ".");
                    var json = await response.Content.ReadAsStringAsync();
                    return ParseOfferDetails(json);
                }
            }
        }

        private static WootOfferDetails ParseOfferDetails(string json)
        {
            JsonValue root;
            if (!JsonValue.TryParse(json, out root) || root.ValueType != JsonValueType.Array)
                throw new FormatException("The Woot detail response was not a valid offer list.");
            var items = root.GetArray();
            if (items.Count == 0 || items[0].ValueType != JsonValueType.Object)
                throw new FormatException("Woot returned no detail data for this offer.");
            var obj = items[0].GetObject();
            return new WootOfferDetails
            {
                Title = ReadString(obj, "Title", "title") ?? ReadString(obj, "FullTitle", "fullTitle") ?? "Untitled deal",
                FullTitle = ReadString(obj, "FullTitle", "fullTitle"),
                Subtitle = ReadString(obj, "Subtitle", "subtitle"),
                Features = PlainText(ReadString(obj, "Features", "features")),
                Specs = PlainText(ReadString(obj, "Specs", "specs")),
                WriteUp = PlainText(ReadString(obj, "WriteUpBody", "writeUpBody")),
                Teaser = PlainText(ReadString(obj, "Teaser", "teaser")),
                ImageUrl = ReadFirstPhotoUrl(obj),
                OfferUrl = ReadString(obj, "Url", "url"),
                SalePrice = FormatPrice(obj, "SalePrice", "salePrice"),
                ListPrice = FormatPrice(obj, "ListPrice", "listPrice")
            };
        }

        private static string ReadFirstPhotoUrl(JsonObject obj)
        {
            IJsonValue value;
            if (obj.TryGetValue("Photos", out value) && value.ValueType == JsonValueType.Array)
            {
                foreach (var photo in value.GetArray())
                {
                    if (photo.ValueType == JsonValueType.Object)
                    {
                        var url = ReadString(photo.GetObject(), "Url", "url");
                        if (!string.IsNullOrEmpty(url))
                            return url;
                    }
                }
            }
            return ReadString(obj, "Photo", "photo");
        }

        private static string PlainText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var text = StripMarkup(value);
            return text.Replace("&nbsp;", " ")
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"")
                .Trim();
        }

        private static string StripMarkup(string value)
        {
            var text = new System.Text.StringBuilder(value.Length);
            var inTag = false;
            foreach (var character in value)
            {
                if (character == '<')
                    inTag = true;
                else if (character == '>')
                    inTag = false;
                else if (!inTag)
                    text.Append(character);
            }
            return text.ToString();
        }

        private static string ReadString(JsonObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                IJsonValue value;
                if (obj.TryGetValue(name, out value) && value.ValueType == JsonValueType.String)
                    return value.GetString();
            }
            return null;
        }

        private static bool ReadBool(JsonObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                IJsonValue value;
                if (obj.TryGetValue(name, out value) && value.ValueType == JsonValueType.Boolean)
                    return value.GetBoolean();
            }
            return false;
        }

        private static string FormatPrice(JsonObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                IJsonValue value;
                if (!obj.TryGetValue(name, out value))
                    continue;
                if (value.ValueType == JsonValueType.Number)
                    return value.GetNumber().ToString("C");
                if (value.ValueType == JsonValueType.String)
                    return value.GetString();
                if (value.ValueType == JsonValueType.Object)
                {
                    var range = value.GetObject();
                    IJsonValue minimum;
                    if (range.TryGetValue("Minimum", out minimum) && minimum.ValueType == JsonValueType.Number)
                        return minimum.GetNumber().ToString("C");
                }
            }
            return null;
        }
    }
}
