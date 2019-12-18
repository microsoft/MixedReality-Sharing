
# MixedReality-Sharing 

[![Licensed under the MIT License](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/microsoft/MixedReality-WebRTC/blob/master/LICENSE)
[![Under active development](https://img.shields.io/badge/status-active-green.svg)](https://github.com/microsoft/MixedReality-Sharing/commits/master)

MixedReality-Sharing is a cross-platform solution to enable rich multiuser experiences. In particular most mixed reality experiences need the following:
 - Matchmaking - find and join experiences
 - Streaming - transmit and receive audio/video/data
 - Localization - agree a shared reference frame (anchor)

MixedReality-Sharing provides library packages to fulfil each of the above needs. The packages are independent, meaning they can be adopted or replaced incrementally. Integration libraries are provided so that the packages work together seamlessly, both with each other and with external libraries.

## Matchmaking

The [Matchmaking.Discovery](libs/Matchmaking/docs/index.md)  is based on a simple advertisement/discovery protocol. The transport is pluggable and there are implementations for peer-to-peer UDP broadcast and dedicated server.

## Streaming

We recommend [MixedReality-WebRTC](https://github.com/microsoft/MixedReality-WebRTC) for streaming audio, video and data. As with any WebRTC solution, an external signaling mechanism is required to establish a connection. We provide an in-process signaling solution to make this easy.

## Localization

On augmented reality (AR) platforms, “anchors” are a common frame of reference for enabling multiple users to place digital content in the same physical location, where it can be seen on different devices in the same position and orientation relative to the environment.

[SpatialAlignment](libs/SpatialAlignment/docs/index.md) allows a variety of backends such as [Azure Spatial Anchors](https://azure.microsoft.com/en-us/services/spatial-anchors/) or [Fiduciary Markers]() such as QR codes.

# Getting Started

## Installation

* NuGet Packages
  - NuGet packages are our recommended way to consume MixedReality-Sharing libraries.
* Download
  - Packages can be downloaded directly from the [Releases Page](https://github.com/microsoft/MixedReality-Sharing/releases)
* Build Source
  - Clone the [github repo](https://github.com/microsoft/MixedReality-WebRTC)

## Documentation

...

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
