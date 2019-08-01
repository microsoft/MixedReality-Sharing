// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Sharing.Utilities.Collections
{
    /// <summary>
    /// Helper class that implements <see cref="IReadOnlyCollection{T}"/>. 
    /// The existing <see cref="System.Collections.ObjectModel.ReadOnlyCollection{T}"/> has some strange issues, for example can't be given an <see cref="ICollection{T}"/> , nor an <see cref="IReadOnlyCollection{T}"/> . 
    /// Some concurrent classes implement <see cref="IReadOnlyCollection{T}"/>, but if we expose them they can be cast back to the original type; however, those classes don't implement <see cref="ICollection{T}"/>. 
    /// (Ex: <see cref="System.Collections.Concurrent.ConcurrentBag{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the collection.</typeparam>
    public class ReadOnlyCollectionWrapper<T> : IReadOnlyCollection<T>
    {
        private readonly Func<int> getCountFunc;
        private readonly Func<IEnumerator<T>> getEnumeratorFunc;

        int IReadOnlyCollection<T>.Count => getCountFunc();

        /// <summary>
        /// A general purpose constructor to enable this to act as a wrapper to any collection of items.
        /// </summary>
        /// <param name="getCountFunc">The function that returns count of items for the underlying source of items.</param>
        /// <param name="getEnumeratorFunc">The function that returns an <see cref="IEnumerator{T}"/> for the underlyingsource of items.</param>
        public ReadOnlyCollectionWrapper(Func<int> getCountFunc, Func<IEnumerator<T>> getEnumeratorFunc)
        {

            this.getCountFunc = getCountFunc ?? throw new ArgumentNullException(nameof(getCountFunc));
            this.getEnumeratorFunc = getEnumeratorFunc ?? throw new ArgumentNullException(nameof(getEnumeratorFunc));
        }

        /// <summary>
        /// Wraps a collection implementing <see cref="IReadOnlyCollection{T}"/>, like <see cref="System.Collections.Concurrent.ConcurrentBag{T}"/>.
        /// </summary>
        public ReadOnlyCollectionWrapper(IReadOnlyCollection<T> collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            getCountFunc = () => collection.Count;
            getEnumeratorFunc = collection.GetEnumerator;
        }

        /// <summary>
        /// Wraps a collection implementing <see cref=ICollection{T}"/>.
        /// </summary>
        public ReadOnlyCollectionWrapper(ICollection<T> collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            getCountFunc = () => collection.Count;
            getEnumeratorFunc = collection.GetEnumerator;
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return getEnumeratorFunc();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>)this).GetEnumerator();
        }
    }
}
