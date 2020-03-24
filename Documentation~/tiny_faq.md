# Frequently Asked Questions

## At runtime, I get an error about "Cannot find TypeIndex for type hash"

For example: "System.ArgumentException: Cannot find TypeIndex for type hash 9701635103632511287. Ensure your runtime depends on all assemblies defining the Component types your data uses."

The conversion systems generated data that refers to a type that is not present in your output executable.  You can get the name of the actual type by looking in the build output directory (normally in the "Builds" folder inside your project folder, and then in a folder named the same as your build settings), inside “Logs/SceneExportLog.txt”.  Looking for the given TypeIndex shows:

`0x86a321b9ac79d137 -    9701635103632511287 - Unity.Rendering.RenderBounds`

Which gives you the "Unity.Rendering.RenderBounds" type name.  There are two fixes:

1. If the output is actually supposed to use this type, then ensure that the root assembly references the assembly that contains the type.

2. If the output is not supposed to use this type, then you can filter out the conversion system that is generating it.  The conversion system is not properly set up to filter itself, which is a bug in that conversion system.  In this particular instance, this type comes from the Hybrid rendering package (which is used with DOTS Hybrid mode only).  You can add a "Conversion System Filter" build settings component to work around this:

    To add a "Conversion System Filter Settings" component:
      1. Select the BuildSettings asset for the project (in the Tiny samples, we put the **Conversion System Filter Settings** in a "common" asset so that it is shared by all the other build settings).
      2. Click the Plus **+** button at the ottom of the Inspector to open the **Add Component** menu.
      3. Choose **Conversion System Filter Settings** under **Unity.Entities.Conversion**.
      4. In the new component, set the **Size** to 1.
      5. Click the asset picker at the right of the field.
      6. Find and select `Unity.Rendering.Hybrid` in the first slot. (You can type the package name in the picker dialog to filter the list.)

    Why?  Currently, we don’t have a clean separation between what conversion systems should run when building for DOTS Runtime/Project Tiny vs what should run when building for DOTS Hybrid.  We are explicitly indicating that we do not want the hybrid rendering conversion systems to be executed when generating data for the DOTS Runtime build.  (This will be resolved in a future release.)

## I’m setting up a new project and trying to play in the editor, and nothing renders.

In order to render the entities-based representation, you need to install the "Hybrid Rendering" package in the editor.  However, this package is not optimized for compatibility with the DOTS Runtime and Project Tiny.  You will need to do additional setup to ensure that builds work (see previous question around issues regarding “Cannot find TypeIndex for type hash”).  Rendering will look different in the editor than in the runtime.

