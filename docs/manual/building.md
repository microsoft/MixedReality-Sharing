---
uid: building
title: Building from Source
---
# Building from source

For convenience, we supply prebuilt [nuget packages](installation.md), but the packages can also be built from source if needed.

The MixedReality-Sharing libraries are built from the `Microsoft.MixedReality.Sharing.sln` Visual Studio solution located at the root of the repository. The solution may contain more projects than are produced by official packages.

## Prerequisites

The solution uses Visual Studio 2019 with the following features:

- The MSVC v142 - VS 2019 C++ x64/x86 build tools toolchain is required to build any of the C++ libraries. This is installed by default with the Desktop development with C++ workload on Visual Studio 2019.

- For ARM support, the MSVC v142 - VS 2019 C++ ARM build tools toolchain is also required.

- The C# library requires a .NET Standard 2.0 compiler, like the Roslyn compiler available as part of Visual Studio when installing the .NET desktop development workload.

- The UWP libraries and projects require UWP support from the compiler, available as part of Visual Studio when installing the Universal Windows Platform development workload.

## Cloning the Repository

The official repository containing the source code of MixedReality-Sharing is https://github.com/microsoft/MixedReality-Sharing. Development is done on the master branch.

```cmd
git clone https://github.com/microsoft/MixedReality-Sharing
```

## Building the Libraries

1. Open the `Microsoft.MixedReality.Sharing.sln` Visual Studio solution located at the root of the freshly cloned repository.

1. Choose the Platform and Configuration

1. Then Either:
    - Build the entire solution with F7 or **Build > Build Solution**
    - Select individual project(s) to build

On successful build, the binaries will be generated in `<root>/build/bin/<Configuration>/<Platform>`
