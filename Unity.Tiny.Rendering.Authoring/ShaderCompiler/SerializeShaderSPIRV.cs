using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Unity.Tiny.ShaderCompiler
{
    static class SerializeShaderSPIRV
    {
        internal static byte[] CreateSerializedBgfxShader(CompiledShader compiled, Stage stage)
        {
            // TODO SMOL-V compression and shader stage extraction
            throw new NotImplementedException();

#if false
            // Export bgfx file
            bool isVertexShader = stage == Stage.Vertex;
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            { 
                // Header
                bw.Write(isVertexShader ? BgfxHelper.BGFX_CHUNK_MAGIC_VSH : BgfxHelper.BGFX_CHUNK_MAGIC_FSH);

                // Input/output hashes used for validating that vertex output matches fragment input
                bw.Write(0u);
                bw.Write(0u);

                uint uniformBufferSize = SerializeUniforms(compiled, bw);

                bw.Write((uint)compiled.shader.outputCode.Length);
                bw.Write(compiled.shader.outputCode);
                byte nul = 0;
                bw.Write(nul);

                // vertex attributes
                if (isVertexShader)
                {
                    bw.Write((byte)compiled.inputs.Count);
                    foreach (var input in compiled.inputs)
                    {
                        BgfxHelper.Attrib attrib = UnityToBgfx.ConvertAttribute(input.src);
                        if (attrib != BgfxHelper.Attrib.Count)
                        {
                            bw.Write(BgfxHelper.s_attribToId[attrib]);
                        }
                        else
                        {
                            bw.Write(ushort.MaxValue);
                        }
                    }
                }
                else
                {
                    bw.Write((byte)0);
                }

                // constant buffer size
                bw.Write(uniformBufferSize);

                return ms.ToArray();
            }
#endif
        }
#if false
        // TODO can determine which uniform buffers are used in which stage based on binding index (see 'Create' in GpuProgramsVk.cpp)
        // if (binding.GetShaderStageFlags() != VK_SHADER_STAGE_FRAGMENT_BIT) // *only* used in fragment shader
        static uint SerializeUniforms(CompiledShader compiled, BinaryWriter writer)
        {
            int totalConstants = 0;
            foreach (var constantBuffer in compiled.constantBuffers)
            {
                totalConstants += constantBuffer.usedConstants;
            }
            writer.Write((ushort)totalConstants);

            int size = 0;
            foreach (var constantBuffer in compiled.constantBuffers)
            {
                for (int i = 0; i < constantBuffer.usedConstants; i++)
                {
                    var constant = constantBuffer.constants[i];
                    UnityToBgfx.ConvertBuiltInUniform(constant.name, out string uniformName);
                    writer.Write((byte)uniformName.Length);
                    writer.Write(Encoding.UTF8.GetBytes(uniformName));

                    if (constant.dataType != UnityHelper.ShaderParamType.kShaderParamFloat)
                        throw new NotSupportedException("Only float types are supported for uniforms.");

                    // Data type
                    var type = UnityToBgfx.GetBgfxUniformDataType(constant);
                    writer.Write((byte)type); // TODO bx::write(_writer, uint8_t(un.type | fragmentBit));

                    //  array size
                    writer.Write((byte)constant.arraySize); // Should be 0 if not an array

                    // regIndex - bgfx puts all constants into a single buffer
                    writer.Write((ushort)(size + constant.idx)); // TODO

                    // regCount
                    var regCount = constant.arraySize;
                    if (type == BgfxHelper.UniformType.Mat3)
                        regCount *= 3;
                    else if (type == BgfxHelper.UniformType.Mat4)
                        regCount *= 4;
                    writer.Write((ushort)regCount);

                    // TODO this is only used in renderer_webgpu.cpp?
                    // texComponent
                    // texDimension
//                    writer.Write((byte)0);
//                    writer.Write((byte)0);
                }

                size += constantBuffer.size;
            }

            return (uint)size;
    }

    enum VKShaderType
        {   // Order must match with the CGBatch shader compiler type enum
            kVKShaderVertex = 0,
            kVKShaderFragment = 1,
            kVKShaderTessControl = 2,
            kVKShaderTessEval = 3,
            kVKShaderGeometry = 4,
            kVKShaderRayTracing = 5,
            kVKShaderCount
        }

        struct ShaderFormatHeader
        {
            internal UInt32 offsetBytes;
            internal UInt32 size;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ShaderSetHeader
        {
            internal UInt32 flags;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)VKShaderType.kVKShaderCount)]
            internal ShaderFormatHeader[] shaders;
        }

        static void ExtractShaderStages(CompiledShader compiled, out CompiledShader compiledVertex, out CompiledShader compiledFragment)
        {
            using (var stream = new MemoryStream(compiled.shader.outputCode))
            {
                BinaryFormatter bin = new BinaryFormatter();
                ShaderSetHeader shaderHeader = (ShaderSetHeader)bin.Deserialize(stream);

                var headerVertex = shaderHeader.shaders[(int)VKShaderType.kVKShaderVertex];
                if (headerVertex.offsetBytes == 0 || headerVertex.size == 0)
                {

                }


            }




            compiledVertex = new CompiledShader();
            compiledVertex.Init();
            compiledVertex.attributes = compiled.attributes;
            //compiledVertex.constantBuffers = compiled.constantBuffers;
            compiledVertex.shader = new Shader { /*outputCode = Encoding.UTF8.GetBytes(vertSrc)*/ };
            compiledFragment = new CompiledShader();
            compiledFragment.Init();
            //compiledFragment.constantBuffers = compiled.constantBuffers;
            compiledFragment.shader = new Shader { /*outputCode = Encoding.UTF8.GetBytes(fragSrc)*/ };
    }
#endif
    }
}
