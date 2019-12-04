---
uid: index
title: Index
---
# MixedReality-Sharing Matchmaking 0.0.1

The Matchmaking library automates the process of discovering and joining MR experiences over the network.

Matchmaking is a C# .NET Standard 2.0 library. It is available as a [NuGet package](insert_link). Alternatively, the [Microsoft.MixedReality.Sharing.Matchmaking](../) project can be added to a Visual Studio solution and built from source.

Currently the library contains a simple system to publish and discover arbitrary resources (application users, shared experiences, or others) on a local network using UDP. Check the [project roadmap](../../../docs/manual/roadmap.md) for planned features.

## Resource discovery

Matchmaking defines a __resource__ as an application-specific entity available on the network. Application clients looking for shared experiences can be resources, as well as hosting servers or individual sessions of a shared experience.

A resource has:
- a _connection string_: an application-defined string that indicates how to connect to the resource. This can be an URL, an IP address-port pair, or any string that can be interpreted by the network transport used by the application to start a communication with the resource.
- a _category string_: an application-defined string that indicates the type of the resource. This can be used to distinguish between resources published by different applications or between different types of resources within the same application (e.g. users vs shared sessions).
- a set of optional _attributes_, application-defined key-value pairs. These can be used to expose room properties or capabilities to the discovery system.

[IDiscoveryAgent](../src/IDiscoveryAgent.cs) is the main entry point of the discovery system. Through an agent you can:
1. **publish** resources on the network for other application processes to discover:

    ```csharp
    IDiscoveryAgent agent = InitDiscoveryAgent();
    var cts = new CancellationTokenSource();
    string category = "myapp/session";
    string connection = "http://myappserver.org/session42";
    var attributes = new Dictionary<string, string>{ ["environment"] = "house" };
    IDiscoveryResource newSession = await agent.PublishAsync(category, connection, attributes, cts.Token);
    ```
2. **subscribe** to resources published by other application processes:
    
    ```csharp
    string category = "myapp/session";
    IDiscoverySubscription sessionSubscription = agent.Subscribe(category);
    IDiscoveryResource foundSession = null;
    while (foundSession == null)
    {
        foreach (IDiscoveryResource res in sessionSubscription.Resources)
        {
            if (res.Attributes["environment"] == "house")
            {
                Console.WriteLine("Discovered session at " + res.Connection);
                foundSession = res;
                break;
            }
        }
    }
    // Unsubscribe from further updates.
    sessionSubscription.Dispose();
    ```

The [IDiscoveryResource](../src/IDiscoveryResource.cs) interface gives read access to resources published and discovered. In general, the publisher of a resource can edit its attribute after publishing by calling `RequestEdit` on the `IDiscoveryResource` and using the obtained [IDiscoveryResourceEditor](../src/IDiscoveryResource.cs).

`IDiscoveryAgent.Dispose()` stops advertising resources and terminates any active subscriptions.

## Peer-to-peer discovery agent

The discovery API is generic and can be implemented on top of a variery of protocols/network transports. The Matchmaking library contains a simple implementation, useful for prototypes/demos where all the participant devices are joined to the same local network or multicast group.

[PeerDiscoveryAgent](../src/Peer/PeerDiscoveryAgent.cs) implements IDiscoveryAgent using a simple peer-to-peer protocol loosely based on [SSDP](https://tools.ietf.org/html/draft-cai-ssdp-v1-03). When an agent publishes a resource, it starts periodically broadcasting announcement messages announcing its availability and attributes. When an agent subscribes to a category, it broadcasts a query message to which publishers reply with the current active resources, and starts listening for periodic announcements. Every announcement contains the resource lifetime in seconds - agents will consider a resource expired after an interval equal to its lifetime has passed from the last announcement about the resource.

Messages are exchanged between agents using a [IPeerDiscoveryTransport](../src/Peer/PeerDiscoveryTransport.cs) specified on agent creation:
```csharp
var transport = new UdpPeerDiscoveryTransport(IPAddress.Broadcast, 45278);
var agent = new PeerDiscoveryAgent(transport);
```

IPeerDiscoveryTransport is a simple convenience interface to send and receive broadcast messages among peers. The library contains an implementation that exchanges messages through UDP broadcast/multicast ([UdpPeerDiscoveryTransport](../src/Peer/UdpPeerDiscoveryTransport.cs)) plus a memory-based, in-process one for testing ([MemoryPeerDiscoveryTransport](../src/Peer/MemoryPeerDiscoveryTransport.cs)).

### Limitations
PeerDiscoveryAgent is meant to be used in small-size prototypes and is not recommended in production application that expect to handle many concurrent agents/resources. Importantly, the implementation assumes a trusted network and it is not suitable to applications that might deal with malicious network traffic.

UdpPeerDiscoveryTransport sends one UDP packet per resource announcement, including all the resource data - category, connection, attributes. Depending on the network configuration, UDP packets bigger than a certain size might be fragmented - increasing packet loss and decreasing performance - or dropped by the network stack. The implementation will therefore work reliably only if the total data for each resource is low - generally under 1KB.
