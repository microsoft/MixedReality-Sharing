---
uid: roadmap
title: Roadmap
---
# Project Goal

The goal of MixedReality-Sharing is to provide a set cross-platform networking libraries which provide a solid foundation for rich multi-user experiences. Concretely, this covers everything from discovering peers, establishing connections and exchanging messages with them. We do this via the following independent packages

* [Discovery](../../libs/Matchmaking/docs/index.md) - Decide who is included in the experience
* [Audio/Video/Data streams](https://microsoft.github.io/MixedReality-WebRTC/) - Exchange media and data
* [Spatial Alignment](xref:Microsoft.MixedReality.Sharing.SpatialAlignment) - Establish a shared frame of reference
* Synchronization - High level state synchronization


# Milestones

Milestones are only roughly in expected chronological order.

## Audio/Video/Data (Shipped October 2019)

Goal: Ship a networking solution which supports audio/video/data.

Our solution is based on WebRTC. MixedReality-WebRTC 1.0.0 comes support for C#/C++, Desktop/UWP, ARM/x86/x64. https://github.com/microsoft/MixedReality-WebRTC/releases/tag/v1.0.0

This adds support for WebRTC on windows platforms. Developers still need to supply their own signalling solution (before opening a WebRTC connection, some out-of-band messages must be exchanged configure media types and perform NAT punchthrough)

## Network Autoconfiguration

Goal: Provide a mechanism for automatic configuration. Remove the need for explicit IP addresses.

This milestone adds v0.0.1 of [Matchmaking](../../libs/Matchmaking/docs/index.md) discovery mechanism which allows simple network autoconfiguration. This initial implementation works via UDP broadcast. In other words, all devices must be on the same subnet. Future versions will lift this restriction.

This release will be used as an integration test in a branch of [SpectatorView](https://microsoft.github.io/MixedReality-SpectatorView/). As well as the portable C# implementation, there will be some Unity specific UI components for things such as choosing a session if there are several available.

## Spatial Alignment

Goal: Simplify establishing a shared reference frame.

To create holograms which can be viewed from multiple devices, they must know their positions relative to one or more fixed points (spatial anchors). With this milestone, applications can easily create content which is firmly anchored to the real world.

Several swappable backends are available. [QR code](https://en.wikipedia.org/wiki/QR_code) can be used for an initial demo/disconnected operation. There is a natural migration path to richer services such as [Azure Spatial Anchors](https://azure.microsoft.com/en-us/services/spatial-anchors/)

The implementation is extracted and refined from [SpectatorView](https://microsoft.github.io/MixedReality-SpectatorView/). Naturally, SpectatorView will be the first consumer of the new API.

## WebRTC Signalling

Goal: Provide an integrated WebRTC signalling service. Remove the need for an external solution.

WebRTC provides a lot of value. But the need to implement signalling can be an obstacle. With this milestone, a developer can easily get multiple devices connected, without the requirement to run a server.

## Matchmaking Transport

Goal: Make Matchmaking work without requiring devices to be on the same subnet.

Provide a service and client libraries to allow discovery based on something other than the (somewhat arbitrary) network connection.
