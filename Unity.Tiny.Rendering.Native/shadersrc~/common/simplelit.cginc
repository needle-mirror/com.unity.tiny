struct VertexOutput
{
    float4 pos : SV_POSITION;
    float4 texcoord0_metal_smoothness : TEXCOORD0;
    float3 normalVS : NORMAL;
    float3 tangentVS : TANGENT;
    float4 albedo_opacity : COLOR;
    float3 viewpos : TEXCOORD1;
    float4 light0pos : TEXCOORD2;
    float4 light1pos : TEXCOORD3;
    float4 csmlightpos : TEXCOORD4;
};

CBUFFER_START(UniformsFrag)

    // Ambient probe array contains:
    // 0 - SHAr
    // 1 - SHAg
    // 2 - SHAb
    // 3 - SHBr
    // 4 - SHBg
    // 5 - SHBb
    // 6 - SHC
    float4 u_ambientProbe[7];

    float4 u_emissive_normalz;

    float4 u_numlights; // x=simple y=mapped

    // unmapped point or directional lights
    float4 u_simplelight_posordir[8];
    float4 u_simplelight_color_ivr[8];

    // mapped light0
    float4 u_light_color_ivr0;
    float4 u_light_pos0;
    float4 u_light_mask0;

    // mapped light1
    float4 u_light_color_ivr1;
    float4 u_light_pos1;
    float4 u_light_mask1;

    float4 u_texShadow01sis;  // x=s_texShadow0.size, y=1/s_texShadow0.size, z=s_texShadow1.size, w=1/s_texShadow1.size

    // csm light (always directional)
    float4 u_csm_light_color;
    float4 u_csm_light_dir;   // view space
    float4 u_csm_texsis;      // x=s_texShadowCSM.size, y=1/s_texShadowCSM.size, z=1-2/s_texShadowCSM.size
    float4 u_csm_offset_scale[4]; // xyz = offset, w = scale

    float4 u_fogcolor;
    float4 u_fogparams;

    // debug only
    float4 u_outputdebugselect;

    // Smoothness
    float4 u_smoothness_params;

    float4x4 u_invView; // hack - direct map to bgfx predefined uniform...
CBUFFER_END

#if SHADER_API_D3D11
#   define mtxInvView u_invView
#else
#   define mtxInvView unity_MatrixInvV
#endif

#define EMULATEPCF

sampler2D s_texAlbedoOpacity; // stage 0
// pack into one? normal + smoothness
sampler2D s_texNormal;        // stage 1
// pack into one? emissive + metal
sampler2D s_texEmissive;      // stage 2
sampler2D s_texMetal;         // stage 3

// mapped light0
UNITY_DECLARE_SHADOWMAP(s_texShadow0);   // stage 4

// mapped light1
UNITY_DECLARE_SHADOWMAP(s_texShadow1);   // stage 5

UNITY_DECLARE_SHADOWMAP(s_texShadowCSM); // stage 6

#define PI 3.14159265

// unity std brdf
float OneMinusReflectivityFromMetallic(float metallic) {
    float oneMinusDielectricSpec = 1.0 - 0.04;
    return oneMinusDielectricSpec - metallic * oneMinusDielectricSpec;
}

float3 SafeNormalize(float3 inVec)
{
    float dp3 = max(0.001, dot(inVec, inVec));
    return inVec / sqrt(dp3); // no rsqrt
}

float PerceptualRoughnessToRoughness(float perceptualRoughness) {
    return perceptualRoughness * perceptualRoughness;
}

float Pow5(float x) {
    return x * x * x*x * x;
}

float3 FresnelTerm(float3 F0, float cosA) {
    float t = Pow5(1.0 - cosA);   // ala Schlick interpoliation
    return F0 + (1.0 - F0) * t;
}

float3 FresnelLerp(float3 F0, float3 F90, float cosA) {
    float t = Pow5(1.0 - cosA);   // ala Schlick interpoliation
    return lerp(F0, F90, t);
}

float DisneyDiffuse(float NdotV, float NdotL, float LdotH, float perceptualRoughness) {
    float fd90 = 0.5 + 2.0 * LdotH * LdotH * perceptualRoughness;
    float lightScatter = (1.0 + (fd90 - 1.0) * Pow5(1.0 - NdotL));
    float viewScatter = (1.0 + (fd90 - 1.0) * Pow5(1.0 - NdotV));
    return lightScatter * viewScatter;
}

float SmithJointGGXVisibilityTerm(float NdotL, float NdotV, float roughness) {
    // Approximation of the above formulation (simplify the sqrt, not mathematically correct but close enough)
    float a = roughness;
    float lambdaV = NdotL * (NdotV * (1.0 - a) + a);
    float lambdaL = NdotV * (NdotL * (1.0 - a) + a);
    return 0.5 / (lambdaV + lambdaL + 1e-5);
}

float GGXTerm(float NdotH, float roughness) {
    float a2 = roughness * roughness;
    float d = (NdotH * a2 - NdotH) * NdotH + 1.0;
    return (1.0 / PI) * a2 / (d * d + 1e-7); // This function is not intended to be running on Mobile, therefore epsilon is smaller than what can be represented by half
}

float LightMask(float3 ndcpos, float4 params)
{
    float2 s = params.xy * ndcpos.xy;
    return min ( max ( params.z - dot(s, s), params.w ), 1.0 );
}

void AddOneLight(float3 lightdir, float3 viewdir, float3 normalVS, float nv, float perceptualRoughness, float roughness, float3 lightcolor, float3 spec, inout float3 diffsum, inout float3 specsum )
{
    float3 floatdir = SafeNormalize(float3(lightdir) + viewdir);
    float nh = saturate(dot(normalVS, floatdir));
    float nl = saturate(dot(normalVS, lightdir));

    float lv = saturate(dot(lightdir, viewdir));
    float lh = saturate(dot(lightdir, floatdir));

    float diffuseTerm = DisneyDiffuse(nv, nl, lh, perceptualRoughness) * nl;
    float V = SmithJointGGXVisibilityTerm(nl, nv, roughness);
    float D = GGXTerm(nh, roughness);
    float specularTerm = V * D;
    specularTerm = max(0.0, specularTerm * nl);

    // To provide true Lambert lighting, we need to be able to kill specular completely.
    // specularTerm *= any(spec) ? 1.0 : 0.0;

    diffsum += lightcolor * diffuseTerm;
    specsum += specularTerm * lightcolor * FresnelTerm(spec, lh);
}

float bilinearMix(float4 s, float2 coord, float texSize) { // TODO built in?
    float2 fr = frac(coord * texSize);
    float2 s2 = lerp(s.xy, s.zw, fr.x);
    return lerp(s2.x, s2.y, fr.y);
}

// this is pretty crazy that we have to write it like this,
// but shaderc has no goo type for shadow samplers, so we can not
// pass them as function arguments

#define SAMPLEFOURSHADOW(_sampler, _coord, _d)\
    float4 (\
        UNITY_SAMPLE_SHADOW(_sampler, _coord),\
        UNITY_SAMPLE_SHADOW(_sampler, _coord + float3(0.0, _d, 0.0)),\
        UNITY_SAMPLE_SHADOW(_sampler, _coord + float3(_d, 0.0, 0.0)),\
        UNITY_SAMPLE_SHADOW(_sampler, _coord + float3(_d, _d, 0.0)))

// Note: we use a "less than" comparison sampler for shadows but iOS Metal shaders that go through HLSLcc use a fixed "greater than" shadow sampler declared in the shader.
// We can't just change our samplers to use "greater than" because bgfx injects a "less than" function into GLSL shaders when shadow samplers aren't supported (on web).
// So we special-case metal shaders here.
#ifdef EMULATEPCF
    #ifdef SHADER_API_METAL
        #define shadow2DPCF(_sampler, _coord, _texsize, _invtexsize) 1.0 - bilinearMix(SAMPLEFOURSHADOW(_sampler,_coord,_invtexsize), _coord.xy, _texsize)
    #else
        #define shadow2DPCF(_sampler, _coord, _texsize, _invtexsize) bilinearMix(SAMPLEFOURSHADOW(_sampler,_coord,_invtexsize), _coord.xy, _texsize)
    #endif
#else
    #ifdef SHADER_API_METAL
        #define shadow2DPCF(_sampler, _coord, _texsize, _invtexsize) 1.0 - UNITY_SAMPLE_SHADOW(_sampler, _coord)
    #else
        #define shadow2DPCF(_sampler, _coord, _texsize, _invtexsize) UNITY_SAMPLE_SHADOW(_sampler, _coord)
    #endif
#endif


bool IsInsideProj(float3 pos, float w)
{
    pos = abs(pos);
    return max(max( pos.x, pos.y), pos.z) < w;
}

bool IsInside(float2 pos, float cut)
{
    pos = abs(pos);
    return max(pos.x,pos.y) < cut;
}

float3 AdjustCascadePos(float3 pos, float dx, float dy)
{
    pos.xy = pos.xy * 0.25 + float2(0.25+dx, 0.25+dy);
    return pos;
}

float3 GetCascadePos(float4 vPos, float4 offset_scale) {
    vPos.xy = vPos.xy * offset_scale.w + offset_scale.xy;
    return vPos.xyz;
}

// fogParams.x - Fog mode. Stored as flags where 0 = None, 1 = Linear, 2 = Exp, 4 = Exp2
// fogParams.y - Fog density. Used for exponential and exponential squared fog
// fogParams.z - Distance from camera at which fog completely obscures scene object. Used for linear fog
// fogParams.w - Constant for 1 / (end - start), where 'start' is the distance from camera at which fog starts, and 'end' is equal to fogParams.z. Used for linear fog
float ComputeFogFactor(float z, float4 fogParams)
{
    // The fractional amount of original color that should remain after applying fog
    float factor;

    if (fogParams.x == 1.0)
    {
        // Linear
        // factor = (end-z)/(end-start)
        factor = (fogParams.z - z) * fogParams.w;
    }
    else
    {
        float expPower = fogParams.y * z;
        if (fogParams.x == 2.0)
        {
            // Exponential
            // factor = exp(-density*z)
            factor = exp2(-expPower);
        }
        else if (fogParams.x == 4.0)
        {
            // Exponential squared
            // factor = exp(-(density*z)^2)
            factor = exp2(-expPower * expPower);
        }
        else
        {
            factor = 1.0;
        }
    }

    return saturate(factor);
}

float3 MixFogColor(float3 fragColor, float3 fogColor, float z, float4 fogParams)
{
    float fogFactor = ComputeFogFactor(z, fogParams);
    fragColor = lerp(fogColor, fragColor, fogFactor);
    return fragColor;
}


float3 TangentToViewSpace(float3 normal, float3 t, float3 b, float3 n)
{
    return float3(
        dot(float3(t.x, b.x, n.x), normal),
        dot(float3(t.y, b.y, n.y), normal),
        dot(float3(t.z, b.z, n.z), normal));
}

// Ref: "Efficient Evaluation of Irradiance Environment Maps" from ShaderX 2
float3 SHEvalLinearL0L1(float3 N, float4 shAr, float4 shAg, float4 shAb)
{
    float4 vA = float4(N, 1.0);

    float3 x1;
    // Linear (L1) + constant (L0) polynomial terms
    x1.r = dot(shAr, vA);
    x1.g = dot(shAg, vA);
    x1.b = dot(shAb, vA);

    return x1;
}

float3 SHEvalLinearL2(float3 N, float4 shBr, float4 shBg, float4 shBb, float4 shC)
{
    float3 x2;
    // 4 of the quadratic (L2) polynomials
    float4 vB = N.xyzz * N.yzzx;
    x2.r = dot(shBr, vB);
    x2.g = dot(shBg, vB);
    x2.b = dot(shBb, vB);

    // Final (5th) quadratic (L2) polynomial
    float vC = N.x * N.x - N.y * N.y;
    float3 x3 = shC.rgb * vC;

    return x2 + x3;
}

float3 SampleSH9(float4 SHCoefficients[7], float3 N)
{
    float4 shAr = SHCoefficients[0];
    float4 shAg = SHCoefficients[1];
    float4 shAb = SHCoefficients[2];
    float4 shBr = SHCoefficients[3];
    float4 shBg = SHCoefficients[4];
    float4 shBb = SHCoefficients[5];
    float4 shCr = SHCoefficients[6];

    // Linear + constant polynomial terms
    float3 res = SHEvalLinearL0L1(N, shAr, shAg, shAb);

    // Quadratic polynomials
    res += SHEvalLinearL2(N, shBr, shBg, shBb, shCr);

    return res;
}

// Samples SH L0, L1 and L2 terms
float3 SampleSH(float3 normalWS)
{
    // LPPV is not supported in Ligthweight Pipeline
    return max(float3(0, 0, 0), SampleSH9(u_ambientProbe, normalWS));
}

float3 AddLights(float4 albedo_opacity, float opacity, float3 viewpos, float3 normalVS, float2 uv, float2 metal_smoothness, float4 light0pos, float4 light1pos, float4 csmlightpos)
{
    // metal to specular
    float4 texValueMetal = tex2D(s_texMetal, uv);
    float metal = texValueMetal.x * metal_smoothness.x;
    float smoothness = (opacity * u_smoothness_params.x + texValueMetal.w * u_smoothness_params.y) * metal_smoothness.y;
    float3 spec = lerp(float3(0.04, 0.04, 0.04), albedo_opacity.xyz, metal);
    float oneMinusReflectivity = OneMinusReflectivityFromMetallic(metal);
    albedo_opacity.xyz = albedo_opacity.xyz * oneMinusReflectivity;

    float3 viewdir = -normalize(viewpos); // view space

    float3 normalWS  = mul(mtxInvView, float4(normalVS, 0.0)).xyz;
    float3 diffsum = SampleSH(normalWS);
    float3 specsum = float3(0.0, 0.0, 0.0);

    // shade
    float perceptualRoughness = 1.0 - smoothness;
    float nv = abs(dot(normalVS, viewdir));
    float roughness = PerceptualRoughnessToRoughness(perceptualRoughness);
    roughness = max(roughness, 0.002);

    // float surfaceReduction = 1.0 / (roughness*roughness + 1.0);
    // float grazingTerm = saturate((1.0 - perceptualRoughness) + (1.0 - oneMinusReflectivity));
    // float3 diffsum = gi.diffuse;
    // float3 specsum = surfaceReduction * gi.specular * FresnelLerp(specColor, grazingTerm, nv);

    // mapped 0
    if ( u_numlights.y > 0.0 ) {
        if ( max ( max( abs(light0pos).x, abs(light0pos).y) , abs(light0pos).z) < light0pos.w ) {
            float3 lightprojp = light0pos.xyz / light0pos.w;
            float3 lightproj = lightprojp * 0.5 + float3(0.5, 0.5, 0.5);
            float shadow = shadow2DPCF(s_texShadow0, lightproj, u_texShadow01sis.x, u_texShadow01sis.y);
            if ( shadow > 0.001 ) {
                float3 lightc = u_light_color_ivr0.xyz;
                lightc *= LightMask(lightprojp, u_light_mask0);
                lightc *= shadow;
                float3 lightdir = u_light_pos0.xyz - viewpos * u_light_pos0.w;
                float distsqr = dot(lightdir,lightdir);
                lightc *= max(1.0 - distsqr * u_light_color_ivr0.w, 0.0);
                AddOneLight(normalize(lightdir), viewdir, normalVS, nv, perceptualRoughness, roughness, lightc, spec, diffsum, specsum );
            }
        }
        // mapped 1
        if ( u_numlights.y > 1.0 ) {
            if ( max ( max( abs(light1pos).x, abs(light1pos).y) , abs(light1pos).z) < light1pos.w ) {
                float3 lightprojp = light1pos.xyz / light1pos.w;
                float3 lightproj = lightprojp * 0.5 + float3(0.5, 0.5, 0.5);
                float shadow = shadow2DPCF(s_texShadow1, lightproj, u_texShadow01sis.z, u_texShadow01sis.w);
                if ( shadow > 0.001 ) {
                    float3 lightc = u_light_color_ivr1.xyz;
                    lightc *= LightMask(lightprojp, u_light_mask1);
                    lightc *= shadow;
                    float3 lightdir = u_light_pos1.xyz - viewpos * u_light_pos1.w;
                    float distsqr = dot(lightdir,lightdir);
                    lightc *= max(1.0 - distsqr * u_light_color_ivr1.w, 0.0);
                    AddOneLight(normalize(lightdir), viewdir, normalVS, nv, perceptualRoughness, roughness, lightc, spec, diffsum, specsum );
                }
            }
        }
    }

    // csm - directional only
    float3 debugcascade = float3(1.0, 1.0, 1.0); // off
    if ( u_numlights.z > 0.0 ) {
        // cascades are located at [near........|..........|...........|.......far]
        // indices:                      3            2            1            0
        // debug color:                green       yellow       orange         red
        // location:                    1,1          1,0          0,1          0,0
        float3 lightproj = GetCascadePos(csmlightpos,  u_csm_offset_scale[3]);
        if ( IsInside(lightproj.xy, u_csm_texsis.z) ) {
            // green
            lightproj = AdjustCascadePos(lightproj, 0.5, 0.5);
            debugcascade = float3(0.0,1.0,0.0); // cascade #3 green
        } else {
            lightproj = GetCascadePos(csmlightpos,  u_csm_offset_scale[2]);
            if ( IsInside(lightproj.xy, u_csm_texsis.z) ) {
                lightproj = AdjustCascadePos(lightproj, 0.5, 0.0);
                debugcascade = float3(1.0,1.0,0.0); // cascade #2 yellow
            } else {
                lightproj = GetCascadePos(csmlightpos,  u_csm_offset_scale[1]);
                if ( IsInside(lightproj.xy, u_csm_texsis.z) ) {
                    lightproj = AdjustCascadePos(lightproj, 0.0, 0.5);
                    debugcascade = float3(1.0,.5,0.0); // cascade #1 orange
                } else {
                    lightproj = GetCascadePos(csmlightpos,  u_csm_offset_scale[0]);
                    if ( IsInside(lightproj.xy, u_csm_texsis.z) ) {
                        lightproj = AdjustCascadePos(lightproj, 0.0, 0.0);
                        debugcascade = float3(1.0,0.0,0.0); // cascade #0 red
                    } else {
                        debugcascade = float3(1.0, 0.0, 1.0); // error, owhere to sample! - magenta
                    }
                }
            }
        }
        lightproj.z = lightproj.z * 0.5 + 0.5; // -1..1 -> 0..1

        float shadow = shadow2DPCF(s_texShadowCSM, lightproj, u_csm_texsis.x, u_csm_texsis.y);
        if ( shadow > 0.001 ) {
            float3 lightc = u_csm_light_color.xyz * shadow;
            AddOneLight(u_csm_light_dir.xyz, viewdir, normalVS, nv, perceptualRoughness, roughness, lightc, spec, diffsum, specsum );
        }
    }

    // directional or point lights
    int nl = int(u_numlights.x);
    for ( int i=0; i<8; i++ ) {
        // this is a really stupid hack to get around the combined limitiations of shaderc and webgl:
        // - webgl can not do non constant loops
        // - shaderc transforms loops with an "if (i>=nl) break" into a while(true) loop, which does not work in webgl
        if ( i<nl ) {
            float4 posordir = u_simplelight_posordir[i];
            float4 color_ivr = u_simplelight_color_ivr[i];
            float3 lightdir = posordir.xyz - viewpos * posordir.w; // w = 0 for directional lights
            float atten = max(1.0 - dot(lightdir,lightdir) * color_ivr.w, 0.0); // ivr = 0 for directional lights
            if ( atten > 0.001 )
                AddOneLight(normalize(lightdir), viewdir, normalVS, nv, perceptualRoughness, roughness, atten * color_ivr.xyz, spec, diffsum, specsum );
        }
    }

    // finalize
    float4 texEmissive = tex2D(s_texEmissive, uv);
    float3 c = albedo_opacity.xyz * diffsum * albedo_opacity.w + specsum + texEmissive.xyz * u_emissive_normalz.xyz;

    // debug only
    c = lerp ( c, diffsum, u_outputdebugselect.x);
    c = lerp ( c, normalWS, u_outputdebugselect.y);
    c = lerp ( c, specsum, u_outputdebugselect.z);
    c = lerp ( c, debugcascade, u_outputdebugselect.w);

    return c;
}

float4 LitFragColor(VertexOutput input)
{
    float4 texValueAlbedoOpacity= tex2D(s_texAlbedoOpacity, input.texcoord0_metal_smoothness.xy);
    float4 albedo_opacity = texValueAlbedoOpacity * input.albedo_opacity;

    // unpack unity normal maps
    float3 texNormal = UnpackNormalmapRGorAG(tex2D(s_texNormal, input.texcoord0_metal_smoothness.xy).xyzw).xyz;
    texNormal.z *= u_emissive_normalz.w;

    float3 bitangentVS = cross(input.normalVS, input.tangentVS);
    float3 normalVS = TangentToViewSpace(texNormal, input.tangentVS, bitangentVS, input.normalVS);
    normalVS = normalize(normalVS);

    float3 c = AddLights(albedo_opacity, texValueAlbedoOpacity.w, input.viewpos, normalVS, input.texcoord0_metal_smoothness.xy, input.texcoord0_metal_smoothness.zw, input.light0pos, input.light1pos, input.csmlightpos);

    // fog
    c = MixFogColor(c, u_fogcolor.rgb * albedo_opacity.w, input.viewpos.z, u_fogparams);

    return float4(c, albedo_opacity.w);

    //return float4(normal + c * 0.00001,opacity);
}

CBUFFER_START(UniformsVert)
    float4 u_albedo_opacity;
    float4 u_metal_smoothness_billboarded;
    float4x4 u_wl_light0;
    float4x4 u_wl_light1;
    float4x4 u_wl_csm;
    float4 u_texmad;
    float4x4 u_modelInverseTranspose; // TODO: float3x3 uniforms aren't aligned correctly in HLSLcc
CBUFFER_END

VertexOutput LitVert(float4 vertexPos, float2 texcoord, float4 normal, float3 tangent, float3 billboardpos, float4 color, float2 metal_smoothness)
{
    VertexOutput output;
    // billboarded
    if (u_metal_smoothness_billboarded.z == 1.0)
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

    // TODO can use built in TRANSFORM_TEX(input.texcoord, s_texColor) function if u_texmad is renamed to "<sampler_name>_ST"
    float2 tt = texcoord * u_texmad.xy + u_texmad.zw;
    float2 ms = metal_smoothness * u_metal_smoothness_billboarded.xy;
    output.texcoord0_metal_smoothness = float4(tt.x, tt.y, ms.x, ms.y);

    // TODO Unity has UNITY_MATRIX_IT_MV but it relies on built-in uniform 'unity_WorldToObject' which does not have a corresponding built-in uniform in bgfx
    float3x3 view3 = (float3x3)unity_MatrixV;
    float3x3 mvit = mul(view3, u_modelInverseTranspose);

    output.normalVS  = mul(mvit, normal).xyz;
    output.tangentVS = mul(mvit, tangent).xyz;
    output.albedo_opacity = color * u_albedo_opacity;
    output.viewpos = mul(unity_MatrixMV, vertexPos).xyz;

    float4 wspos = mul(unity_ObjectToWorld, vertexPos);  // model -> world
    output.light0pos = mul(u_wl_light0, wspos);                // world -> light0
    output.light1pos = mul(u_wl_light1, wspos);                // world -> light1
    output.csmlightpos = mul(u_wl_csm, wspos);                 // world -> csm

    return output;
}
