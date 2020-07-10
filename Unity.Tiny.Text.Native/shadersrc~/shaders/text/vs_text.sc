$input a_position, a_color0, a_texcoord0
$output v_color0, v_texcoord0, v_mask

#include "common/common.sh"

//uniform float u_VertexOffsetX;
//uniform float u_VertexOffsetY;
//uniform vec4 u_Color;
//uniform float u_DiffusePower;

uniform vec4 u_ClipRect;
uniform vec4 u_MaskSoftness;

void main()
{
	vec4 vert = a_position;
	//vert.x += u_VertexOffsetX;
	//vert.y += u_VertexOffsetY;

    vec4 pos = mul(u_modelViewProj, vec4(vert.xyz, 1.0));
	v_color0 = a_color0;
	//v_color0 *= u_Color;
	//v_color0.rgb *= u_DiffusePower;
	v_texcoord0 = a_texcoord0;

	vec2 pixelSize = pos.ww;
	vec4 clampedRect = clamp(u_ClipRect, -2e10, 2e10);
    v_mask = vec4(vert.xy * 2. - clampedRect.xy - clampedRect.zw,
	              vec2(0.25, 0.25) / (0.25 * vec2(u_MaskSoftness.x, u_MaskSoftness.y) + pixelSize.xy));

	gl_Position = pos;
}
