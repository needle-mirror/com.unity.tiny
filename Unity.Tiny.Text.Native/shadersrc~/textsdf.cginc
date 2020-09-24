uniform fixed4 u_FaceColor; // TODO replace with uniform from TMPro_Properties.cginc?

//sampler2D u_MainTex;

uniform half4 u_TexDimScale;
#define u_TextureWidth u_TexDimScale.x
#define u_TextureHeight u_TexDimScale.y
#define u_ScaleX u_TexDimScale.z
#define u_ScaleY u_TexDimScale.w

uniform float4 u_ClipRect;

uniform half4 u_MiscP;
#define u_FaceDilate u_MiscP.x
#define u_GradientScale u_MiscP.y
#define u_PerspectiveFilter u_MiscP.z
#define u_Sharpness u_MiscP.w

#if false
uniform float4 u_WorldSpaceCameraPos;

#if defined(OUTLINE_ON)
uniform fixed4 u_OutlineColor;
uniform fixed4 u_OutlineP;
#define u_OutlineWidth u_OutlineP.x
#define u_OutlineSoftness u_OutlineP.y
#endif

#if defined(UNDERLAY_ON) || defined(UNDERLAY_INNER)
uniform fixed4 u_UnderlayColor;
uniform half4 u_UnderlayP;
#define u_UnderlayOffsetX u_UnderlayP.x
#define u_UnderlayOffsetY u_UnderlayP.y
#define u_UnderlayDilate u_UnderlayP.z
#define u_UnderlaySoftness u_UnderlayP.w
#endif

uniform fixed4 u_WeightAndMaskSoftness;
#define u_WeightNormal u_WeightAndMaskSoftness.x
#define u_WeightBold u_WeightAndMaskSoftness.y

uniform half4 u_ScaleRatio;
#define u_ScaleRatioA u_ScaleRatio.x
#define u_ScaleRatioB u_ScaleRatio.y
#define u_ScaleRatioC u_ScaleRatio.z

#define u_MaskSoftnessY u_WeightAndMaskSoftness.w
#define u_MaskSoftnessX u_WeightAndMaskSoftness.z

uniform float4 u_ScreenParams;
// x is the width of the camera’s target texture in pixels
// y is the height of the camera’s target texture in pixels,
// z is 1.0 + 1.0/width
// w is 1.0 + 1.0/height

//uniform float u_VertexOffsetX;
//uniform float u_VertexOffsetY;
//uniform float4 u_Color;
//uniform float u_DiffusePower;
#endif