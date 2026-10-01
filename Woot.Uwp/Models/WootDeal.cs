using System;
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
        public string OfferUrl { get; set; }
        public bool IsSoldOut { get; set; }
        public bool IsFeatured { get; set; }
        public double? SalePriceValue { get; set; }
        public double? ListPriceValue { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public double? SoldOutPercentage { get; set; }
        public int FeedOrder { get; set; }

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
