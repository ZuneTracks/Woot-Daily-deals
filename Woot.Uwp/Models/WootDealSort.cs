using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Woot.Uwp.Models
{
    public enum WootDealSort
    {
        Default = 0,
        PriceLowToHigh = 1,
        PriceHighToLow = 2,
        BiggestDiscount = 3,
        Newest = 4,
        EndingSoonest = 5,
        MostPopular = 6,
        AvailableFirst = 7,
        FeaturedFirst = 8,
        TitleAscending = 9,
        TitleDescending = 10
    }

    public sealed class WootDealSortOption
    {
        private static readonly ReadOnlyCollection<WootDealSortOption> all =
            new ReadOnlyCollection<WootDealSortOption>(new List<WootDealSortOption>
            {
                new WootDealSortOption(WootDealSort.Default, "Default order"),
                new WootDealSortOption(WootDealSort.PriceLowToHigh, "Price: low to high"),
                new WootDealSortOption(WootDealSort.PriceHighToLow, "Price: high to low"),
                new WootDealSortOption(WootDealSort.BiggestDiscount, "Biggest discount"),
                new WootDealSortOption(WootDealSort.Newest, "Newest first"),
                new WootDealSortOption(WootDealSort.EndingSoonest, "Ending soonest"),
                new WootDealSortOption(WootDealSort.MostPopular, "Most popular"),
                new WootDealSortOption(WootDealSort.AvailableFirst, "Available first"),
                new WootDealSortOption(WootDealSort.FeaturedFirst, "Featured first"),
                new WootDealSortOption(WootDealSort.TitleAscending, "Title: A to Z"),
                new WootDealSortOption(WootDealSort.TitleDescending, "Title: Z to A")
            });

        private WootDealSortOption(WootDealSort sort, string label)
        {
            Sort = sort;
            Label = label;
        }

        public WootDealSort Sort { get; private set; }
        public string Label { get; private set; }

        public static IList<WootDealSortOption> All { get { return all; } }
        public static int IndexOf(WootDealSort sort)
        {
            for (var index = 0; index < all.Count; index++)
            {
                if (all[index].Sort == sort)
                    return index;
            }
            return 0;
        }

        public static bool IsDefined(int value)
        {
            return all.Any(option => (int)option.Sort == value);
        }

        /// <summary>
        /// The ComboBox displays options through this override rather than
        /// DisplayMemberPath, so no reflection is needed on .NET Native.
        /// </summary>
        public override string ToString()
        {
            return Label;
        }
    }

    public static class WootDealSorter
    {
        public static IEnumerable<WootDeal> Sort(IEnumerable<WootDeal> deals, WootDealSort sort)
        {
            var source = deals ?? Enumerable.Empty<WootDeal>();
            switch (sort)
            {
                case WootDealSort.PriceLowToHigh:
                    return source.OrderBy(deal => deal.SalePriceValue.HasValue ? 0 : 1)
                        .ThenBy(deal => deal.SalePriceValue ?? 0d)
                        .ThenBy(deal => deal.FeedOrder);
                case WootDealSort.PriceHighToLow:
                    return source.OrderBy(deal => deal.SalePriceValue.HasValue ? 0 : 1)
                        .ThenByDescending(deal => deal.SalePriceValue ?? 0d)
                        .ThenBy(deal => deal.FeedOrder);
                case WootDealSort.BiggestDiscount:
                    return source.OrderBy(deal => deal.DiscountPercent.HasValue ? 0 : 1)
                        .ThenByDescending(deal => deal.DiscountPercent ?? 0d)
                        .ThenBy(deal => deal.FeedOrder);
                case WootDealSort.Newest:
                    return source.OrderBy(deal => deal.StartDate.HasValue ? 0 : 1)
                        .ThenByDescending(deal => deal.StartDate ?? DateTimeOffset.MinValue)
                        .ThenBy(deal => deal.FeedOrder);
                case WootDealSort.EndingSoonest:
                    return source.OrderBy(deal => deal.EndDate.HasValue ? 0 : 1)
                        .ThenBy(deal => deal.EndDate ?? DateTimeOffset.MaxValue)
                        .ThenBy(deal => deal.FeedOrder);
                case WootDealSort.MostPopular:
                    return source.OrderBy(deal => deal.SoldOutPercentage.HasValue ? 0 : 1)
                        .ThenByDescending(deal => deal.SoldOutPercentage ?? 0d)
                        .ThenBy(deal => deal.FeedOrder);
                case WootDealSort.AvailableFirst:
                    return source.OrderBy(deal => deal.IsSoldOut ? 1 : 0)
                        .ThenBy(deal => deal.FeedOrder);
                case WootDealSort.FeaturedFirst:
                    return source.OrderBy(deal => deal.IsFeatured ? 0 : 1)
                        .ThenBy(deal => deal.FeedOrder);
                case WootDealSort.TitleAscending:
                    return source.OrderBy(deal => deal.Title ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(deal => deal.FeedOrder);
                case WootDealSort.TitleDescending:
                    return source.OrderByDescending(deal => deal.Title ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(deal => deal.FeedOrder);
                default:
                    return source.OrderBy(deal => deal.FeedOrder);
            }
        }

        /// <summary>
        /// Reports whether the feed carries the data a sort needs. Sorts whose source
        /// field is absent from the Woot response would otherwise silently leave the
        /// list in feed order.
        /// </summary>
        public static bool IsSupportedBy(IEnumerable<WootDeal> deals, WootDealSort sort)
        {
            var source = deals == null ? new List<WootDeal>() : deals.ToList();
            if (source.Count == 0)
                return true;
            switch (sort)
            {
                case WootDealSort.PriceLowToHigh:
                case WootDealSort.PriceHighToLow:
                    return source.Any(deal => deal.SalePriceValue.HasValue);
                case WootDealSort.BiggestDiscount:
                    return source.Any(deal => deal.DiscountPercent.HasValue);
                case WootDealSort.Newest:
                    return source.Any(deal => deal.StartDate.HasValue);
                case WootDealSort.EndingSoonest:
                    return source.Any(deal => deal.EndDate.HasValue);
                case WootDealSort.MostPopular:
                    return source.Any(deal => deal.SoldOutPercentage.HasValue);
                default:
                    return true;
            }
        }
    }
}
