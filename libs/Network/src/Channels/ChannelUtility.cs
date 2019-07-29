using Microsoft.MixedReality.Sharing.Utilities;
using System;
using System.Collections.Generic;

namespace Microsoft.MixedReality.Sharing.Network.Channels
{
    public class ChannelsUtility
    {
        public static void ProcessChannelFactories<TSessionType>(Dictionary<Type, IChannelFactory<TSessionType, IMessage>> mapToFill, IEnumerable<IChannelFactory<TSessionType, IMessage>> factories, ILogger logger)
            where TSessionType : class, ISession<TSessionType>
        {
            foreach (IChannelFactory<TSessionType, IMessage> factory in factories)
            {
                // Check for all interface implementations of the factory object
                Type factoryType = factory.GetType();
                foreach (Type interfaceType in factoryType.GetInterfaces())
                {
                    if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IChannelFactory<,>))
                    {
                        Type messageType = interfaceType.GetGenericArguments()[1];

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

        public static IChannelFactory<TSessionType, TMessageType> GetChannelFactory<TSessionType, TMessageType>(IReadOnlyDictionary<Type, IChannelFactory<TSessionType, IMessage>> channelFactoryMap)
            where TSessionType : class, ISession<TSessionType>
            where TMessageType : IMessage
        {
            if (channelFactoryMap.TryGetValue(typeof(TMessageType), out IChannelFactory<TSessionType, IMessage> factory))
            {
                return (IChannelFactory<TSessionType, TMessageType>)factory;
            }

            throw new InvalidOperationException($"Can't find a channel factory for message type '{typeof(TMessageType).FullName}', and no default available.");
        }
    }
}