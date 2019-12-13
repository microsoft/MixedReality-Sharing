// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.MixedReality.Sharing.Matchmaking.Example
{
    /// <summary>
    /// Simple P2P chat app that exemplifies usage of <see cref="PeerDiscoveryAgent"/> and <see cref="UdpPeerDiscoveryTransport"/>.
    /// Usage: P2PChat [localUsername] [broadcastAddress]
    /// </summary>
    class P2PChat : IDisposable
    {
        private const ushort DiscoveryPort = 45678;
        private const ushort ChatPort = 45679;
        private const string ParticipantCategory = "Microsoft.MixedReality.Sharing.Matchmaking.DiscoveryDemo/participant";
        private const string NameKey = "name";
        private const string Prompt = "Write a message: ";

        private IDiscoveryAgent _discoveryAgent;
        private IDiscoverySubscription _discoverySubscription;

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

        private TcpListener _chatServer;

        private string _username;
        private string _localAddrString;

        private readonly object _consoleLock = new object();

        public void Run(string username, IPAddress broadcastAddress)
        {
            _username = username;
            _localAddrString = GetLocalIPAddress().ToString();
            _chatServer = new TcpListener(IPAddress.Any, ChatPort);
            _chatServer.Start();
            Task.Run(ListenForConnections);

            // Initialize the discovery agent
            _discoveryAgent = new PeerDiscoveryAgent(new UdpPeerDiscoveryTransport(broadcastAddress, DiscoveryPort));

            // Publish a resource exposing the local IP address for connections and the user name as an attribute.
            var connection = GetLocalIPAddress().ToString();
            var attributes = new Dictionary<string, string> { [NameKey] = username };
            _discoveryAgent.PublishAsync(ParticipantCategory, connection, attributes);

            // Subscribe to other participant resources.
            _discoverySubscription = _discoveryAgent.Subscribe(ParticipantCategory);
            Action<IDiscoverySubscription> onUpdateCallback = (IDiscoverySubscription subscription) =>
            {
                // Parse discovered resources.
                var activePeers = new Dictionary<IPAddress, string>();
                foreach (var res in subscription.Resources)
                {
                    if (res.Connection == _localAddrString)
                    {
                        // Exclude the local resource.
                        continue;
                    }
                    try
                    {
                        var address = IPAddress.Parse(res.Connection);
                        var name = res.Attributes[NameKey];
                        activePeers.Add(address, name);
                    }
                    catch (Exception e)
                    {
                        // Invalid resource format, or multiple resources per host.
                        Debug.WriteLine($"Invalid resource: {e}");
                        continue;
                    }
                }

                // Create reader connections to the active peers.
                RefreshReaderConnections(activePeers);
            };
            _discoverySubscription.Updated += onUpdateCallback;

            // Initialize the readers list.
            onUpdateCallback(_discoverySubscription);

            // Loop waiting for input.
            Console.CursorTop = Console.WindowHeight;
            Console.Write(Prompt);
            while (true)
            {
                string message = Console.ReadLine();
                PostLocalMessageToConsole(message);
                PostLocalMessageToPeers(message);
            }
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
            _chatServer?.Stop();

            // Dispose of the subscription (this is not strictly necessary if
            // disposing of the agent shortly after, but it is good form).
            _discoverySubscription?.Dispose();

            // Dispose of the agent.
            _discoveryAgent?.Dispose();
        }

        void RefreshReaderConnections(Dictionary<IPAddress, string> activePeers)
        {
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
                            PostMessageToConsole($"{name} has joined");
                            var reader = new Reader { PeerName = name, Client = client };
                            _readers.Add(reader);
                            Task.Run(() => ListenForMessages(reader));
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

        #region Listening loops

        private void ListenForConnections()
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
        }

        void ListenForMessages(Reader reader)
        {
            try
            {
                using (var strReader = new BinaryReader(reader.Client.GetStream(), Encoding.UTF8, leaveOpen: true))
                {
                    while (true)
                    {
                        string message = strReader.ReadString();
                        PostMessageToConsole($"{reader.PeerName}: {message}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                lock (_readers)
                {
                    _readers.Remove(reader);
                }
                PostMessageToConsole($"{reader.PeerName} has left");
            }
        }

        #endregion

        #region Utilities

        void PostMessageToConsole(string message)
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

        void PostLocalMessageToConsole(string message)
        {
            lock (_consoleLock)
            {
                Console.WindowTop = 0;
                Console.WindowLeft = 0;
                PostMessageToConsole($"{_username}: {message}");

                Console.Write(Prompt);
                for (int i = 0; i < message.Length; ++i)
                {
                    Console.Write(' ');
                }
                Console.CursorLeft -= message.Length;
            }
        }

        void PostLocalMessageToPeers(string message)
        {
            lock (_writers)
            {
                foreach (var peer in _writers)
                {
                    using (var writer = new BinaryWriter(peer.GetStream(), Encoding.UTF8, leaveOpen: true))
                    {
                        writer.Write(message);
                    }
                }
            }
        }

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

            using (var simpleChat = new P2PChat())
            {
                simpleChat.Run(localName, broadcastAddress);
            }
        }
    }
}
