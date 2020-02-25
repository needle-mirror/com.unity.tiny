using NUnit.Framework;
using UnityEngine;
using Unity.Tiny;
using Unity.TinyConversion;

class TinyImage2DAuthoringTest
{
    [Test]
    public void TestTexture2D_Size()
    {
        var uTexture = new Texture2D(10, 10);
        Assert.IsFalse(Texture2DExportUtils.IsPowerOfTwo(uTexture));
        uTexture = new Texture2D(256, 256);
        Assert.IsTrue(Texture2DExportUtils.IsPowerOfTwo(uTexture));
    }
    
    [Test]
    public void TestTexture2D_Alpha()
    {
        var uTexture = new Texture2D(10, 10,TextureFormat.RGBAFloat,false);
        Assert.IsFalse(Texture2DExportUtils.HasAlpha(uTexture));
        uTexture = new Texture2D(10, 10, TextureFormat.ARGB32, false);
        Assert.IsTrue(Texture2DExportUtils.HasAlpha(uTexture));
    }

    [Test]
    public void TestTexture2D_Color()
    {
        var uTexture = new Texture2D(10, 10);
        Assert.IsTrue(Texture2DExportUtils.HasColor(uTexture));
        uTexture = new Texture2D(10, 10, TextureFormat.Alpha8, false);
        Assert.IsFalse(Texture2DExportUtils.HasColor(uTexture));
    }

    [Test]
    public void TestTexture2D_WrapModeU()
    {
        var uTexture = new Texture2D(16, 16)
        {
            wrapModeU = TextureWrapMode.Clamp
        };
        var flags = Texture2DExportUtils.GetTextureFlags(uTexture, null);
        Assert.IsTrue((flags & TextureFlags.UClamp) == TextureFlags.UClamp);
        
        uTexture = new Texture2D(16, 16)
        {
            wrapModeU = TextureWrapMode.Mirror
        };
        flags = Texture2DExportUtils.GetTextureFlags(uTexture, null);
        Assert.IsTrue((flags & TextureFlags.UMirror) == TextureFlags.UMirror);
        
        uTexture = new Texture2D(16, 16)
        {
            wrapModeU = TextureWrapMode.Repeat
        };
        flags = Texture2DExportUtils.GetTextureFlags(uTexture, null);
        Assert.IsTrue((flags & TextureFlags.URepeat) == TextureFlags.URepeat);      
    }   
    
    [Test]
    public void TestTexture2D_WrapModeV()
    {
        var uTexture = new Texture2D(16, 16)
        {
            wrapModeV = TextureWrapMode.Clamp
        };
        var flags = Texture2DExportUtils.GetTextureFlags(uTexture, null);
        Assert.IsTrue((flags & TextureFlags.VClamp) == TextureFlags.VClamp);
        
        uTexture = new Texture2D(16, 16)
        {
            wrapModeV = TextureWrapMode.Mirror
        };
        flags = Texture2DExportUtils.GetTextureFlags(uTexture, null);
        Assert.IsTrue((flags & TextureFlags.VMirror) == TextureFlags.VMirror);
        
        uTexture = new Texture2D(16, 16)
        {
            wrapModeV = TextureWrapMode.Repeat
        };
        flags = Texture2DExportUtils.GetTextureFlags(uTexture, null);
        Assert.IsTrue((flags & TextureFlags.VRepeat) == TextureFlags.VRepeat);      
    }      
    
    [Test]
    public void TestTexture2D_FilterMode()
    {
        var uTexture = new Texture2D(16, 16)
        {
            filterMode = FilterMode.Point
        };
        var flags = Texture2DExportUtils.GetTextureFlags(uTexture, null);
        Assert.IsTrue((flags & TextureFlags.Point) == TextureFlags.Point);
        
        uTexture = new Texture2D(16, 16)
        {
            filterMode = FilterMode.Bilinear
        };
        flags = Texture2DExportUtils.GetTextureFlags(uTexture, null);
        Assert.IsTrue((flags & TextureFlags.Linear) == TextureFlags.Linear);
        
        uTexture = new Texture2D(16, 16)
        {
            filterMode = FilterMode.Trilinear
        };
        flags = Texture2DExportUtils.GetTextureFlags(uTexture, null);
        Assert.IsTrue((flags & TextureFlags.Trilinear) == TextureFlags.Trilinear);        
    }    
    
    [Test]
    public void TestTexture2D_MipMap()
    {
        var uTexture = new Texture2D(16, 16, TextureFormat.RGB24, true);
        var flags = Texture2DExportUtils.GetTextureFlags(uTexture, null);
        Assert.IsTrue((flags & TextureFlags.MimapEnabled) == TextureFlags.MimapEnabled);
        
        uTexture = new Texture2D(16, 16, TextureFormat.RGB24, false);
        flags = Texture2DExportUtils.GetTextureFlags(uTexture, null);
        Assert.IsFalse((flags & TextureFlags.MimapEnabled) == TextureFlags.MimapEnabled);
    }
}