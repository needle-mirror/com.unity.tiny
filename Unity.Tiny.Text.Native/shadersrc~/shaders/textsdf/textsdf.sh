#define a_color a_color0

uniform lowp vec4 u_FaceColor;

//SAMPLER2D(u_MainTex, 0);

uniform mediump vec4 u_TexDimScale;
#define u_TextureWidth u_TexDimScale.x
#define u_TextureHeight u_TexDimScale.y
#define u_ScaleX u_TexDimScale.z
#define u_ScaleY u_TexDimScale.w

uniform vec4 u_ClipRect;

uniform mediump vec4 u_MiscP;
#define u_FaceDilate u_MiscP.x
#define u_GradientScale u_MiscP.y
#define u_PerspectiveFilter u_MiscP.z
#define u_Sharpness u_MiscP.w

#if false
uniform vec4 u_WorldSpaceCameraPos;

#if defined(OUTLINE_ON)
uniform lowp vec4 u_OutlineColor;
uniform lowp vec4 u_OutlineP;
#define u_OutlineWidth u_OutlineP.x
#define u_OutlineSoftness u_OutlineP.y
#endif

#if defined(UNDERLAY_ON) || defined(UNDERLAY_INNER)
uniform lowp vec4 u_UnderlayColor;
uniform mediump vec4 u_UnderlayP;
#define u_UnderlayOffsetX u_UnderlayP.x
#define u_UnderlayOffsetY u_UnderlayP.y
#define u_UnderlayDilate u_UnderlayP.z
#define u_UnderlaySoftness u_UnderlayP.w
#endif

uniform lowp vec4 u_WeightAndMaskSoftness;
#define u_WeightNormal u_WeightAndMaskSoftness.x
#define u_WeightBold u_WeightAndMaskSoftness.y

uniform mediump vec4 u_ScaleRatio;
#define u_ScaleRatioA u_ScaleRatio.x
#define u_ScaleRatioB u_ScaleRatio.y
#define u_ScaleRatioC u_ScaleRatio.z

#define u_MaskSoftnessY u_WeightAndMaskSoftness.w
#define u_MaskSoftnessX u_WeightAndMaskSoftness.z

uniform vec4 u_ScreenParams;
// x is the width of the camera’s target texture in pixels
// y is the height of the camera’s target texture in pixels,
// z is 1.0 + 1.0/width
// w is 1.0 + 1.0/height

//uniform float u_VertexOffsetX;
//uniform float u_VertexOffsetY;
//uniform vec4 u_Color;
//uniform float u_DiffusePower;

uniform mat4 u_invModel0; // unity_WorldToObject
#endif

#if false
// Transforms direction from world to object space
vec3 UnityWorldToObjectDir(vec3 dir)
{
    return normalize(mul(mat3(u_invModel0), dir));
}

vec3 UnityObjectToWorldDir(vec3 dir)
{
    return normalize(mul(mat3(u_model[0]), dir));
}

// Transforms normal from object to world space
vec3 UnityObjectToWorldNormal(vec3 norm)
{
//#ifdef UNITY_ASSUME_UNIFORM_SCALING
    return UnityObjectToWorldDir(norm);
//#else
//    // mul(IT_M, norm) => mul(norm, I_M) => {dot(norm, I_M.col0), dot(norm, I_M.col1), dot(norm, I_M.col2)}
//    return normalize(mul(norm, (float3x3)unity_WorldToObject));
//#endif
}

// Computes world space view direction, from object space position
vec3 UnityWorldSpaceViewDir(vec3 worldPos)
{
    return u_WorldSpaceCameraPos.xyz - worldPos;
}

// Computes world space view direction, from object space position
// *Legacy* Please use UnityWorldSpaceViewDir instead
vec3 WorldSpaceViewDir(vec4 localPos)
{
    vec3 worldPos = mul(u_model[0], localPos).xyz;
    return UnityWorldSpaceViewDir(worldPos);
}
#endif
