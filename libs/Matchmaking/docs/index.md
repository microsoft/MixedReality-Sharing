---
uid: index
title: Index
---
# MixedReality-Sharing Matchmaking 0.0.1

The Matchmaking library automates the process of discovering and joining MR experiences over the network.

Matchmaking is a C# .NET Standard 2.0 library. It is available as a [NuGet package](insert_link). Alternatively, the [Microsoft.MixedReality.Sharing.Matchmaking](../) project can be added to a Visual Studio solution and built from source.

Currently the library contains a simple system to publish and discover arbitrary resources (application users, shared experiences, or others) on a local network using UDP.

## Resource discovery

Matchmaking defines a __resource__ as an application-specific entity available on the network. Application clients looking for shared experiences can be resources, as well as hosting servers or individual sessions of a shared experience.

A resource has:
- a _connection string_: an application-defined string that indicates how to connect to the resource. This can be an URL, an IP address-port pair, or any string that can be interpreted by the application network transport to start a communication with the resource.
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
    sessionSubscription.Updated += (IDiscoverySubscription sub) =>
    {
        foreach (IDiscoveryResource res in sub.Resources)
        {
            Console.WriteLine("Discovered session: " + res.Category);
        }
    };
    ```

The [IDiscoveryResource](../src/IDiscoveryResource.cs) interface gives read access to resources published and discovered. In general, the publisher of a resource can edit its attribute after publishing by calling `RequestEdit` on the `IDiscoveryResource` and using the obtained `IDiscoveryResourceEditor`.

## Peer-to-peer discovery agent

The discovery API is generic and can implemented on top of a variery of protocols/network transports. The Matchmaking library contains a simple implementation, useful for prototypes/demos where all the participant devices are joined to the same local network.

TODO
_Agent uses simple P2P protocol, based on [SSDP](https://tools.ietf.org/html/draft-cai-ssdp-v1-03). Can choose the transport - memory-based (for testing) or UDP-based. Examples_

### Limitations
TODO 
_not production-ready - no performance, security etc. Only small packets(< UDP limit)_
