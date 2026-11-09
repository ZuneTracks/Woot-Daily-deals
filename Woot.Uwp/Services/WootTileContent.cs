using System;
using System.Collections.Generic;
using System.Text;
using Woot.Uwp.Models;

namespace Woot.Uwp.Services
{
    /// <summary>
    /// Builds the XML for the live tile.
    /// <para>
    /// Kept apart from <see cref="WootTileService"/>, which does the WinRT work, so
    /// the markup can be checked without a device. Malformed tile XML throws when it
    /// is loaded, and on the foreground path nothing catches that.
    /// </para>
    /// <para>
    /// The tile used to be built from the Windows 8 templates
    /// TileSquare150x150Text04 and TileWide310x150Text03. Both draw only their first
    /// text element, so of the three lines handed to them only "Last viewed" ever
    /// appeared. The featured deals the background refresh exists to show were never
    /// drawn at any tile size, which made the tile look as though it only changed
    /// when a deal was tapped. Adaptive bindings draw every line and wrap properly,
    /// and the app already requires 15063, so they are safe to use.
    /// </para>
    /// </summary>
    public static class WootTileContent
    {
        private const string NoDeals = "Open the app to load deals.";

        public static string Build(string lastTitle, string lastPrice, IList<WootDeal> featured)
        {
            var deals = featured ?? new List<WootDeal>();
            var lastViewed = string.IsNullOrWhiteSpace(lastTitle)
                ? null
                : "Last viewed: " + lastTitle +
                  (string.IsNullOrWhiteSpace(lastPrice) ? string.Empty : " - " + lastPrice);

            return "<tile><visual>" +
                Binding("TileMedium", "none", MediumLines(deals)) +
                Binding("TileWide", "name", WideLines(deals, lastViewed)) +
                Binding("TileLarge", "nameAndLogo", LargeLines(deals, lastViewed)) +
                "</visual></tile>";
        }

        private static string MediumLines(IList<WootDeal> featured)
        {
            if (featured.Count == 0)
                return Text("captionSubtle", "FEATURED") + Text("body", NoDeals, 3);

            return Text("captionSubtle", "FEATURED") +
                Text("body", featured[0].Title, 2) +
                Text("captionSubtle", featured[0].SalePrice);
        }

        private static string WideLines(IList<WootDeal> featured, string lastViewed)
        {
            if (featured.Count == 0)
                return Text("captionSubtle", "WOOT! FEATURED") +
                    Text("body", NoDeals, 2) +
                    Text("captionSubtle", lastViewed);

            var lines = Text("captionSubtle", "WOOT! FEATURED") +
                Text("body", featured[0].Title, 2) +
                Text("captionSubtle", featured[0].SalePrice);
            if (featured.Count > 1)
                lines += Text("captionSubtle", Summarise(featured[1]));
            return lines;
        }

        private static string LargeLines(IList<WootDeal> featured, string lastViewed)
        {
            var lines = Text("captionSubtle", "WOOT! FEATURED");
            if (featured.Count == 0)
            {
                lines += Text("body", NoDeals, 2);
            }
            else
            {
                foreach (var deal in featured)
                {
                    lines += Text("body", deal.Title, 2);
                    lines += Text("captionSubtle", deal.SalePrice);
                }
            }
            return lines + Text("captionSubtle", lastViewed, 2);
        }

        private static string Summarise(WootDeal deal)
        {
            var title = deal.Title ?? string.Empty;
            return string.IsNullOrWhiteSpace(deal.SalePrice) ? title : title + " - " + deal.SalePrice;
        }

        private static string Binding(string template, string branding, string lines)
        {
            return "<binding template='" + template + "' branding='" + branding + "'>" + lines + "</binding>";
        }

        /// <summary>
        /// A single tile line. An empty value is dropped rather than drawn as a blank
        /// row, and only wrapping lines are given a line budget.
        /// </summary>
        private static string Text(string style, string value, int maxLines = 0)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return "<text hint-style='" + style + "'" +
                (maxLines > 0 ? " hint-wrap='true' hint-maxLines='" + maxLines + "'" : string.Empty) +
                ">" + Escape(value) + "</text>";
        }

        /// <summary>
        /// Deal titles arrive from Woot and routinely contain ampersands and quotes,
        /// and occasionally characters XML will not accept at all.
        /// </summary>
        private static string Escape(string value)
        {
            var text = value ?? string.Empty;
            var builder = new StringBuilder(text.Length);
            foreach (var character in text)
            {
                if (IsXmlCharacter(character))
                    builder.Append(character);
            }
            return builder.ToString()
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private static bool IsXmlCharacter(char character)
        {
            return character == '\t' || character == '\n' || character == '\r' ||
                (character >= '\u0020' && character <= '\uD7FF') ||
                (character >= '\uE000' && character <= '\uFFFD');
        }
    }
}
