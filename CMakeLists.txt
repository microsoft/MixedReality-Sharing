
cmake_minimum_required(VERSION 3.12)
project(MixedReality-Sharing
    VERSION 0.0.1
    DESCRIPTION "Portable APIs and components for networking and state synchronization"
    HOMEPAGE_URL https://github.com/microsoft/MixedReality-Sharing
    LANGUAGES CXX CSharp)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(CMAKE_CSharp_FLAGS "/langversion:7.1") # otherwise cmake defaults to 3

add_subdirectory("libs/Microsoft.MixedReality.Sharing.Network")
add_subdirectory("libs/Microsoft.MixedReality.Sharing.StateSync")
