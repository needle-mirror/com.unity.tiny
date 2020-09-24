using Unity.Entities;
using Unity.Tiny;
using Unity.Tiny.Rendering;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities.Runtime.Build;
using UnityEditor;

namespace Unity.TinyConversion
{
    [UpdateInGroup(typeof(GameObjectDeclareReferencedObjectsGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    [ConverterVersion("christine-johnson", 1)]
    class MaterialDeclareAssets : GameObjectConversionSystem
    {
        protected override void OnUpdate() =>
            Entities.ForEach((UnityEngine.Material uMaterial) =>
            {
                int[] ids = uMaterial.GetTexturePropertyNameIDs();
                for (int i = 0; i < ids.Length; i++)
                {
                    DeclareReferencedAsset(uMaterial.GetTexture(ids[i]));
                }

                DeclareReferencedAsset(uMaterial.shader);
            });
    }

    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.DotsRuntimeGameObjectConversion)]
    [ConverterVersion("christine-johnson", 1)]
    public class MaterialConversion : GameObjectConversionSystem
    {
        private BlendOp GetBlending(float blend)
        {
            switch (blend)
            {
                case 0:
                    return BlendOp.Alpha;
                case 2:
                    return BlendOp.Add;
                case 3:
                    return BlendOp.Multiply;
                default:
                    return BlendOp.Alpha;
            }
        }

        private Entity GetTextureEntity(Material mat, string textureName)
        {
            if (mat.HasProperty(textureName))
                return GetPrimaryEntity(mat.GetTexture(textureName) as Texture2D);
            return Entity.Null;
        }

        private Entity GetShaderEntity(Shader shader)
        {
            var entity = GetPrimaryEntity(shader);
            if (DstEntityManager.HasComponent<CustomShader>(entity))
                return entity;
            return Entity.Null;
        }

        private bool IsTwoSided(Material uMaterial)
        {
            //both = 0, back = 1, front = 2
            if (uMaterial.GetInt("_Cull") == 2)
                return false;
            if (uMaterial.GetInt("_Cull") != 0)
                UnityEngine.Debug.LogWarning("Setting a value of Back for Render Face is not supported. Choose either Both or Front in material: " + uMaterial.name);
            return true;
        }

        private bool ContainsShaderKeyword(Material uMaterial, string keyword)
        {
            foreach (var key in uMaterial.shaderKeywords)
                if (key == keyword)
                    return true;
            return false;
        }

        private void ConvertUnlitMaterial(Entity entity, Material uMaterial)
        {
            //Do the conversion
            Vector2 textScale = uMaterial.GetTextureScale("_BaseMap");
            Vector2 textTrans = uMaterial.GetTextureOffset("_BaseMap");
            UnityEngine.Color baseColor = uMaterial.GetColor("_BaseColor").linear;

            DstEntityManager.AddComponentData<SimpleMaterial>(entity, new SimpleMaterial()
            {
                texAlbedoOpacity = GetTextureEntity(uMaterial, "_BaseMap"),
                constAlbedo = new float3(baseColor.r, baseColor.g, baseColor.b),
                constOpacity = baseColor.a,
                blend = GetBlending(uMaterial.GetFloat("_Blend")),
                twoSided = IsTwoSided(uMaterial),
                transparent = uMaterial.GetInt("_Surface") == 1,
                scale = new float2(textScale[0], textScale[1]),
                offset = AdjustTextureOffset(textTrans, textScale),
            });
        }

        private static float2 AdjustTextureOffset(Vector2 textTrans, Vector2 textScale)
        {
            // need to invert the translation in y
            return new float2(textTrans[0], (1.0f - textScale[1]) - textTrans[1]);
        }

        private void ConvertLitMaterial(Entity entity, Material uMaterial)
        {
            //Check any unsupported properties
            if (uMaterial.GetInt("_WorkflowMode") == 0)
                UnityEngine.Debug.LogWarning("Specular workflow mode is not supported on material: " + uMaterial.name);
            Texture t = uMaterial.GetTexture("_OcclusionMap");
            if (!((uMaterial.GetTexture("_OcclusionMap") as Texture2D) is null))
                UnityEngine.Debug.LogWarning("_OcclusionMap is not supported on material: " + uMaterial.name);

            //Do the conversion
            var texAlbedo = GetTextureEntity(uMaterial, "_BaseMap");
            var texMetal = GetTextureEntity(uMaterial, "_MetallicGlossMap");
            Vector2 textScale = uMaterial.GetTextureScale("_BaseMap");
            Vector2 textTrans = uMaterial.GetTextureOffset("_BaseMap");
            var shader = GetShaderEntity(uMaterial.shader);

            //Check if _Emission shader keyword has been enabled for that material
            float3 emissionColor = new float3(0.0f);
            if (uMaterial.IsKeywordEnabled("_EMISSION"))
            {
                UnityEngine.Color uEmissionColor = uMaterial.GetColor("_EmissionColor").linear;
                emissionColor = new float3(uEmissionColor.r, uEmissionColor.g, uEmissionColor.b);
            }

            UnityEngine.Color baseColor = uMaterial.GetColor("_BaseColor").linear;
            DstEntityManager.AddComponentData<LitMaterial>(entity, new LitMaterial() {
                texAlbedoOpacity = texAlbedo,
                constAlbedo = new float3(baseColor.r, baseColor.g, baseColor.b),
                constOpacity = baseColor.a,
                constEmissive = emissionColor,
                texMetal = texMetal,
                texNormal = GetTextureEntity(uMaterial, "_BumpMap"),
                texEmissive = GetTextureEntity(uMaterial, "_EmissionMap"),
                constMetal = uMaterial.GetFloat("_Metallic"),
                constSmoothness = uMaterial.GetFloat("_Smoothness"),
                normalMapZScale = uMaterial.GetFloat("_BumpScale"),
                twoSided = IsTwoSided(uMaterial),
                transparent = uMaterial.GetInt("_Surface") == 1,
                smoothnessAlbedoAlpha = ContainsShaderKeyword(uMaterial, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"),
                scale = new float2(textScale[0], textScale[1]),
                offset = AdjustTextureOffset(textTrans, textScale),
                shader = shader
            });
        }

        private void ConvertParticleLitMaterial(Entity entity, Material uMaterial)
        {
            //Do the conversion
            var texAlbedo = GetTextureEntity(uMaterial, "_BaseMap");
            var texMetal = GetTextureEntity(uMaterial, "_MetallicGlossMap");
            Vector2 textScale = uMaterial.GetTextureScale("_BaseMap");
            Vector2 textTrans = uMaterial.GetTextureOffset("_BaseMap");

            //Check if _Emission shader keyword has been enabled for that material
            float3 emissionColor = new float3(0.0f);
            if (uMaterial.IsKeywordEnabled("_EMISSION"))
            {
                UnityEngine.Color uEmissionColor = uMaterial.GetColor("_EmissionColor").linear;
                emissionColor = new float3(uEmissionColor.r, uEmissionColor.g, uEmissionColor.b);
            }

            UnityEngine.Color baseColor = uMaterial.GetColor("_BaseColor").linear;
            DstEntityManager.AddComponentData<LitMaterial>(entity, new LitMaterial()
            {
                texAlbedoOpacity = texAlbedo,
                constAlbedo = new float3(baseColor.r, baseColor.g, baseColor.b),
                constOpacity = baseColor.a,
                constEmissive = emissionColor,
                texMetal = texMetal,
                texNormal = GetTextureEntity(uMaterial, "_BumpMap"),
                texEmissive = GetTextureEntity(uMaterial, "_EmissionMap"),
                constMetal = uMaterial.GetFloat("_Metallic"),
                constSmoothness = uMaterial.GetFloat("_Smoothness"),
                normalMapZScale = uMaterial.GetFloat("_BumpScale"),
                twoSided = IsTwoSided(uMaterial),
                transparent = uMaterial.GetInt("_Surface") == 1,
                smoothnessAlbedoAlpha = ContainsShaderKeyword(uMaterial, "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"),
                scale = new float2(textScale[0], textScale[1]),
                offset = AdjustTextureOffset(textTrans, textScale)
            });
        }

        internal enum SupportedMaterialType
        {
            Unlit,
            Lit,
            ParticleUnlit,
            ParticleLit,
            Other,
            Custom
        }

        internal static SupportedMaterialType GetMaterialType(UnityEngine.Shader uShader)
        {
            switch (uShader.name)
            {
                case "Universal Render Pipeline/Unlit":
                    return SupportedMaterialType.Unlit;
                case "Universal Render Pipeline/Lit":
                    return SupportedMaterialType.Lit;
                case "Universal Render Pipeline/Particles/Unlit":
                    return SupportedMaterialType.ParticleUnlit;
                case "Universal Render Pipeline/Particles/Lit":
                    return SupportedMaterialType.ParticleLit;
                case "Sprites/Default": // Sprite material conversion is handled by Unity.U2D.Entities.MaterialProxyConversion
                case "Universal Render Pipeline/2D/Sprite-Lit-Default":
                    return SupportedMaterialType.Other;
                default:
                    return SupportedMaterialType.Custom;
            }
        }

        protected override void OnUpdate()
        {
            Entities.ForEach((UnityEngine.Material uMaterial) =>
            {
                var entity = GetPrimaryEntity(uMaterial);
                switch (GetMaterialType(uMaterial.shader))
                {
                    case SupportedMaterialType.Unlit:
                    case SupportedMaterialType.ParticleUnlit:
                        ConvertUnlitMaterial(entity, uMaterial);
                        break;
                    case SupportedMaterialType.Lit:
                        ConvertLitMaterial(entity, uMaterial);
                        break;
                    case SupportedMaterialType.ParticleLit:
                        ConvertParticleLitMaterial(entity, uMaterial);
                        break;
                    case SupportedMaterialType.Other:
                        // Sprite material conversion is handled by Unity.U2D.Entities.MaterialProxyConversion
                        break;
                    case SupportedMaterialType.Custom:
                        // The only custom shaders that are currently supported are ones that have the same parameters as the lit shader
                        ConvertLitMaterial(entity, uMaterial);
                        break;
                    default:
                        //UnityEngine.Debug.LogWarning("No material conversion yet for shader " + uMaterial.shader.name);
                        break;
                }
            });
        }
    }
}
