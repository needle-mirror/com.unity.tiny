using Bee.Toolchain.Xcode;
using JetBrains.Annotations;
using Unity.BuildSystem.NativeProgramSupport;
using static Unity.BuildSystem.NativeProgramSupport.NativeProgramConfiguration;
using Bee.Core;
using NiceIO;

[UsedImplicitly]
class CustomizerForTinyImage2DNative : AsmDefCSharpProgramCustomizer
{
    public override string CustomizerFor => "Unity.Tiny.Image2D.Native";

    public override string[] ImplementationFor => new [] { "Unity.Tiny.Image2D" };

    public override void CustomizeSelf(AsmDefCSharpProgram program)
    {
        program.NativeProgram.Libraries.Add(IsWindows, new SystemLibrary("opengl32.lib"));
        program.NativeProgram.Libraries.Add(c => c.Platform is MacOSXPlatform, new SystemFramework("OpenGL"));
        program.NativeProgram.Libraries.Add(IsLinux, new SystemLibrary("GL"));
        program.NativeProgram.IncludeDirectories.Add(program.MainSourcePath.Combine("cpp~/libwebp"));
    }
}


public static class WebPBuild
{
    //Run ./bee build-lib-webp to compile the webp library located in Unity.Tiny.Image2D.Native.cpp~/libwebp and copy it to Unity.Tiny.Image2D.Authoring/libwebp
    public static void SetupWebPAlias()
    {
        var asmdefRoot = AsmDefConfigFile.AsmDefDescriptionFor("Unity.Tiny.Image2D.Native").Directory;
        var outputDirectory = AsmDefConfigFile.AsmDefDescriptionFor("Unity.Tiny.Image2D.Authoring").Directory;
        NativeProgram WebPLib = new NativeProgram("libwebp")
        {
            Sources = {
                asmdefRoot.Combine("cpp~/libwebp"),
            },
            IncludeDirectories = { asmdefRoot.Combine("cpp~/libwebp") },
            OutputDirectory = { outputDirectory.Combine("libwebp") }
        };

        DotsRuntimeNativeProgramConfiguration config = DotsConfigs.HostDotnet.NativeProgramConfiguration;
        var builtWebPLib = WebPLib.SetupSpecificConfiguration( config, config.ToolChain.DynamicLibraryFormat);

        Backend.Current.AddAliasDependency("build-lib-webp", builtWebPLib.Path);
    }
}


