using System.Collections.Generic;
using Bee.Toolchain.Xcode;
using JetBrains.Annotations;
using Bee.NativeProgramSupport;

[UsedImplicitly]
internal class CustomizerForTiny2D : AsmDefCSharpProgramCustomizer
{
    public override string CustomizerFor => "Unity.Tiny.2D.Native";

    public override string[] ImplementationFor => new[] {"Unity.Tiny.2D"};
}