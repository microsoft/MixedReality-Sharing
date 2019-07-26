// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Extensions.Experimental.Socketer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Network.Socketer
{
    internal class SocketerChannelCategory : IChannelCategory
    {
        public string Name { get; }
        public ChannelType Type { get; }
        public IMessageQueue Queue { get => queue_; }

        // 16-bit length + category name as UTF-8 sequence.
        // Prepended to all messages sent using this channel.
        internal byte[] Header;

        private SocketerChannelCategoryFactory factory_;
        private MessageQueue queue_ = new MessageQueue();

        internal SocketerChannelCategory(string name, ChannelType type, SocketerChannelCategoryFactory factory)
        {
            Name = name;
            Type = type;
            factory_ = factory;

            // Build category header.
            byte[] nameToBytes = Encoding.UTF8.GetBytes(name);
            byte[] lengthToBytes = BitConverter.GetBytes(nameToBytes.Length);
            Header = new byte[2 + nameToBytes.Length];
            Array.Copy(lengthToBytes, Header, 2);
            Array.Copy(nameToBytes, 0, Header, 2, nameToBytes.Length);
        }

        public void Dispose()
        {
            factory_.Remove(this);
        }

        // Dispatch message to either queue or event.
        internal void Dispatch(SocketerEndpoint sender, byte[] payload)
        {
            var msg = new Message(sender, this, payload);
            queue_.Add(msg);
        }

        internal SocketerChannel Create(SocketerEndpoint endpoint)
        {
            // TODO check if a channel with same name and endpoint exists already
            return new SocketerChannel(this, endpoint, factory_);
        }

        public void StartListening()
        {
            throw new NotImplementedException();
        }

        public void StopListening()
        {
            throw new NotImplementedException();
        }
    }

    public class SocketerChannelCategoryFactory : IChannelCategoryFactory
    {
        // Continuously listen for new connections/datagrams.
        private SocketerClient tcpServer_;
        private SocketerClient udpServer_;

        // Keeps all created categories.
        private ConcurrentDictionary<string, SocketerChannelCategory> categoryFromName_ =
            new ConcurrentDictionary<string, SocketerChannelCategory>();

        // Send queues for active (connected) sockets. The sending loop polls these for new messages to send.
        private SendQueue[] activeSendQueues_ = { new SendQueue() };

        // Send queues pointing to TCP sockets that are still connecting.
        private List<SendQueue> connectingQueues_ = new List<SendQueue>();

        // Represents a socket that can be used to send data. Can correspond to either an outgoing socket or to
        // an incoming connection to the local TCP server.
        // Counts the number of SocketerChannels that are using a socket to know when it's safe to close it.
        private class SocketEntry
        {
            // If this corresponds to an outgoing socket, Socket is said socket and SourceId is 0.
            // If this corresponds to an incoming connection, Socket is tcpServer_ and SourceId is the connection ID
            // in the server.
            public readonly SocketerClient Socket;
            public readonly int SourceId;

            public int RefCount = 1;

            public SocketEntry(SocketerClient socket, int sourceId = 0)
            {
                Socket = socket;
                SourceId = sourceId;
            }
        }

        // Queues of messages waiting to be sent through a channel + sockets to send them.
        private class SendQueue : BlockingCollection<byte[]>
        {
            public SocketEntry Entry;
        }

        private Dictionary<string, SocketEntry> udpSocketFromEndpoint_ = new Dictionary<string, SocketEntry>();
        private Dictionary<string, SocketEntry> tcpSocketFromEndpoint_ = new Dictionary<string, SocketEntry>();

        // Used to stop the sending thread.
        private bool keepSending_ = true;

        private event Action activeSendQueuesChanged_;

        public SocketerChannelCategoryFactory(int port)
        {
            tcpServer_ = new SocketerClient(SocketerClient.Protocol.TCP, port);
            udpServer_ = new SocketerClient(SocketerClient.Protocol.UDP, port);
            tcpServer_.Message += OnMessage;
            tcpServer_.Connected += OnConnected;
            tcpServer_.Disconnected += OnDisconnected;
            udpServer_.Message += OnMessage;
            tcpServer_.Start();
            udpServer_.Start();

            Task.Run(RunSendingLoop);
        }

        private void RunSendingLoop()
        {
            // Listen to updates to the active send queues. If there are updates, cancel any ongoing wait operation on
            // the send queues and reset the token.
            CancellationTokenSource cts = new CancellationTokenSource();
            activeSendQueuesChanged_ += () =>
            {
                cts.Cancel();
            };

            while (keepSending_)
            {
                cts = new CancellationTokenSource();
                var token = cts.Token;
                while (keepSending_)
                {
                    // Get the current active send queues array in a local variable (activeSendQueues_ might be changed
                    // by other threads).
                    var currentSendQueues = activeSendQueues_;

                    // Wait for a message.
                    byte[] item;
                    int index;
                    try
                    {
                        index = SendQueue.TakeFromAny(currentSendQueues, out item, token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Send queues have been updated. Restart from the beginning.
                        // The event handler above will update `token` to a new one (note that this might take a few
                        // iterations to happen).
                        continue;
                    }

                    // Send the message.
                    try
                    {
                        currentSendQueues[index].Entry.Socket.SendNetworkMessage(item, currentSendQueues[index].Entry.SourceId);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.ToString());
                        // TODO set to not ok
                    }
                }
            }
        }

        public void Dispose()
        {
            keepSending_ = false;

            tcpServer_.Stop();
            udpServer_.Stop();
            lock (tcpSocketFromEndpoint_)
            {
                foreach (var socket in tcpSocketFromEndpoint_.Values)
                {
                    socket.Socket.Stop();
                }
                tcpSocketFromEndpoint_.Clear();
            }
            lock (udpSocketFromEndpoint_)
            {
                foreach (var socket in udpSocketFromEndpoint_.Values)
                {
                    socket.Socket.Stop();
                }
                udpSocketFromEndpoint_.Clear();
            }
        }

        private void UpdateActiveQueues(SendQueue[] newActiveQueues)
        {
            // Set the currently active queues to a new set and restart the send thread.
            // Update the field.
            activeSendQueues_ = newActiveQueues;
            // Notify the sending thread.
            activeSendQueuesChanged_.Invoke();
        }

        private void OnConnected(SocketerClient socket, int id, string host, int port)
        {
            // Triggered when a client TCP socket is connected, or when the TCP server receives an incoming connection.
            if (socket == tcpServer_)
            {
                // Incoming connection. Add the endpoint-socket couple to the map for sharing.
                lock (tcpSocketFromEndpoint_)
                {
                    tcpSocketFromEndpoint_.Add(SocketerEndpoint.GetId(host, port), new SocketEntry(socket, id));
                }
            }

            lock(this)
            {
                // If there are queues waiting for the connection, move them to the active queues.
                var connected = connectingQueues_.FindAll(q => q.Entry.Socket == socket);
                var newActiveQueues = new SendQueue[activeSendQueues_.Length + connected.Count];
                Array.Copy(activeSendQueues_, newActiveQueues, activeSendQueues_.Length);
                connected.CopyTo(newActiveQueues, activeSendQueues_.Length);
                UpdateActiveQueues(newActiveQueues);
                // todo could be done in first loop
                connectingQueues_ = connectingQueues_.FindAll(q => q.Entry.Socket != socket);
            }
        }

        private void OnDisconnected(SocketerClient socket, int id, string host, int port)
        {
            // Triggered when a client TCP socket or the TCP server lose a connection.
            lock (this)
            {
                // Clear and remove the queues associated to this socket.
                var toRemove = Array.FindAll(activeSendQueues_, q => (q.Entry == null || q.Entry.Socket != socket));
                var newActiveQueues = Array.FindAll(activeSendQueues_, q => (q.Entry == null || q.Entry.Socket != socket));
                UpdateActiveQueues(newActiveQueues);
            }
            lock (tcpSocketFromEndpoint_)
            {
                // Remove the endpoint-socket couple.
                var key = SocketerEndpoint.GetId(host, port);
                var entry = tcpSocketFromEndpoint_[key];
                entry.Socket.Disconnected -= OnDisconnected;
                entry.Socket.Connected -= OnConnected;
                entry.Socket.Message -= OnMessage;
                entry.Socket.Stop();
                tcpSocketFromEndpoint_.Remove(key);
            }
        }

        private void OnMessage(SocketerClient client, SocketerClient.MessageEvent ev)
        {
            try
            {
                string categoryName;
                byte[] payload;
                Utils.ExtractCategory(ev.Message, out categoryName, out payload);

                SocketerChannelCategory category;
                if (categoryFromName_.TryGetValue(categoryName, out category))
                {
                    // TODO check if the category has the same type
                    category.Dispatch(new SocketerEndpoint(ev.SourceHost, ev.SourcePort), payload);
                }
            }
            catch(Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        public IChannelCategory Create(string name, ChannelType type)
        {
            var res = new SocketerChannelCategory(name, type, this);
            ((IDictionary<string, SocketerChannelCategory>)categoryFromName_).Add(name, res);
            return res;
        }

        public IChannelCategory Get(string name)
        {
            SocketerChannelCategory res;
            categoryFromName_.TryGetValue(name, out res);
            return res;
        }

        public IChannelCategory GetOrCreate(string name, ChannelType type)
        {
            return categoryFromName_.GetOrAdd(name, new SocketerChannelCategory(name, type, this));
        }

        internal void Remove(SocketerChannelCategory category)
        {
            ((IDictionary<string, SocketerChannelCategory>)categoryFromName_).Remove(category.Name);
        }

        static SocketerClient.Protocol ProtocolFromType(ChannelType type)
        {
            switch (type)
            {
                case ChannelType.Unordered: return SocketerClient.Protocol.UDP;
                case ChannelType.Ordered: return SocketerClient.Protocol.TCP;
                default: throw new ArgumentException();
            }
        }

        // Create a send queue (and possibly a new socket) for a new/reconnecting SocketerChannel.
        internal BlockingCollection<byte[]> CreateChannelQueue(SocketerEndpoint endpoint, ChannelType type)
        {
            // Check if an entry for this endpoint-protocol exists, or create one.
            string endpointId = endpoint.Id;
            var map = (type == ChannelType.Unordered) ? udpSocketFromEndpoint_ : tcpSocketFromEndpoint_;
            var protocol = ProtocolFromType(type);
            var newSocket = new SocketerClient(protocol, endpoint.Host, endpoint.Port);
            SocketEntry entry;
            lock (map)
            {
                if (map.TryGetValue(endpointId, out entry))
                {
                    ++entry.RefCount;
                }
                else
                {
                    entry = new SocketEntry(newSocket);
                    map.Add(endpointId, entry);
                }
            }

            // Make a new queue.
            var newQueue = new SendQueue();
            newQueue.Entry = entry;
            if (type == ChannelType.Ordered && entry.Socket != tcpServer_) //< not connected yet
            {
                lock(this)
                {
                    // The connection needs to be established before we start sending messages through the Socketer.
                    // So we add the queue to the ones waiting for connection. Will be moved to the active ones by the
                    // connection handler.
                    connectingQueues_.Add(newQueue);
                }
            }
            else
            {
                lock(this)
                {
                    // Add the queue to the active ones.
                    var newActiveQueues = new SendQueue[activeSendQueues_.Length + 1];
                    Array.Copy(activeSendQueues_, newActiveQueues, activeSendQueues_.Length);
                    newActiveQueues[newActiveQueues.Length - 1] = newQueue;
                    UpdateActiveQueues(newActiveQueues);
                }
            }

            if (entry.Socket == newSocket)
            {
                // Start the socket (if this is a TCP client, starts the connection request).
                newSocket.Message += OnMessage;
                newSocket.Connected += OnConnected;
                newSocket.Disconnected += OnDisconnected;
                newSocket.Start();
            }
            return newQueue;
        }

        // Dispose the send queue (and possibly the new socket) for a SocketerChannel.
        internal void DisposeChannelQueue(BlockingCollection<byte[]> queue, SocketerEndpoint endpoint, ChannelType type)
        {
            lock(this)
            {
                // Remove the queue from the active/connecting lists.
                int index = Array.FindIndex(activeSendQueues_, q => q == queue);
                if (index >= 0)
                {
                    var newActiveQueues = new SendQueue[activeSendQueues_.Length - 1];
                    Array.Copy(activeSendQueues_, newActiveQueues, index);
                    Array.Copy(activeSendQueues_, index + 1, newActiveQueues, index, newActiveQueues.Length - index);
                    UpdateActiveQueues(newActiveQueues);
                }
                else
                {
                    connectingQueues_.Remove((SendQueue)queue);
                }
                queue.Dispose();
            }

            // Get the corresponding socket entry.
            string endpointId = endpoint.Id;
            var map = (type == ChannelType.Unordered) ? udpSocketFromEndpoint_ : tcpSocketFromEndpoint_;
            lock (map)
            {
                SocketEntry existing;
                if (map.TryGetValue(endpointId, out existing))
                {
                    --existing.RefCount;
                    if (existing.RefCount == 0)
                    {
                        // No other channels use this socket; shut it down and remove it.
                        existing.Socket.Disconnected -= OnDisconnected;
                        existing.Socket.Connected -= OnConnected;
                        existing.Socket.Message -= OnMessage;
                        existing.Socket.Stop();
                        map.Remove(endpointId);
                    }
                }
                // else was already removed by disconnection
            }
        }
    }

    public static class Utils
    {
        public static void ExtractCategory(byte[] message, out string category, out byte[] payload)
        {
            int categoryLength = BitConverter.ToInt16(message, 0);
            category = Encoding.UTF8.GetString(message, 2, categoryLength);
            int payloadOffset = 2 + categoryLength;
            payload = new byte[message.Length - payloadOffset];
            Array.Copy(message, payloadOffset, payload, 0, payload.Length);
        }

        public static byte[] PrependCategory(byte[] payload, byte[] categoryHeader)
        {
            var message = new byte[categoryHeader.Length + payload.Length];
            Array.Copy(categoryHeader, message, categoryHeader.Length);
            Array.Copy(payload, 0, message, categoryHeader.Length, payload.Length);
            return message;
        }
    }
}
