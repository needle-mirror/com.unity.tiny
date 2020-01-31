using NUnit.Framework;
using UnityEngine;
using Unity.TinyConversion;

class TinyImage2DAuthoringTest
{
    [Test]
    public void TestTexture2DSize()
    {
        var uTexture = new Texture2D(10, 10);
        Assert.IsFalse(Texture2DExportUtils.IsPowerOfTwo(uTexture));
        uTexture = new Texture2D(256, 256);
        Assert.IsTrue(Texture2DExportUtils.IsPowerOfTwo(uTexture));
    }
    
    [Test]
    public void TestTexture2DAlpha()
    {
        var uTexture = new Texture2D(10, 10,TextureFormat.RGBAFloat,false);
        Assert.IsFalse(Texture2DExportUtils.HasAlpha(uTexture));
        uTexture = new Texture2D(10, 10, TextureFormat.ARGB32, false);
        Assert.IsTrue(Texture2DExportUtils.HasAlpha(uTexture));
    }

    [Test]
    public void TestTexture2DColor()
    {
        var uTexture = new Texture2D(10, 10);
        Assert.IsTrue(Texture2DExportUtils.HasColor(uTexture));
        uTexture = new Texture2D(10, 10, TextureFormat.Alpha8, false);
        Assert.IsFalse(Texture2DExportUtils.HasColor(uTexture));
    }
}