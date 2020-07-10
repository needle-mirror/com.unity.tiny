using JetBrains.Annotations;
using Unity.BuildSystem.NativeProgramSupport;

[UsedImplicitly]
class CustomizerForTinyIO : AsmDefCSharpProgramCustomizer
{
    public override string CustomizerFor => "Unity.Tiny.IO";

    public override string[] ImplementationFor => new [] { "Unity.Tiny.IO" };

    public override void CustomizeSelf(AsmDefCSharpProgram program)
    {
        program.NativeProgram.Defines.Add(c => ((DotsRuntimeNativeProgramConfiguration)c).CSharpConfig.PlatformBuildConfig is WebBuildConfig webBuildConfig && webBuildConfig.SingleFile, "SINGLE_FILE=1");
        program.NativeProgram.Libraries.Add(c => c.Platform is AndroidPlatform, new SystemLibrary("log"));
        program.NativeProgram.Libraries.Add(c => c.Platform is AndroidPlatform, new SystemLibrary("android"));
    }
}
