# Anatomy of a Tiny Project

A Unity project that’s compatible with the DOTS Runtime is very similar to a normal Unity project with the following differences:

1. Scenes must be composed entirely of DOTS Subscenes containing convertible components, or use Convert to Entity components on GameObjects in a top-level scene. (You can use unconverted GameObjects as authoring aides, but these are not preserved in play mode or the built application.)

2. Run-time code must be compiled into an assembly using an [Assembly Definition](https://docs.unity3d.com/Manual/ScriptCompilationAssemblyDefinitionFiles.html). 

3. Your code can only use DOTS APIs. No UnityEngine APIs are supported (with a very few exceptions for some source code compatibility, like logging).

4. Building (and Running) must be done using a Build Settings asset, with a DOTS Runtime build profile.

These requirements are discussed in more detail below. We’ll use the TinyRacing project as an example where you can see all of these requirements in action.

## Scenes and Subscenes

Because DOTS Runtime is pure DOTS, scenes must be convertible to their DOTS Entity and Component representation. There are no GameObjects or MonoBehaviours in the DOTS Runtime world. Anything that is not convertible will not exist at runtime. For more details, the [Converting Scene Data to DOTS talk](https://www.youtube.com/watch?v=TdlhTrq1oYk) from 2019 Unite Copenhagen gives more information.

__This area is under heavy development__. The setup described below will be simplified in future releases. Additionally, much better guidance will be given in the editor as to what can and can’t be converted.

Currently, a Unity scene for Project Tiny must have at least one Subscene. The Unity scene should have nothing in it other than subscenes, as any game objects here will not be automatically converted. Everything inside each subscene is automatically converted at build time. 

In TinyRacing, the scene in "Scenes/TinyRacing.scene" contains a single subscene (the “DOTS Subscene” scene).

## GameObject and Behaviour Authoring

With the GameObject conversion authoring workflow, DOTS runtime data is created from GameObjects and MonoBehaviours.  A portion of the standard behaviours and assets have conversion code defined -- for example, MeshRenderers, MeshFilters, Material and Texture assets.  However, your game logic needs to be defined entirely using ECS components and executed using ECS systems.

A detailed list of supported behaviours is coming soon.  Additionally, we’re working on extending the editor to provide guidance in the UI around what’s supported and how it will be converted.

## The "Root Assembly"

DOTS Runtime, and with it Project Tiny, only supports pure DOTS code. All functionality provided has been designed with DOTS in mind and takes full advantage of DOTS, C# Jobs, and Burst. In order to enforce the separation of pure DOTS code from code that references UnityEngine types or only works in the Unity Editor, your game scripts must be compiled into one or more assemblies defined using [Assembly Definition](https://docs.unity3d.com/Manual/ScriptCompilationAssemblyDefinitionFiles.html) assets. You must designate one Assembly Definition as the __Root Assembly__ in your Build Settings asset. 

The root assembly must reference any assemblies containing the classes and types your code uses. For example, it must reference Unity.Entities for core DOTS functionality, but also may reference Unity.Tiny.Input and Unity.Tiny.Rendering to gain access to the input and rendering functionality provided by Project Tiny. If you create additional assemblies for your project, they also must be referenced by the root assembly, either directly or through other referenced assemblies. Any scripts that are not part of an assembly are not included in your app.

See [Assembly Definition properties](https://docs.unity3d.com/Manual/class-AssemblyDefinitionImporter.html) for information about setting references and other assembly properties.

In TinyRacing, the root assembly is defined by the TinyRacing.asmdef asset in the "Scripts/TinyRacing" folder.

#### DOTS .NET Subset

All code written for DOTS Runtime is built against a subset of the .NET framework optimized for code size and performance. Much of standard .NET is not available. __This area is under heavy development, and what is and is not available in this profile is not finalized. __Our goal is to provide chunks of opt-in functionality to provide maximum flexibility in what is and isn’t used by your projects. Thus, just because your code builds using the Unity C# Project, does not guarantee that it will build using the DOTS Runtime build system.

Instead, you can generate a separate DOTS C# Project, which targets the limited .NET subset. See [below](#heading=h.flt05qfyo528) for more information about this project.

Notable limitations of the DOTS .NET subset include:

* No runtime reflection (System.Reflection and friends)
* No heavyweight functionality (e.g. System.Xml)
* No non-generic collections (List<T> is available, List is not)
* Many of the more complex generic collections are missing

    Note that while some of these collections may be added in the future, more efficient collections are already available as part of Unity.Collections, including: NativeList, NativeSet, NativeHashMap, NativeMultiHashMap

* No generic ToString and Equals functionality (each type must explicitly define version of these functions when needed)

An explicit overview of what is and isn’t included will be available with a future release.

## Build Settings

You build DOTS Runtime and Project Tiny apps with the new Build Settings mechanism instead of the usual Unity Build Settings window. 

The new Build Settings are assets that live in your project that define a build: the individual settings and the steps (Build Pipeline) used to execute those steps. It lets you easily define multiple build configurations and store them with your project, as well as extend with your own build data and build steps. Build Settings can inherit their settings from another Build Settings assets.

Build Settings can be used to create DOTS Runtime builds or classic Unity builds, based on the Build Profile component present in the Build Settings asset. Build Settings can be inspected to view their data, as well as trigger builds.

_This area is under heavy development._ While the Build Settings functionality is very powerful, its design is still a preview. It may change significantly in the future, but the core functionality will remain the same. The UI and overall user experience will also be significantly improved.

In TinyRacing, various Build Settings assets are defined in the "Build" folder. 

## Authoring and Editor-only Code

The DOTS GameObject authoring workflow requires that authoring happens using GameObjects and MonoBehaviours. These are converted to an ECS representation using conversion systems. For your own components, there are three ways to define the authoring MonoBehaviour and runtime components. In increasing order of complexity (and power):

* GenerateAuthoringComponent attribute. Any IComponentData can have a [GenerateAuthoringComponent] attribute placed on it. This will automatically generate a MonoBehaviour that can be used for authoring with the same fields as those present in the component.

* A MonoBehaviour that implements IConvertGameObjectToEntity. This interface defines a Convert method that will be called on the MonoBehaviour and must create any ECS components it needs using the provided EntityManager and Entity.

* A GameObjectConversionSystem. This is the lowest level and most powerful mechanism. These systems execute at conversion time and have a full view of both the original Unity Scene data as well as the destination ECS EntityManager data.

For all of these approaches, the IComponentData to be used at runtime *must be defined in an assembly that’s built as part of the DOTS Runtime build*: either the Root Assembly or one referenced by it.

For IConvertGameObjectToEntity and GameObjectConversionSystem, the MonoBehaviour or conversion system code *must be in a separate assembly that is not referenced by the Root Assembly*. (Compilation errors at build time will result if this isn’t true.)  Unity recommends that you add a ".Authoring" suffix in the name of these assemblies.

In the TinyRacing project, you can see a mix of GenerateAuthoringComponent used directly in the TinyRacing assembly, as well as a separate TinyRacing.Authoring assembly that defines some more complex conversion code.  It uses a custom GameObjectConversionSystem to handle some hand-baked UI and textures for displaying numbers at runtime.
