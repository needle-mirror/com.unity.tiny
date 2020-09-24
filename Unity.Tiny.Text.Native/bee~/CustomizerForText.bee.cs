using System.Collections.Generic;
using Bee.Toolchain.Xcode;
using JetBrains.Annotations;
using Bee.NativeProgramSupport;

[UsedImplicitly]
class CustomizerForTinyTextNative : AsmDefCSharpProgramCustomizer
{
    public override string CustomizerFor => "Unity.Tiny.Text.Native";

    public override string[] ImplementationFor => new[] {"Unity.Tiny.Text"};
}

