# Running and Debugging

## Build Settings

Build Settings are the main entry point for building and running your project. For example, in the TinyRacing project, inspecting the "Build/Windows-DotNet" (or Mac, etc.) build settings will show a “Build” or “Build and Run” button in the upper right. Pressing Build will build and place the result (by default) in a Build folder at the top level of your project. Build and Run will build and (if possible on the target platform) run the resulting build.

On the Web, you will have to manually spin up a web server to launch your build.  (The "http-server" npm package can help here.)  Playing directly from a file:// URL is not supported due to web browser security constraints.

## DOTS C# Project

Because of the C# and environment constraints detailed earlier, a separate C# project from the Unity one can be created that targets the .NET framework and assemblies that are used during build with the DOTS Runtime. This can be built via __Assets__ > __Open DOTS Runtime C# Project__. The project solution is named the name of the project, with "-Dots" at the end.

After a Build Setting asset is built at least once from the Editor, it will be available as a target in the DOTS C# Project. If you make code-only changes, you can build and run directly from your IDE (currently tested with Visual Studio and Rider, but will be expanded in the future).

## .NET vs il2cpp Builds

DOTS Runtime builds can be built using the regular (mono or Microsoft) .NET runtimes or Unity’s il2cpp runtime. When built for .NET, the end result is a pure .NET application (with native code shared libraries). .NET builds are intended for development, while il2cpp builds are intended for final builds.

## Debugging

After creating the DOTS C# Project with a .NET build configuration and opening it in your IDE, you can set breakpoints and run and debug as normal. __Currently, debugging C# code in il2cpp builds is not possible__. This is a significant limitation and will be fixed in an upcoming release.

## Play-in-Editor

It’s possible to play in the editor, however there are many rough spots due to the interactions between rendering, input, etc. when in the editor (via DOTS Hybrid) and standalone (via the DOTS Runtime).  You can play the TinyRacing sample in the editor, but it is not yet a seamless feature.
