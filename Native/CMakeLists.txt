cmake_minimum_required(VERSION 3.29)
project(miniaudio)

set(CMAKE_CXX_STANDARD 26)

include_directories(${CMAKE_CURRENT_SOURCE_DIR})

# Building Shared Library
add_library(miniaudio SHARED
    library.cpp
    library.h
    Submodules/miniaudio/miniaudio.h)

# Configure settings specifically for iOS
if (CMAKE_SYSTEM_NAME STREQUAL "iOS")
    # Use Objective-C++ for library.cpp
    set_source_files_properties(library.cpp PROPERTIES LANGUAGE OBJCXX)

    # Link with AVFoundation and AudioToolbox frameworks
    target_link_libraries(miniaudio "-framework AVFoundation" "-framework AudioToolbox")

    # Mark the miniaudio target as a framework
    set_target_properties(miniaudio PROPERTIES
        FRAMEWORK TRUE
        FRAMEWORK_VERSION A
        MACOSX_FRAMEWORK_IDENTIFIER com.mackron.miniaudio
        MACOSX_FRAMEWORK_SHORT_VERSION_STRING "1.0"
        MACOSX_FRAMEWORK_BUNDLE_VERSION "1"
    )
endif()