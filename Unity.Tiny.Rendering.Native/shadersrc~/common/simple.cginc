struct VertexOutput
{
    float4 pos : SV_POSITION;
    float4 color : COLOR;
    float2 texcoord : TEXCOORD0;
};

uniform float4 u_color0;
uniform float4 u_texmad;
uniform float4 u_billboarded;

sampler2D s_texColor;

VertexOutput SimpleVert(float4 vertexPos, float2 texcoord, float3 billboardpos, float4 color)
{
    VertexOutput output;

    // billboarded
    if (u_billboarded.x == 1.0)
    {
        float4x4 invview = transpose(unity_MatrixInvV);
        float3 camPos = invview[3].xyz;
        float3 fromCam = normalize(billboardpos - camPos);
        float3 camUp = normalize(invview[1].xyz);
        float3 right = cross(camUp, fromCam);
        float3 up = cross(fromCam, right);
        float4x4 model = float4x4(
                    right.x, up.x, fromCam.x, billboardpos.x,
                    right.y, up.y, fromCam.y, billboardpos.y,
                    right.z, up.z, fromCam.z, billboardpos.z,
                    0.0,     0.0,  0.0,       1.0);

        float4 worldPos = mul(model, vertexPos);
        output.pos = mul(unity_MatrixVP, worldPos);
    }
    else
    {
        output.pos = UnityObjectToClipPos(vertexPos.xyz);
    }
    output.color = color * u_color0;

    // TODO can use built in TRANSFORM_TEX(input.texcoord, s_texColor) function if u_texmad is renamed to "<sampler_name>_ST"
    output.texcoord = texcoord * u_texmad.xy + u_texmad.zw;

    return output;
}

float4 SimpleFragColor(VertexOutput input)
{
    float4 c = tex2D(s_texColor, input.texcoord) * input.color;
    c.xyz *= c.w;
    return c;
}
