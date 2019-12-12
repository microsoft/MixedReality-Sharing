// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.MixedReality.Sharing.Matchmaking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking.DiscoveryDemo
{
    /// <summary>
    /// Simple P2P chat app that exemplifies usage of <see cref="PeerDiscoveryAgent"/> and <see cref="UdpPeerDiscoveryTransport"/>.
    /// Usage: SimpleChat [localUsername] [broadcastAddress]
    /// </summary>
    class Program : IDisposable
    {
        private const ushort DiscoveryPort = 45678;
        private const ushort ChatPort = 45679;
        private const string ParticipantCategory = "Microsoft.MixedReality.Sharing.Matchmaking.DiscoveryDemo/participant";
        private const string NameKey = "name";

        private class Publisher
        {
            public string Name;
            public TcpClient Client;
        }

        private List<TcpClient> _subscribers = new List<TcpClient>();
        private List<Publisher> _publishers = new List<Publisher>();

        private readonly string _username;
        private readonly string _localAddrString;

        private readonly TcpListener _chatServer;
        private readonly IDiscoveryAgent _discoveryAgent;
        private readonly IDiscoverySubscription _discoverySubscription;
        private readonly object _consoleLock = new object();

        Program(string username, IPAddress broadcastAddress)
        {
            _username = username;
            _localAddrString = GetLocalIPAddress().ToString();
            _chatServer = new TcpListener(IPAddress.Any, ChatPort);
            ListenForConnectionsAsync();

            // Initialize the discovery agent
            _discoveryAgent = new PeerDiscoveryAgent(new UdpPeerDiscoveryTransport(broadcastAddress, DiscoveryPort));

            // Publish a resource exposing the local IP address for connections and the user name as an attribute.
            var connection = GetLocalIPAddress().ToString();
            var attributes = new Dictionary<string, string> { [NameKey] = username };
            _discoveryAgent.PublishAsync(ParticipantCategory, connection, attributes);

            // Subscribe to other participant resources.
            _discoverySubscription = _discoveryAgent.Subscribe(ParticipantCategory);
            _discoverySubscription.Updated += subscription =>
            {
                // When the active resources change, refresh the publishers list.
                RefreshPublishers(_discoverySubscription.Resources);
            };

            // Initialize the publishers list.
            RefreshPublishers(_discoverySubscription.Resources);
        }

        public void Dispose()
        {
            // Dispose of the various connections.
            foreach (var peer in _publishers)
            {
                peer.Client.Dispose();
            }
            foreach (var client in _subscribers)
            {
                client.Dispose();
            }
            ((IDisposable)_chatServer).Dispose();

            // Dispose of the subscription (this is not strictly necessary if
            // disposing of the agent shortly after, but it is good form).
            _discoverySubscription.Dispose();

            // Dispose of the agent.
            _discoveryAgent.Dispose();
        }

        void RefreshPublishers(IEnumerable<IDiscoveryResource> resources)
        {
            // Exclude the local resource.
            resources = resources.Where(r => r.Connection != _localAddrString);

            // Parse discovered resources.
            var activePublishers = new Dictionary<IPAddress, string>();
            foreach (var res in resources)
            {
                try
                {
                    var address = IPAddress.Parse(res.Connection);
                    var name = res.Attributes[NameKey];
                    activePublishers.Add(address, name);
                }
                catch(Exception)
                {
                    // Invalid resource format, or multiple resources per host.
                    continue;
                }
            }

            Publisher[] expiredPublishers;
            lock(_publishers)
            {
                var knownAddresses = _publishers.Select(p => GetLocalAddress(p.Client)).ToHashSet();

                // Remove publishers that are no longer active.
                expiredPublishers = _publishers.Where(p => !activePublishers.ContainsKey(GetLocalAddress(p.Client))).ToArray();
                _publishers = _publishers.Except(expiredPublishers).ToList();

                // Add publishers that weren't previously known.
                foreach (var peer in activePublishers)
                {
                    IPAddress address = peer.Key;
                    string name = peer.Value;
                    if (!knownAddresses.Contains(address))
                    {
                        var client = new TcpClient();
                        client.NoDelay = true;
                        try
                        {
                            // Connect to the publisher and start listening for messages.
                            client.Connect(address, ChatPort);
                            PostMessage($"{name} has joined");
                            var publisher = new Publisher { Name = name, Client = client };
                            _publishers.Add(publisher);
                            ListenForMessagesAsync(publisher);
                        }
                        catch (SocketException e)
                        {
                            Debug.WriteLine(e);
                            client.Dispose();
                        }
                    }
                }
            }

            // Cleanup.
            foreach (var Publisher in expiredPublishers)
            {
                Publisher.Client.Dispose();
            }
        }

        #region Listening tasks

        private Task ListenForConnectionsAsync()
        {
            _chatServer.Start();
            return Task.Run(() =>
            {
                while (true)
                {
                    var client = _chatServer.AcceptTcpClient();
                    lock(_subscribers)
                    {
                        _subscribers.Add(client);
                    }
                }
            });
        }

        Task ListenForMessagesAsync(Publisher publisher)
        {
            return Task.Run(() =>
            {
                try
                {
                    using (var reader = new BinaryReader(publisher.Client.GetStream(), Encoding.UTF8, leaveOpen: true))
                    {
                        while (true)
                        {
                            string message = reader.ReadString();
                            PostMessage($"{publisher.Name}: {message}");
                        }
                    }
                }
                catch(Exception e)
                {
                    Debug.WriteLine(e);
                    Debug.WriteLine("Stop listening");
                    lock(_publishers)
                    {
                        _publishers.Remove(publisher);
                    }
                }
            });
        }

        #endregion

        #region Input/Output

        void PostMessage(string message)
        {
            lock(_consoleLock)
            {
                Console.MoveBufferArea(0, 1, Console.WindowWidth, Console.WindowHeight - 2, 0, 0);
                var oldLeft = Console.CursorLeft;
                Console.CursorTop = Console.WindowHeight - 3;
                Console.CursorLeft = 0;
                Console.Write(message);
                Console.CursorTop = Console.WindowHeight;
                Console.CursorLeft = oldLeft;
            }
        }
        void ReadInput()
        {
            const string prompt = "Write a message: ";
            Console.CursorTop = Console.WindowHeight;
            Console.Write(prompt);
            while (true)
            {
                string temp = Console.ReadLine();
                lock (_consoleLock)
                {
                    Console.WindowTop = 0;
                    Console.WindowLeft = 0;
                    PostMessage($"{_username}: {temp}");

                    Console.Write(prompt);
                    for (int i = 0; i < temp.Length; ++i)
                    {
                        Console.Write(' ');
                    }
                    Console.CursorLeft -= temp.Length;
                }
                lock (_subscribers)
                {
                    foreach (var peer in _subscribers)
                    {
                        using (var writer = new BinaryWriter(peer.GetStream(), Encoding.UTF8, leaveOpen: true))
                        {
                            writer.Write(temp);
                        }
                    }
                }
            }

        }

        #endregion

        #region Utilities

        private static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }

            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        private static IPAddress GetLocalAddress(TcpClient client)
        {
            return ((IPEndPoint)client.Client.LocalEndPoint).Address;
        }

        #endregion

        static void Main(string[] args)
        {
            var localName = GetLocalIPAddress().ToString();
            if (args.Length > 0)
            {
                localName = args[0];
            }

            IPAddress broadcastAddress;
            if (args.Length <= 1 ||!IPAddress.TryParse(args[1], out broadcastAddress))
            {
                broadcastAddress = IPAddress.Broadcast;
            }

            using (var simpleChat = new Program(localName, broadcastAddress))
            {
                simpleChat.ReadInput();
            }
        }
    }
}
