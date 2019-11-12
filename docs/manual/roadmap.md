---
uid: roadmap
title: Roadmap
---
# Project Goal

The goal of MixedReality-Sharing is to provide a set crossplatform networking libraries which provide a solid foundation for rich multiuser experiences. Concretely, this covers everything from discovering peers, establishing connections and exchanging messages with them. We do this via the following independent packages

* [Discovery](xref:Microsoft.MixedReality.Sharing.Matchmaking) - Decide who is included in the experience
* [Audio/Video/Data streams](https://microsoft.github.io/MixedReality-WebRTC/) - Exchange media and data
* [Spatial Alignment](xref:Microsoft.MixedReality.Sharing.SpatialAlignment) - Establish a shared frame of reference
* Synchronization - High level state synchronization


# Milestones

## Milestone 1 (Shipped October 2019)

Ship WebRTC 1.0.0 with support for C#/C++, Desktop/UWP, ARM/x86/x64. https://github.com/microsoft/MixedReality-WebRTC/releases/tag/v1.0.0

This adds support for WebRTC on windows platforms. Developers still need to supply their own signalling solution (before opening a WebRTC connection, some out-of-band messages must be exchanaged configure media types and perform NAT punchthrough)

## Milestone 2 (Date TBD)

This milestone adds a discovery mechanism which allows simple network autoconfiguration.