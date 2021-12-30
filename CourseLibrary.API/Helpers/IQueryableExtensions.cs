using CourseLibrary.API.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;

namespace CourseLibrary.API.Helpers
{
    public static class IQueryableExtensions
    {
        public static IQueryable<T> ApplySort<T>(this IQueryable<T> source, string orderBy,
            Dictionary<string, PropertyMappingValue> mappingDictionary)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (mappingDictionary == null)
            {
                throw new ArgumentNullException(nameof(mappingDictionary));
            }

            // if no order by just return the iqueryable as is
            if (string.IsNullOrWhiteSpace(orderBy))
            {
                return source;
            }

            var orderByString = string.Empty;

            // the orderBy string is separated by ",", so we split it
            var orderByAfterSplit = orderBy.Split(',');

            // apply each orderBy clause in reverse order - otherwise the
            // IQueryable will be ordered in the wrong order
            foreach (var orderByClause in orderByAfterSplit.Reverse())
            {
                // trim the orderBy clause, as it might contain leading
                // or trailing spaces.  Can't trim the var in foreach
                // so use another var.
                var trimmmedOrderByClause = orderByClause.Trim();

                // if the sort option ends with " desc", we order
                // descending, otherwise ascending
                var orderDescending = trimmmedOrderByClause.EndsWith(" desc");

                // remove " asc" or " desc" from the orderBy clause, so we
                // get the property name to look for in the mapping dictionary
                var indexOfFirstSpace = trimmmedOrderByClause.IndexOf(" ");
                var propertyName = indexOfFirstSpace == -1 ?
                    trimmmedOrderByClause : trimmmedOrderByClause.Remove(indexOfFirstSpace);

                // find the matching property
                if (!mappingDictionary.ContainsKey(propertyName))
                {
                    throw new ArgumentException($"Key mapping for {propertyName} is missing");
                }

                // get the PropertyMappingValue
                var propertyMappingValue = mappingDictionary[propertyName];

                if (propertyMappingValue == null)
                {
                    throw new ArgumentException("propertyMappingValue");
                }

                // Run through the property names
                // so the orderBy clauses are applied in the correct order
                foreach(var destinationProperty in propertyMappingValue.DestinationProperties)
                {
                    // revert sort order if neccessary
                    if (propertyMappingValue.Revert)
                    {
                        orderDescending = !orderDescending;
                    }

                    orderByString = orderByString +
                        (string.IsNullOrWhiteSpace(orderByString) ? string.Empty : ", ")
                        + destinationProperty
                        + (orderDescending ? " descending" : " ascending");
                }
            }

            return source.OrderBy(orderByString);
        }
    }
}
