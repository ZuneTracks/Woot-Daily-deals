using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Woot.Uwp.Models
{
    public sealed class WootFeedViewModel : INotifyPropertyChanged
    {
        private readonly List<WootDeal> feedOrderDeals = new List<WootDeal>();
        private readonly List<WootDeal> visibleDeals = new List<WootDeal>();
        private readonly List<string> subcategories = new List<string>();
        private WootDealSort sort = WootDealSort.Default;
        private string subcategory;

        public WootFeedViewModel(string name)
        {
            Name = name;
            Deals = new ObservableCollection<WootDeal>();
            StatusText = "Not loaded. Press Refresh if nothing appears.";
        }

        public string Name { get; private set; }

        /// <summary>
        /// The sorted collection the UI binds to. Always mutated in place so the
        /// existing bindings stay attached.
        /// </summary>
        public ObservableCollection<WootDeal> Deals { get; private set; }

        /// <summary>
        /// The deals as Woot returned them, independent of the current sort.
        /// </summary>
        public IList<WootDeal> FeedOrderDeals { get { return feedOrderDeals; } }

        public WootDealSort Sort
        {
            get { return sort; }
            set
            {
                if (sort == value)
                    return;
                sort = value;
                ApplyFilterAndSort();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Sort"));
            }
        }

        /// <summary>
        /// The subcategory the list is narrowed to, or null to show everything.
        /// </summary>
        public string Subcategory
        {
            get { return subcategory; }
            set
            {
                var selected = string.IsNullOrWhiteSpace(value) ? null : value;
                if (subcategory == selected)
                    return;
                subcategory = selected;
                ApplyFilterAndSort();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Subcategory"));
            }
        }

        /// <summary>
        /// The subcategories present in the loaded deals, as filter choices.
        /// </summary>
        public IList<string> Subcategories { get { return subcategories; } }

        public bool IsFiltered { get { return subcategory != null; } }

        /// <summary>
        /// How many deals survive the current filter.
        /// </summary>
        public int VisibleCount { get { return visibleDeals.Count; } }

        public void SetDeals(IEnumerable<WootDeal> deals)
        {
            feedOrderDeals.Clear();
            if (deals != null)
                feedOrderDeals.AddRange(deals);

            subcategories.Clear();
            foreach (var name in WootDealCategories.DistinctSubcategories(feedOrderDeals))
                subcategories.Add(name);

            // Woot's deals turn over daily, so a subcategory chosen earlier may simply
            // not exist in what just arrived. Clearing it keeps the user from staring
            // at an empty list with no obvious cause.
            if (subcategory != null && WootDealCategories.IndexOf(subcategories, subcategory) < 0)
                subcategory = null;

            ApplyFilterAndSort();
        }

        public void ApplyFilterAndSort()
        {
            visibleDeals.Clear();
            foreach (var deal in feedOrderDeals)
            {
                if (WootDealCategories.IsIn(deal, subcategory))
                    visibleDeals.Add(deal);
            }

            Deals.Clear();
            foreach (var deal in WootDealSorter.Sort(visibleDeals, sort))
                Deals.Add(deal);
        }

        /// <summary>
        /// False when the deals on show carry none of the data the current sort needs,
        /// so the caller can tell the user why the order looks unchanged.
        /// </summary>
        public bool IsSortSupported
        {
            get { return WootDealSorter.IsSupportedBy(visibleDeals, sort); }
        }

        private string statusText;
        public string StatusText
        {
            get { return statusText; }
            set
            {
                if (statusText == value)
                    return;
                statusText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("StatusText"));
            }
        }
        public bool IsLoaded { get; set; }

        /// <summary>
        /// When the deals on show were saved to disk, or null when they came from
        /// Woot during this session. Set only while a cached list is standing in for
        /// a feed that has not loaded, so the caller can say how old it is.
        /// </summary>
        public DateTimeOffset? CachedAt { get; set; }

        /// <summary>
        /// When these deals were last fetched, or null if they never have been.
        /// </summary>
        public DateTimeOffset? LastLoaded { get; set; }

        /// <summary>
        /// True when the cached deals have aged past <paramref name="maxAge"/> and are
        /// worth re-fetching. Woot allows 1000 requests a day, so a feed the user is
        /// not looking at is left alone until it is shown again.
        /// </summary>
        public bool IsStale(TimeSpan maxAge)
        {
            return !LastLoaded.HasValue || DateTimeOffset.UtcNow - LastLoaded.Value >= maxAge;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
