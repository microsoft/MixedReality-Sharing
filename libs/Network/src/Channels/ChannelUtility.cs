// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Sharing.Network.Channels
{
    /// <summary>
    /// Helper utility to process and reigster the factories.
    /// </summary>
    public class ChannelsUtility
    {
        /// <summary>
        /// Popupate a map with <see cref="IChannelFactory{TChannel}"/> and the types they support.
        /// </summary>
        /// <param name="mapToFill">The map to populate.</param>
        /// <param name="factories">The list of fatories.</param>
        /// <param name="logger">The logger for any logging.</param>
        public static void PopulateChannelFactories(Dictionary<Type, IChannelFactory<IChannel>> mapToFill, IEnumerable<IChannelFactory<IChannel>> factories, ILogger logger)
        {
            foreach (IChannelFactory<IChannel> factory in factories)
            {
                // Check for all interface implementations of the factory object
                Type factoryType = factory.GetType();
                foreach (Type interfaceType in factoryType.GetInterfaces())
                {
                    if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IChannelFactory<>))
                    {
                        Type messageType = interfaceType.GetGenericArguments()[0];

                        if (mapToFill.ContainsKey(messageType))
                        {
                            logger.LogWarning($"A channel factory registration type '{messageType.FullName}' already exists, the factory '{factory.Name}' will be ignored.");
                        }
                        else
                        {
                            mapToFill.Add(messageType, factory);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Resolve <see cref="IChannelFactory{TChannel}"/> for the given type.
        /// </summary>
        /// <param name="channelFactoryMap">The factory map.</param>
        /// <param name="type">The <see cref="Type"/> to use.</param>
        /// <returns>The factory for the given type.</returns>
        public static IChannelFactory<IChannel> GetChannelFactory(IReadOnlyDictionary<Type, IChannelFactory<IChannel>> channelFactoryMap, Type type)
        {
            if (channelFactoryMap.TryGetValue(type, out IChannelFactory<IChannel> factory))
            {
                return factory;
            }

            throw new InvalidOperationException($"Can't find a channel factory for message type '{type.FullName}', and no default available.");
        }
    }
}