using Bee.Toolchain.Xcode;
using JetBrains.Annotations;
using Bee.NativeProgramSupport;
using static Bee.NativeProgramSupport.NativeProgramConfiguration;
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


public class WebPBuildCustomizer : DotsBuildCustomizer
{
    //Run ./bee build-lib-webp to compile the webp library located in Unity.Tiny.Image2D.Native.cpp~/libwebp and copy it to Unity.Tiny.Image2D.Authoring/libwebp
    public override void Customize()
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
        var builtWebPLib = WebPLib.SetupSpecificConfiguration(config, config.ToolChain.DynamicLibraryFormat);

        Backend.Current.AddAliasDependency("build-lib-webp", builtWebPLib.Path);
    }
}


