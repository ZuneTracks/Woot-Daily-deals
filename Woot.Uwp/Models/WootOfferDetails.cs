namespace Woot.Uwp.Models
{
    public sealed class WootOfferDetails
    {
        public string Title { get; set; }
        public string FullTitle { get; set; }
        public string Subtitle { get; set; }
        public string Features { get; set; }
        public string Specs { get; set; }
        public string WriteUp { get; set; }
        public string Teaser { get; set; }
        public string ImageUrl { get; set; }
        public string OfferUrl { get; set; }
        public string SalePrice { get; set; }
        public string ListPrice { get; set; }

        /// <summary>
        /// Woot's blurred remaining-stock estimate, 0-100. Present only on the
        /// offer detail endpoints; the category feed never carries it.
        /// </summary>
        public double? PercentageRemaining { get; set; }
    }
}
