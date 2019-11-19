﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.SpatialAlignment
{
    /// <summary>
    /// Helper base class for <see cref="ISpatialCoordinateService"/> implementations.
    /// </summary>
    /// <typeparam name="TKey">They key for the <see cref="ISpatialCoordinate"/>.</typeparam>
    public abstract class SpatialCoordinateServiceBase<TKey> : ISpatialCoordinateService
    {
        /// <inheritdoc />
        public event Action<ISpatialCoordinate> CoordinatedDiscovered;

        /// <inheritdoc />
        public event Action<ISpatialCoordinate> CoordinateRemoved;

        private readonly object discoveryLockObject = new object();
        protected readonly CancellationTokenSource disposedCTS = new CancellationTokenSource();

        private volatile bool isDiscovering = false;
        private volatile int discoveryOrCreateRequests = 0;
        private bool isDisposed = false;


        protected readonly ConcurrentDictionary<TKey, ISpatialCoordinate> knownCoordinates = new ConcurrentDictionary<TKey, ISpatialCoordinate>();

        protected void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException("SpatialCoordinateServiceBase");
            }
        }

        /// <inheritdoc />
        public bool IsTracking
        {
            get
            {
                ThrowIfDisposed();
                return discoveryOrCreateRequests > 0;
            }
        }

        protected virtual bool SupportsDiscovery => true;

        /// <inheritdoc />
        public IEnumerable<ISpatialCoordinate> KnownCoordinates
        {
            get
            {
                ThrowIfDisposed();

                return knownCoordinates.Values.Cast<ISpatialCoordinate>();
            }
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                // Notify of dispose to any existing operations
                disposedCTS.Cancel();
                disposedCTS.Dispose();

                knownCoordinates.Clear();
            }
        }

        bool ISpatialCoordinateService.TryGetKnownCoordinate(string id, out ISpatialCoordinate spatialCoordinate)
        {
            if (!TryParse(id, out TKey key))
            {
                throw new ArgumentException($"Id {id} is not recognized by this coordinate service.");
            }

            return TryGetKnownCoordinate(key, out spatialCoordinate);
        }

        /// <inheritdoc />
        public bool TryGetKnownCoordinate(TKey id, out ISpatialCoordinate spatialCoordinate)
        {
            return knownCoordinates.TryGetValue(id, out spatialCoordinate);
        }

        /// <summary>
        /// Adds a coordinate to be tracked by this service.
        /// </summary>
        protected void OnNewCoordinate(TKey id, ISpatialCoordinate spatialCoordinate)
        {
            ThrowIfDisposed();

            if (knownCoordinates.TryAdd(id, spatialCoordinate))
            {
                CoordinatedDiscovered?.Invoke(spatialCoordinate);
            }
            else
            {
                LoggingUtility.LogWarning($"Unexpected behavior, coordinate {id} was rediscovered.");
            }
        }

        /// <summary>
        /// Removes a tracked coordinate from this service.
        /// </summary>
        /// <param name="id">The id of the coordinate to remove.</param>
        /// <remarks>Will throw if coordinate was not tracked by this service, checking is possible through <see cref="knownCoordinates"/> field.</remarks>
        protected void OnRemoveCoordinate(TKey id)
        {
            ThrowIfDisposed();

            if (knownCoordinates.TryRemove(id, out ISpatialCoordinate coordinate))
            {
                CoordinateRemoved?.Invoke(coordinate);
            }
            else
            {
                throw new InvalidOperationException($"Coordinate with id '{id}' was not previously registered.");
            }
        }

        /// <inheritdoc />
        public async Task<ISpatialCoordinate> TryCreateCoordinateAsync(Vector3 worldPosition, Quaternion worldRotation, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            try
            {
                Interlocked.Increment(ref discoveryOrCreateRequests);
                return await OnTryCreateCoordinateAsync(worldPosition, worldRotation, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref discoveryOrCreateRequests);
            }
        }

        protected virtual Task<ISpatialCoordinate> OnTryCreateCoordinateAsync(Vector3 worldPosition, Quaternion worldRotation, CancellationToken cancellationToken)
        {
            return Task.FromResult<ISpatialCoordinate>(null);
        }

        /// <inheritdoc />
        Task<bool> ISpatialCoordinateService.TryDeleteCoordinateAsync(string id, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (!TryParse(id, out TKey key))
            {
                throw new ArgumentException($"Id: {id} isn't valid for this spatial coordinate service.");
            }

            return TryDeleteCoordinateAsync(key, cancellationToken);
        }

        public virtual Task<bool> TryDeleteCoordinateAsync(TKey key, CancellationToken cancellationToken)
        {
            bool wasRemoved = knownCoordinates.TryRemove(key, out ISpatialCoordinate coordinate);
            if (wasRemoved)
            {
                CoordinateRemoved?.Invoke(coordinate);
            }
            return Task.FromResult(wasRemoved);
        }

        /// <inheritdoc />
        Task<bool> ISpatialCoordinateService.TryDiscoverCoordinatesAsync(CancellationToken cancellationToken, string[] idsToLocate)
        {
            if (!SupportsDiscovery)
            {
                LoggingUtility.LogWarning($"{GetType().ToString()} does not support discovery. Failed to discover any coordinates.");
                return Task.FromResult(false);
            }

            TKey[] ids = null;
            if (idsToLocate != null && idsToLocate.Length > 0)
            {
                ids = new TKey[idsToLocate.Length];
                for (int i = 0; i < idsToLocate.Length; i++)
                {
                    if (!TryParse(idsToLocate[i], out ids[i]))
                    {
                        throw new ArgumentException($"Id: {idsToLocate[i]} isn't valid for this spatial coordinate service.");
                    }
                }
            }

            return TryDiscoverCoordinatesAsync(cancellationToken, ids);
        }

        public async Task<bool> TryDiscoverCoordinatesAsync(CancellationToken cancellationToken, params TKey[] ids)
        {
            if (!SupportsDiscovery)
            {
                LoggingUtility.LogWarning($"{GetType().ToString()} does not support discovery. Failed to discover any coordinates.");
                return false;
            }

            lock (discoveryLockObject)
            {
                ThrowIfDisposed();

                if (isDiscovering)
                {
                    LoggingUtility.LogWarning($"{GetType().ToString()} is already in a tracking state. This discovery call will be ignored.");
                    return false;
                }

                Interlocked.Increment(ref discoveryOrCreateRequests);
                isDiscovering = true;
            }

            try
            {
                using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(disposedCTS.Token, cancellationToken))
                {
                    await OnDiscoverCoordinatesAsync(cts.Token, ids).IgnoreCancellation();
                }

                return true;
            }
            catch (Exception e)
            {
                LoggingUtility.LogWarning($"Exception thrown when trying to discover coordinate: {e.ToString()}");
                isDiscovering = false;
                Interlocked.Decrement(ref discoveryOrCreateRequests);
                throw e;
            }
        }

        protected abstract bool TryParse(string id, out TKey result);

        /// <summary>
        /// Implement this method for the logic begin and end tracking (when <see cref="CancellationToken"/> is cancelled).
        /// </summary>
        protected abstract Task OnDiscoverCoordinatesAsync(CancellationToken cancellationToken, TKey[] idsToLocate = null);
    }
}
