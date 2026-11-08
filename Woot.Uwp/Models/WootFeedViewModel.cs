using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Woot.Uwp.Models
{
    public sealed class WootFeedViewModel : INotifyPropertyChanged
    {
        private readonly List<WootDeal> feedOrderDeals = new List<WootDeal>();
        private WootDealSort sort = WootDealSort.Default;

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
                ApplySort();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Sort"));
            }
        }

        public void SetDeals(IEnumerable<WootDeal> deals)
        {
            feedOrderDeals.Clear();
            if (deals != null)
                feedOrderDeals.AddRange(deals);
            ApplySort();
        }

        public void ApplySort()
        {
            Deals.Clear();
            foreach (var deal in WootDealSorter.Sort(feedOrderDeals, sort))
                Deals.Add(deal);
        }

        /// <summary>
        /// False when the loaded deals carry none of the data the current sort needs,
        /// so the caller can tell the user why the order looks unchanged.
        /// </summary>
        public bool IsSortSupported
        {
            get { return WootDealSorter.IsSupportedBy(feedOrderDeals, sort); }
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
