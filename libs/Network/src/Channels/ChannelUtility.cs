using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Sharing.Network.Channels
{
    public class ChannelsUtility
    {
        public static void ProcessChannelFactories(Dictionary<Type, IChannelFactory<IChannel>> mapToFill, IEnumerable<IChannelFactory<IChannel>> factories, ILogger logger)
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
                            logger.LogError($"A channel factory registration type '{messageType.FullName}' already exists, the factory '{factory.Name}' will be ignored.");
                        }
                        else
                        {
                            mapToFill.Add(messageType, factory);
                        }
                    }
                }
            }
        }

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