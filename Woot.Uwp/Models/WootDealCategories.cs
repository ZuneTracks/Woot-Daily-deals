using System;
using System.Collections.Generic;

namespace Woot.Uwp.Models
{
    /// <summary>
    /// Interprets the raw category strings Woot attaches to every feed item.
    /// </summary>
    /// <remarks>
    /// A deal carries a flat list that mixes the sites it appears on with the
    /// subcategory paths beneath them, for example
    /// <c>[ "HOME", "TOOLS", "HOME/Lighting &amp; Fans", "TOOLS/Lighting &amp; Fans" ]</c>.
    /// The bare site names duplicate the category pivot and are of no use as a
    /// filter, so only the entries carrying a path are offered. The leading site is
    /// dropped from those: a deal cross-posted to several sites would otherwise show
    /// up as two filter entries meaning the same thing.
    /// </remarks>
    public static class WootDealCategories
    {
        private const char Separator = '/';

        /// <summary>
        /// The subcategory within a raw category entry, or null when the entry names
        /// only a site and so describes nothing more specific than the feed itself.
        /// </summary>
        public static string SubcategoryOf(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return null;
            var separator = category.IndexOf(Separator);
            if (separator < 0)
                return null;
            var subcategory = category.Substring(separator + 1).Trim();
            return subcategory.Length == 0 ? null : subcategory;
        }

        /// <summary>
        /// Every subcategory present in the given deals, alphabetically and without
        /// duplicates, ready to offer as filter choices.
        /// </summary>
        public static IList<string> DistinctSubcategories(IEnumerable<WootDeal> deals)
        {
            var subcategories = new List<string>();
            if (deals == null)
                return subcategories;

            foreach (var deal in deals)
            {
                if (deal == null || deal.Categories == null)
                    continue;
                foreach (var category in deal.Categories)
                {
                    var subcategory = SubcategoryOf(category);
                    if (subcategory != null && IndexOf(subcategories, subcategory) < 0)
                        subcategories.Add(subcategory);
                }
            }

            subcategories.Sort(StringComparer.OrdinalIgnoreCase);
            return subcategories;
        }

        /// <summary>
        /// True when the deal belongs to the given subcategory. A null or empty
        /// subcategory means no filter, so every deal matches.
        /// </summary>
        public static bool IsIn(WootDeal deal, string subcategory)
        {
            if (string.IsNullOrEmpty(subcategory))
                return true;
            if (deal == null || deal.Categories == null)
                return false;

            foreach (var category in deal.Categories)
            {
                if (string.Equals(SubcategoryOf(category), subcategory, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Case-insensitive position of a subcategory in a list, or -1 when absent.
        /// </summary>
        public static int IndexOf(IList<string> subcategories, string subcategory)
        {
            if (subcategories == null || subcategory == null)
                return -1;
            for (var index = 0; index < subcategories.Count; index++)
            {
                if (string.Equals(subcategories[index], subcategory, StringComparison.OrdinalIgnoreCase))
                    return index;
            }
            return -1;
        }
    }
}
