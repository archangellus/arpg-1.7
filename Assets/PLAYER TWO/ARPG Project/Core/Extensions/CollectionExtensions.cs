using System.Collections;
using System.Linq;

namespace PLAYERTWO.ARPGProject
{
    public static class CollectionExtensions
    {
        /// <summary>
        /// Checks if the given index is valid for the array.
        /// This method returns true if the index is within the bounds of the array, otherwise false.
        /// </summary>
        /// <param name="array">The array to check against.</param>
        /// <param name="index">The index to validate.</param>
        /// <returns>Returns true if the index is valid, otherwise false.</returns>
        public static bool IsIndexValid(this System.Array array, int index) =>
            array != null && index >= 0 && index < array.Length;

        /// <summary>
        /// Checks if the given index is valid for the collection.
        /// This method returns true if the index is within the bounds of the collection, otherwise false.
        /// </summary>
        /// <param name="collection">The collection to check against.</param>
        /// <param name="index">The index to validate.</param>
        /// <returns>Returns true if the index is valid, otherwise false.</returns>
        public static bool IsIndexValid(this ICollection collection, int index) =>
            collection != null && index >= 0 && index < collection.Count;

        /// <summary>
        /// Checks if the element at the specified index in the collection is null or invalid.
        /// This method returns true if the index is invalid or if the element at that index is null.
        /// If the index is valid and the element is not null, it returns false.
        /// </summary>
        /// <param name="collection">The collection to check against.</param>
        /// <param name="index">The index to validate.</param>
        /// <returns>Returns true if the index is invalid or the element is null, otherwise false.</returns>
        public static bool IsInvalidOrNullAt(this IList collection, int index) =>
            !collection.IsIndexValid(index) || collection[index] == null;

        /// <summary>
        /// Checks if the collection has any values.
        /// This method returns true if the collection is not null and contains one or more elements.
        /// </summary>
        /// <param name="collection">The collection to check.</param>
        /// <returns>Returns true if the collection has values, otherwise false.</returns>
        public static bool HasValues(this System.Array collection) =>
            collection != null && collection.Length > 0;

        /// <summary>
        /// Removes all null or empty entries from the given collection.
        /// This method filters the collection and returns a new array containing only non-null entries.
        /// </summary>
        /// <param name="collection">The collection to filter.</param>
        /// <returns>A new array containing only non-null entries.</returns>
        public static T[] RemoveEmptyEntries<T>(this System.Array collection)
        {
            if (!collection.HasValues())
                return System.Array.Empty<T>();

            return collection.OfType<T>().Where(e => e != null).ToArray();
        }
    }
}
