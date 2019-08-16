﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.MixedReality.Sharing.Network
{
    public enum ChannelType
    {
        /// <summary>
        /// Guarantees that every message is either eventually delivered in its entirety, or dropped (no
        /// fragmented/corrupted messages will be received).
        /// </summary>
        Unordered,

        /// <summary>
        /// In addition to the `Unordered` guarantees, this guarantees that:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// each message is retransmitted until its delivery is confirmed, or the connection breaks;
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// any sequence of messages is eventually delivered in the same order as it was sent, or dropped (no
        /// out-of-order messages or duplicates will be received).
        /// </description>
        /// </item>
        /// </list>
        /// </summary>
        Ordered
    }

    /// <summary>
    /// Category of channels/messages. Groups channels and messages relative to the same application area/system.
    /// </summary>
    /// <remarks>
    /// A `IChannelCategory` can be used by the process to listen for messages, directed to any
	/// <see cref="IEndpoint"/> of which this process is part, that belong to said category. This
	/// is done by calling <see cref="StartListening"/> and polling <see cref="Queue"/> for messages.
	/// Alternatively, you can create a <see cref="MessageQueueConsumer"/> if you want an event to
	/// be raised every time a message arrives.
    ///
    /// A `IChannelCategory` can also be used to create a <see cref="IChannel"/> and send messages to another process.
    ///
    /// Categories are created through an <see cref="IChannelCategoryFactory"/> and they are identified by their
    /// `Name`. No two categories created by the same factory will have the same name. Two process sending messages of
    /// a specific category to each other must have the exact same category definition on both sides (i.e. the
    /// categories must have the same <see cref="Type"/>). If the sender process uses a `IChannelCategory` with a
    /// different `Type` from the receiver process, its messages won't be delivered.
    /// </remarks>
    public interface IChannelCategory : IDisposable
    {
        /// <summary>
        /// Identifies the category within its <see cref="IChannelCategoryFactory"/>.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Determines the behavior of the <see cref="IChannel"/>s belonging to this category.
        /// </summary>
        ChannelType Type { get; }

        /// <summary>
        /// Queue for incoming messages. Only populated between a call to <see cref="StartListening"/>
        /// and one to <see cref="StopListening"/>.
        /// </summary>
        IMessageQueue Queue { get; }

        /// <summary>
        /// Start listening for incoming messages of this category. Messages will be added to <see cref="Queue"/>.
        /// </summary>
        void StartListening();

        /// <summary>
        /// Stop listening for incoming messages of this category.
        /// </summary>
        void StopListening();
    }

    public interface IChannelCategoryFactory
    {
        /// <summary>
        /// Create a new category.
        /// </summary>
        /// <exception cref="ArgumentException">if a category with the same name already exists.</exception>
        IChannelCategory Create(string name, ChannelType type);

        /// <summary>
        /// Get a category if existing, otherwise return null.
        /// </summary>
        IChannelCategory Get(string name);

        /// <summary>
        /// Get a category by name if existing, otherwise create a new one setting the provided name and type.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// if a category whose name is <paramref name="name"/> exists but its type is different from
        /// <paramref name="type"/>
        /// </exception>
        IChannelCategory GetOrCreate(string name, ChannelType type);
    }
}
