$input a_position, a_color0, a_texcoord0
$output v_vertex, v_faceColor, v_param, v_texcoord0

#include "common/common.sh"
#include "textsdf.sh"

void main()
{
	vec4 vert = a_position;
	//vert.x += u_VertexOffsetX;
	//vert.y += u_VertexOffsetY;
    vec4 vPosition = mul(u_modelViewProj, vec4(vert.xyz, 1.0));

	vec2 pixelSize = vPosition.ww;

#if BGFX_SHADER_LANGUAGE_HLSL
	pixelSize *= abs(mul((mat2)u_proj, u_viewTexel.xy));
#else
	pixelSize *= abs(mul(mat2(u_proj), u_viewTexel.xy));
#endif

	float scale = inversesqrt(dot(pixelSize, pixelSize));
	scale *= /*abs(input.texcoord1.y) **/ u_GradientScale * (u_Sharpness + 1.0);

#if defined(OUTLINE_ON)
	scale /= 1.0f + (u_OutlineSoftness * u_ScaleRatioA * scale);
	float outline = u_OutlineWidth * u_ScaleRatioA * 0.5f * scale;
#else
	float outline = 0.0f;
#endif

	float bias = 0.5f * scale - 0.5f;

	float opacity = a_color0.a;

	lowp vec4 faceColor = vec4(a_color0.rgb, opacity) * u_FaceColor;
	faceColor.rgb *= faceColor.a;

#if defined(OUTLINE_ON)
	lowp vec4 outlineColor = u_OutlineColor;
	outlineColor.a *= opacity;
	outlineColor.rgb *= outlineColor.a;
	outlineColor = mix(faceColor, outlineColor, sqrt(min(1.0f, (outline * 2.0f))));
#endif

	gl_Position = vPosition;
	v_faceColor = faceColor;
	v_texcoord0 = vec4(a_texcoord0, 0.0f, 0.0f);
	v_param = vec4(scale, bias - outline, bias + outline, bias);

#if defined(OUTLINE_ON)
	v_outlineColor = outlineColor;
#endif
}
