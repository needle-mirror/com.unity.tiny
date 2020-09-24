using System;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Tiny.Rendering
{
    public static class BuiltInShaderType
    {
        public static readonly Hash128 simple = new Hash128("c3e8321c7ca44f2dbcb8a097392bd5be");
        public static readonly Hash128 simplegpuskinning = new Hash128("6C669D74191549A58E2EF605FEB5A278");
        public static readonly Hash128 simplelit = new Hash128("5d60ab8152dc455eab1c7b8963242420");
        public static readonly Hash128 simplelitgpuskinning = new Hash128("3506F263556B491BBD2EC3B59622F0A3");
        public static readonly Hash128 line = new Hash128("03E8FA8AF56E49B794B48C6DFA8D4ED9");
        public static readonly Hash128 zonly = new Hash128("11DEAB358D184A0D9D9C8800B7E0FAF6");
        public static readonly Hash128 blitsrgb = new Hash128("5876A49959C746A7A93B6475C97745D5");
        public static readonly Hash128 shadowmap = new Hash128("AFFC8771429B4546B0048069114518B7");
        public static readonly Hash128 shadowmapgpuskinning = new Hash128("BC2BD45FD16846678911E10BE12BC081");
    }

    public struct BuiltInShader : IComponentData
    {
        /// <summary> e.g. see <see cref="BuiltInShaderType"/> </summary>
        public Hash128 Guid;

        public FixedString32 Name;
    }

    public struct CustomShader : IComponentData
    {
        public FixedString32 Name;
        public ShaderStatus Status;
    }

    public enum ShaderStatus
    {
        Invalid,
        Loaded,         // Ready! 
        Loading,        // Still loading
        LoadError,      // Failed to load
        DeviceError     // Failed to upload to gpu
    }

    /// <summary>
    /// Compiled shader stage per shader language type
    /// </summary>
    public struct PrecompiledShader
    {
        public BlobArray<byte> dx11;
        public BlobArray<byte> metal;
        public BlobArray<byte> glsles;
        public BlobArray<byte> glsl;
        public BlobArray<byte> spirv;
    }

    public struct PrecompiledShaderPipeline
    {
        public PrecompiledShader vertex;
        public PrecompiledShader fragment;
    }

    /// <summary>
    /// Blob asset reference for compiled shaders. Add next to <see cref="BuiltInShader"/> or <see cref="CustomShader"/>
    /// </summary>
    public struct ShaderBinData : IComponentData
    {
        public BlobAssetReference<PrecompiledShaderPipeline> shaders;
    }
}
