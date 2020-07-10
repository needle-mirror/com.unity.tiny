$input v_vertex, v_faceColor, v_param, v_texcoord0

#include "common/common.sh"
#include "textsdf.sh"

SAMPLER2D(u_MainTex, 0);

const float smoothval = 1.0f/16.0f;

void main()
{
    // scale and bias to expand the distance value by.  Map 0.5-s .. 0.5+s to 0 .. 1
    mediump float scale = v_param.x;
    mediump float bias = v_param.w;

    mediump float d = texture2D(u_MainTex, v_texcoord0.xy).a * scale;
    mediump vec4 c = v_faceColor * saturate(d - bias);

    #ifdef OUTLINE_ON
    c = lerp(v_outlineColor, v_faceColor, saturate(d - v_param.z));
    c *= saturate(d - v_param.y);
    #endif

    #ifdef UNDERLAY_ON
    d = texture2D(_MainTex, v_texcoord1.xy).a * v_underlayParam.x;
    c += vec4(_UnderlayColor.rgb * _UnderlayColor.a, _UnderlayColor.a) * saturate(d - v_underlayParam.y) * (1 - c.a);
    #endif

    #ifdef UNDERLAY_INNER
    mediump float sd = saturate(d - v_param.z);
    d = tex2D(_MainTex, v_texcoord1.xy).a * v_underlayParam.x;
    c += vec4(_UnderlayColor.rgb * _UnderlayColor.a, _UnderlayColor.a) * (1 - saturate(d - v_underlayParam.y)) * sd * (1 - c.a);
    #endif

    // Alternative implementation to UnityGet2DClipping with support for softness.
    #ifdef UNITY_UI_CLIP_RECT
    mediump vec2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(v_mask.xy)) * v_mask.zw);
    c *= m.x * m.y;
    #endif

    #if defined(UNDERLAY_ON) || defined(UNERLAY_INNER)
    c *= v_texcoord1.z;
    #endif

    #ifdef UNITY_UI_ALPHACLIP
    clip(c.a - 0.001);
    #endif

    gl_FragColor = c;
}
