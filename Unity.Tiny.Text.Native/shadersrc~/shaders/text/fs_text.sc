$input v_color0, v_texcoord0, v_mask

#include "common/common.sh"

SAMPLER2D(u_MainTex, 0);

void main()
{
    vec4 color = vec4(v_color0.rgb, v_color0.a * texture2D(u_MainTex, v_texcoord0).a);
    // some UNITY_UI_CLIP_RECT stuff
    // some UNITY_UI_ALPHACLIP stuff
    gl_FragColor = color;
}
