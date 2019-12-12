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

        private readonly IDiscoveryAgent _discoveryAgent;
        private readonly IDiscoverySubscription _discoverySubscription;

        // For the sake of simplicity, this demo keeps two connections between each pair of peers,
        // each of which used in one direction only. In a real application, on discovery there
        // would be a handshake and only one connection would be opened.
        private class Reader
        {
            public string PeerName;
            public TcpClient Client;
        }

        // Used by this process to read messages from other peers.
        private List<Reader> _readers = new List<Reader>();

        // Used by this process to write messages to other peers.
        private List<TcpClient> _writers = new List<TcpClient>();

        private readonly TcpListener _chatServer;

        private readonly string _username;
        private readonly string _localAddrString;

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
                // When the active resources change, refresh the readers list.
                RefreshReaders(_discoverySubscription.Resources);
            };

            // Initialize the readers list.
            RefreshReaders(_discoverySubscription.Resources);
        }

        public void Dispose()
        {
            // Dispose of the various connections.
            foreach (var reader in _readers)
            {
                reader.Client.Dispose();
            }
            foreach (var writer in _writers)
            {
                writer.Dispose();
            }
            ((IDisposable)_chatServer).Dispose();

            // Dispose of the subscription (this is not strictly necessary if
            // disposing of the agent shortly after, but it is good form).
            _discoverySubscription.Dispose();

            // Dispose of the agent.
            _discoveryAgent.Dispose();
        }

        void RefreshReaders(IEnumerable<IDiscoveryResource> resources)
        {
            // Exclude the local resource.
            resources = resources.Where(r => r.Connection != _localAddrString);

            // Parse discovered resources.
            var activePeers = new Dictionary<IPAddress, string>();
            foreach (var res in resources)
            {
                try
                {
                    var address = IPAddress.Parse(res.Connection);
                    var name = res.Attributes[NameKey];
                    activePeers.Add(address, name);
                }
                catch(Exception)
                {
                    // Invalid resource format, or multiple resources per host.
                    continue;
                }
            }

            Reader[] expiredReaders;
            lock(_readers)
            {
                var knownAddresses = _readers.Select(p => GetLocalAddress(p.Client)).ToHashSet();

                // Remove readers that are no longer active.
                expiredReaders = _readers.Where(p => !activePeers.ContainsKey(GetLocalAddress(p.Client))).ToArray();
                _readers = _readers.Except(expiredReaders).ToList();

                // Create readers for peers that weren't previously known.
                foreach (var peer in activePeers)
                {
                    IPAddress address = peer.Key;
                    string name = peer.Value;
                    if (!knownAddresses.Contains(address))
                    {
                        var client = new TcpClient();
                        client.NoDelay = true;
                        try
                        {
                            // Connect to the peer and start listening for messages.
                            client.Connect(address, ChatPort);
                            PostMessage($"{name} has joined");
                            var reader = new Reader { PeerName = name, Client = client };
                            _readers.Add(reader);
                            ListenForMessagesAsync(reader);
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
            foreach (var Reader in expiredReaders)
            {
                Reader.Client.Dispose();
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

                    // Start sending the local messages to this connection.
                    lock (_writers)
                    {
                        _writers.Add(client);
                    }
                }
            });
        }

        Task ListenForMessagesAsync(Reader reader)
        {
            return Task.Run(() =>
            {
                try
                {
                    using (var strReader = new BinaryReader(reader.Client.GetStream(), Encoding.UTF8, leaveOpen: true))
                    {
                        while (true)
                        {
                            string message = strReader.ReadString();
                            PostMessage($"{reader.PeerName}: {message}");
                        }
                    }
                }
                catch(Exception e)
                {
                    Debug.WriteLine(e);
                    Debug.WriteLine("Stop listening");
                    lock(_readers)
                    {
                        _readers.Remove(reader);
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
                lock (_writers)
                {
                    foreach (var peer in _writers)
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
