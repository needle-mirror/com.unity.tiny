# Setting up a New Project

New project setup will be greatly simplified in the near future via a Project Tiny template. For now "TinyRacing" sample project (available here  [https://github.com/Unity-Technologies/ProjectTinySamples](https://github.com/Unity-Technologies/ProjectTinySamples)) is the easiest way to get all components required for Project Tiny, but if you want to setup a project it on your own, follow the steps below.

## Project and Package Setup

1. Start with a Universal Render Pipeline project template. The Project Tiny 3D renderer will be designed to match a subset of baseline URP rendering. (Note: still very much a work in progress.)

2. Add the required base packages.
    * com.unity.entities
    * com.unity.dots.runtime
    * com.unity.tiny
    * com.unity.tiny.rendering
    * com.unity.platforms


3. Add platform packages.  (Omit whatever is not necessary for your deployment.)
    * com.unity.platforms.desktop
    * com.unity.platforms.windows 
    * com.unity.platforms.linux
    * com.unity.platforms.macos
    * com.unity.platforms.android
    * com.unity.platforms.ios
    * com.unity.platforms.web
    * com.unity.tiny.desktop
    * com.unity.tiny.web

Note: com.unity.platforms.windows is required even if you are targeting other platforms. We are working to remove this dependency in the upcoming release.


## Code Setup

1. Create a new folder inside Assets named "GameSystems".

2. Inside GameSystems, create a new Assembly Definition asset.
    1. Right-click Create and select Create > Assembly Definition.
    2. Name it "GameSystems".
    3. Uncheck "Use GUIDs".
    4. Inside the inspector, add the following assemblies as references:
      * Unity.Entities
      * Unity.Transforms
      * Unity.Mathematics
      * Unity.Tiny.Core
      * Unity.Tiny.Rendering
      * Unity.Tiny.EntryPoint
      * Unity.Tiny.Main


3. Inside GameSystems, create two C# Script files named:
      * RotateComponent.cs
      * RotateSystem.cs


4. Edit RotateComponent.cs and replace the contents with the following.  This defines a new ECS component and makes it available for use in the editor.

    ``` c#
    using Unity.Entities;

    [GenerateAuthoringComponent]
    public struct RotateComponent : IComponentData
    {
        public float Speed;
    }
    ```

5. Edit RotateSystem.cs and replace the contents with the following.  This defines a new ECS system that uses the above component to change an entity’s rotation.

    ``` c#
    using Unity.Entities;
    using Unity.Transforms;
    using Unity.Mathematics;

    public class RotateSystem : ComponentSystem
    {
        protected override void OnUpdate()
        {
            var dt = Time.DeltaTime;
            Entities.ForEach((ref Rotation rot, ref RotateComponent rc) => {
                rot.Value = math.mul(rot.Value, quaternion.RotateY(dt * rc.Speed));
            });
        }
    }
    ```

## Scene Setup

1. Create a new scene to serve as a top-level Unity scene (referred to as Main.unity)

2. In Main.unity, create a new empty GameObject to serve as the root of your subscene.

3. (optionally) Give it a name.

4. Right-click the GameObject, and select "New SubScene from Selection"

5. (optionally) Delete the empty GameObject now created as a child of your subscene.

6. Copy the "Main Camera" and “Directional Light” GameObjects into the Subscene (in the hierarchy underneath the Subscene)
    
    **Why?** Currently, cameras and lights have two different representations depending on whether they’re running in the DOTS Runtime world or the DOTS Hybrid world.  These representations are not compatible at the moment.  The camera and light *inside* the SubScene will be used in DOTS Runtime/Project Tiny builds.  The ones in the top-level Main scene will be used when previewing inside the editor. (This will be resolved in a future release.)
    
7. Create a cube inside the subscene:
    1. Right click Create > 3D Object > Cube
    2. Move the cube forward (in Z) a bit and scale it up so that it’s visible in the camera


8. Add a component to the cube -- search for "Rotate Component"

    Note that this is the component we declared earlier during code setup.

9. Set the speed to 0.2

## Build Setup

1. Create a new Build Settings asset via Create > Build > BuildSettings for DOTS Runtime

2. Drag or select your Root Assembly assembly definition into the Root Assembly field.

    Note that this is the "GameSystems" assembly created earlier during code setup

10. Add a Scene List build settings component, if one isn’t present.

    Note, to add a component, press the + button in the bottom of the inspector.

11. Add your Main.unity scene to the scene list:
    1. In the Scene List section, set the Size to 1 (or the number of scenes you want to include in the build).
    2. Drag the Main.unity scene asset to the array slot below the Size field.

    Yes, this way of interacting with arrays is clunky.  It’ll be fixed soon.

12. Select your build target and build type. For example, "Windows .NET" and “Develop”.

13. Press Build and Run in the upper right.

