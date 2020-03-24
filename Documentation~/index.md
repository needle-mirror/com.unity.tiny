# What Is Project Tiny and DOTS Runtime?

Unity embarked on Project Tiny to deliver a highly-modular runtime giving developers the tools needed to produce small, light, and fast games/experiences like Mobile, Playable Ads and Instant social experiences.

Over the lifetime of the project (see Unite Copenhagen 2019 Project Tiny roadmap [session recording](https://www.youtube.com/watch?v=kNK_niBNkMY&feature=youtu.be)), the goals have not changed but our path to achieving them has. Rather than a separate Editor mode of its own, Project Tiny aligned closer with DOTS and now shares the regular DOTS conversion authoring workflow.  This workflow allows you to work with Unity GameObjects as usual, with all their rich authoring capabilities, while converting into optimal ECS-based data at runtime. (For more information about this authoring flow, see [Converting Scene Data to DOTS](https://www.youtube.com/watch?v=TdlhTrq1oYk)).

Compared to "regular" Unity content, Project Tiny content targets the new DOTS Runtime and has no dependency on the existing UnityEngine.  The DOTS Runtime is a new execution environment focused on DOTS code, with a very lightweight small core runtime that can be extended by modules providing additional features.  Our goal is to ensure that you pay code size and execution cost only for the features that you actually use.  All functionality is provided as pure DOTS modules, delivered as assemblies, and is interacted with using DOTS and ECS methods.

Project Tiny is part of a spectrum of "regular" Unity and pure DOTS capabilities.  Our goal is to ensure that if a project is compatible with the DOTS Runtime, it also works in DOTS Hybrid / Unity. (We’re not there yet.)

## Required Versions

__Unity 2019.3.0f1__ or later is required.

__Windows__: Visual Studio with the following components installed:

	Desktop development with c++, .NET desktop development

__MacOS__: XCode installed

__Android__: Unity Android support, and Android SDK, NDK, and JDK available and configured in Unity.  All can be downloaded via the Unity Hub.

__iOS__: XCode and Unity iOS build support.

## Filing Bugs and Providing Feedback

To file bugs, please use the Unity Bug Reporter in the Unity Editor, accessible via Help > Report a Bug.  Please include "Project Tiny" in the title to help our staff triage things appropriately!  For more details on how to report a bug, please [visit this page](https://unity3d.com/unity/qa/bug-reporting).

For general feedback, please [visit the Project Tiny Forum](https://forum.unity.com/forums/project-tiny.151/).

## Getting started: use the "TinyRacing" Sample Project

The "TinyRacing" sample project is the easiest way to get all components required for Project Tiny. Just go to [https://github.com/Unity-Technologies/ProjectTinySamples](https://github.com/Unity-Technologies/ProjectTinySamples), clone/download everything, and then open the sample project in the /TinyRacing folder. You’ll notice that this repository also has a /Tiny3D folder that include a basic “HelloWorld” style project.

The TinyRacing sample project is intended as a lightweight example of the type of content you can build with Project Tiny.  It is a complete game slice showing a number of elements such as accepting input, implementing simple AI, handling collisions, and similar.  Please explore and play around with the sample project to get a feel for what developing with pure DOTS looks like.

A number of areas of TinyRacing are currently implemented in a way that is not final due to missing features, such as UI.  We’ll improve this sample project as a richer feature set becomes available.

Within the project folder, there is a "Build" folder that contains predefined build settings for a variety of platforms.  For example, if you are on Windows, you should be able to click on the “Windows-DotNet” asset and select “Build and Run” in the inspector window to build and run the sample for Windows.

## Feature Status

| Platform Support| Desktop platforms, the Web (both asmjs and WebAssembly), Android, and iOS are supported. |
|:---|:---| 
| 3D Graphics| A lightweight 3D renderer is available, in the Unity.Tiny.Rendering assembly.  The capabilities of this renderer will be expanded in the future. |
| 2D Graphics| Support for 2D will be coming in a future release. |
| Input| Lightweight input is available via the Unity.Tiny.Input assembly. |
| Audio| Lightweight audio is available via the Unity.Tiny.Audio assembly. |
| Animation| Support for animation will be coming in a future release. |
| Physics| While Unity.Physics should work, due to the lack of fixed-time updates, there are significant issues on platforms that cannot reach a high enough frame rate.  Full robust support will be coming in a future release, at which point TinyRacing will be switched over to use Unity.Physics. |
| UI| A rich UI solution will be coming during the next year.  In the meantime, we are exploring options to provide a lightweight UI solution in the short term. |
| Jobs| The Jobs API is fully supported |
| Burst| Burst is partially supported, with full support (Burst-compiled jobs) coming in a future release.  Burst is enabled for release builds only. |

