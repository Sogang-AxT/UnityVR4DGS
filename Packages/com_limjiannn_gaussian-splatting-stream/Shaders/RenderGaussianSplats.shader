// SPDX-License-Identifier: MIT
Shader "Gaussian Splatting/Render Splats"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            ZWrite Off
            Blend OneMinusDstAlpha One
            Cull Off
            
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma require compute
#pragma use_dxc
#pragma multi_compile_local __ _SHADOW_ON

#include "GaussianSplatting.hlsl"

StructuredBuffer<uint> _OrderBuffer;

struct v2f
{
    half4 col : COLOR0;
    float2 pos : TEXCOORD0;
    float4 vertex : SV_POSITION;
#if _SHADOW_ON
    float2 lightUV    : TEXCOORD1;
    float3 bgWorldPos : TEXCOORD2;
#endif
};

StructuredBuffer<SplatViewData> _SplatViewData;
ByteAddressBuffer _SplatSelectedBits;
uint _SplatBitsValid;

#if _SHADOW_ON
sampler2D _ShadowTex;
float4x4 _WorldToLightMatrix;
float4x4 _ShadowObjToWorld;
float _ShadowStrength;
float _LightDepthMin;
float _LightDepthRange;
float3 _ShadowLightDir;
#endif

v2f vert (uint vtxID : SV_VertexID, uint instID : SV_InstanceID)
{
    v2f o = (v2f)0;
    instID = _OrderBuffer[instID];
	SplatViewData view = _SplatViewData[instID];
	float4 centerClipPos = view.pos;
	bool behindCam = centerClipPos.w <= 0;
	if (behindCam)
	{
		o.vertex = asfloat(0x7fc00000); // NaN discards the primitive
	}
	else
	{
		o.col.r = f16tof32(view.color.x >> 16);
		o.col.g = f16tof32(view.color.x);
		o.col.b = f16tof32(view.color.y >> 16);
		o.col.a = f16tof32(view.color.y);

		uint idx = vtxID;
		float2 quadPos = float2(idx&1, (idx>>1)&1) * 2.0 - 1.0;
		quadPos *= 2;

		o.pos = quadPos;

		float2 deltaScreenPos = (quadPos.x * view.axis1 + quadPos.y * view.axis2) * 2 / _ScreenParams.xy;
		o.vertex = centerClipPos;
		o.vertex.xy += deltaScreenPos * centerClipPos.w;

		// is this splat selected?
		if (_SplatBitsValid)
		{
			uint wordIdx = instID / 32;
			uint bitIdx = instID & 31;
			uint selVal = _SplatSelectedBits.Load(wordIdx * 4);
			if (selVal & (1 << bitIdx))
			{
				o.col.a = -1;				
			}
		}

#if _SHADOW_ON
		// Light-space UV for shadow sampling.
		// LoadSplatPos handles both standard and codebook formats via _SplatFormat.
		float3 localPos = LoadSplatPos(instID);
		float3 worldPos = mul(_ShadowObjToWorld, float4(localPos, 1.0)).xyz;
		float4 lp = mul(_WorldToLightMatrix, float4(worldPos, 1.0));
		o.lightUV    = lp.xy;
		o.bgWorldPos = worldPos;
#endif
	}
	FlipProjectionIfBackbuffer(o.vertex);
    return o;
}

half4 frag (v2f i) : SV_Target
{
	float power = -dot(i.pos, i.pos);
	half alpha = exp(power);
	if (i.col.a >= 0)
	{
		alpha = saturate(alpha * i.col.a);
	}
	else
	{
		// "selected" splat: magenta outline, increase opacity, magenta tint
		half3 selectedColor = half3(1,0,1);
		if (alpha > 7.0/255.0)
		{
			if (alpha < 10.0/255.0)
			{
				alpha = 1;
				i.col.rgb = selectedColor;
			}
			alpha = saturate(alpha + 0.3);
		}
		i.col.rgb = lerp(i.col.rgb, selectedColor, 0.5);
	}
	
    if (alpha < 1.0/255.0)
        discard;

    half shadow = 1.0;
#if _SHADOW_ON
    float2 suv = i.lightUV;
    if (suv.x >= 0.0 && suv.x <= 1.0 && suv.y >= 0.0 && suv.y <= 1.0)
    {
        float2 shadowSample   = tex2D(_ShadowTex, suv).rg;
        float  shadowMask     = shadowSample.r; // 0=그림자영역, 1=그림자없음
        float  humanDepthNorm = shadowSample.g;

        float bgDepth     = dot(i.bgWorldPos, _ShadowLightDir);
        float bgDepthNorm = (bgDepth - _LightDepthMin) / max(_LightDepthRange, 0.001);

        float inShadowArea = 1.0 - shadowMask;                        // 1=그림자영역
        float behindHuman  = step(humanDepthNorm - 0.05, bgDepthNorm); // 1=Human보다 뒤
        float shadowAmount = inShadowArea * behindHuman;

        shadow = 1.0 - shadowAmount * _ShadowStrength;
    }
#endif

    half4 res = half4(i.col.rgb * alpha * shadow, alpha);
    return res;
}
ENDCG
        }
    }
}
