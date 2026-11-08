using System;
using System.Collections.Generic;
using Windows.UI.Xaml;

namespace Woot.Uwp.Models
{
    public sealed class WootDeal
    {
        public string Title { get; set; }
        public string OfferId { get; set; }
        public string Subtitle { get; set; }
        public string SalePrice { get; set; }
        public string ListPrice { get; set; }
        public string ImageUrl { get; set; }

        /// <summary>
        /// <see cref="ImageUrl"/> as a Uri, so the list can bind a BitmapImage that
        /// limits its decode size. Null when Woot gave no usable address, which
        /// simply leaves the image blank.
        /// </summary>
        public Uri ImageUri
        {
            get
            {
                Uri parsed;
                return Uri.TryCreate(ImageUrl, UriKind.Absolute, out parsed) ? parsed : null;
            }
        }

        public string OfferUrl { get; set; }
        public bool IsSoldOut { get; set; }
        public bool IsFeatured { get; set; }
        public double? SalePriceValue { get; set; }
        public double? ListPriceValue { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public int FeedOrder { get; set; }

        /// <summary>
        /// The raw category entries Woot returned, mixing site names with
        /// subcategory paths. See <see cref="WootDealCategories"/>.
        /// </summary>
        public IList<string> Categories { get; set; }

        public double? DiscountPercent
        {
            get
            {
                if (!SalePriceValue.HasValue || !ListPriceValue.HasValue)
                    return null;
                if (ListPriceValue.Value <= 0d || SalePriceValue.Value < 0d)
                    return null;
                if (SalePriceValue.Value >= ListPriceValue.Value)
                    return 0d;
                return (ListPriceValue.Value - SalePriceValue.Value) / ListPriceValue.Value * 100d;
            }
        }

        public string StateText { get { return IsSoldOut ? "SOLD OUT" : "AVAILABLE"; } }
        public Visibility FeaturedVisibility { get { return IsFeatured ? Visibility.Visible : Visibility.Collapsed; } }
    }
}
