using System;
using System.Collections.Generic;

namespace Apache.Calcite.EntityFrameworkCore.Core
{

    /// <summary>
    /// Structural equality comparer for <see cref="IReadOnlyCollection{T}"/> instances.
    /// Two collections are considered equal when they have the same <see cref="IReadOnlyCollection{T}.Count"/>
    /// and their elements are pairwise equal in enumeration order according to the supplied
    /// <paramref name="elementComparer"/> (defaults to <see cref="EqualityComparer{T}.Default"/>).
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    sealed class ReadOnlyCollectionComparer<T> : IEqualityComparer<IReadOnlyCollection<T>>
    {

        /// <summary>
        /// A singleton instance that uses <see cref="EqualityComparer{T}.Default"/> to compare elements.
        /// </summary>
        public static readonly ReadOnlyCollectionComparer<T> Default = new();

        readonly IEqualityComparer<T> _elementComparer;

        /// <summary>
        /// Initializes a new instance using the specified element comparer,
        /// or <see cref="EqualityComparer{T}.Default"/> when <paramref name="elementComparer"/> is <see langword="null"/>.
        /// </summary>
        /// <param name="elementComparer">The comparer used to compare individual elements, or <see langword="null"/> to use the default.</param>
        public ReadOnlyCollectionComparer(IEqualityComparer<T>? elementComparer = null)
        {
            _elementComparer = elementComparer ?? EqualityComparer<T>.Default;
        }

        /// <inheritdoc/>
        public bool Equals(IReadOnlyCollection<T>? x, IReadOnlyCollection<T>? y)
        {
            if (x is null)
                return y is null;
            if (y is null || x.Count != y.Count)
                return false;

            using var ex = x.GetEnumerator();
            using var ey = y.GetEnumerator();
            while (ex.MoveNext() && ey.MoveNext())
                if (!_elementComparer.Equals(ex.Current, ey.Current))
                    return false;

            return true;
        }

        /// <inheritdoc/>
        public int GetHashCode(IReadOnlyCollection<T> collection)
        {
            var h = new HashCode();
            foreach (var item in collection)
                h.Add(item is not null ? _elementComparer.GetHashCode(item) : 0);

            return h.ToHashCode();
        }

    }

}
