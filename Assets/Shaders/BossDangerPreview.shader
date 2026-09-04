Shader "FatefulRush/BossDangerPreview"
{
    Properties
    {
        _VisibilityTex ("Visibility", 2D) = "white" {}
        _DangerColor ("Danger Color", Color) = (1,0.025,0.025,0.46)

        _Origin ("Origin", Vector) = (0,0,0,0)
        _InnerRadius ("Inner Radius", Float) = 0.5
        _MaxRange ("Max Range", Float) = 10

        _UseRadius ("Use Radius", Float) = 0
        _Radius ("Radius", Float) = 3

        _InnerAlphaMultiplier ("Inner Alpha", Float) = 0.22
        _WaveFrontWidth ("Wave Front Width", Float) = 0.12
        _WaveFrontBoost ("Wave Front Boost", Float) = 0.75
        _InnerBrightness ("Inner Brightness", Float) = 0.55

        _CoverFeather ("Cover Feather", Float) = 0.08

        _StrikeWaveProgress ("Strike Wave Progress", Float) = -1
        _StrikeWaveWidth ("Strike Wave Width", Float) = 0.09
        _StrikeWaveBoost ("Strike Wave Boost", Float) = 1.55

        _Progress ("Progress", Range(0,1)) = 0
        _Opacity ("Opacity", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _VisibilityTex;

            float4 _DangerColor;
            float4 _Origin;

            float _InnerRadius;
            float _MaxRange;

            float _UseRadius;
            float _Radius;

            float _InnerAlphaMultiplier;
            float _WaveFrontWidth;
            float _WaveFrontBoost;
            float _InnerBrightness;

            float _CoverFeather;

            float _StrikeWaveProgress;
            float _StrikeWaveWidth;
            float _StrikeWaveBoost;

            float _Progress;
            float _Opacity;

            static const float PI = 3.14159265359;
            static const float TWO_PI = 6.28318530718;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;

                float4 world =
                    mul(unity_ObjectToWorld, v.vertex);

                o.positionCS =
                    UnityObjectToClipPos(v.vertex);

                o.worldPos =
                    world.xy;

                return o;
            }

            float Smooth01(float x)
            {
                x = saturate(x);
                return x * x * (3.0 - 2.0 * x);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 delta =
                    i.worldPos - _Origin.xy;

                float distanceToOrigin =
                    length(delta);

                // Bossun kendi etrafinda kirmizi yok.
                if (distanceToOrigin <= _InnerRadius)
                    discard;

                // MiniBoss local mode.
                if (_UseRadius > 0.5 &&
                    distanceToOrigin > _Radius)
                {
                    discard;
                }

                float angle =
                    atan2(delta.y, delta.x);

                float visibilityU =
                    (angle + PI) / TWO_PI;

                visibilityU =
                    frac(visibilityU);

                float normalizedVisibleDistance =
                    tex2D(
                        _VisibilityTex,
                        float2(visibilityU, 0.5)
                    ).r;

                float visibleDistance =
                    normalizedVisibleDistance *
                    _MaxRange;

                // Obstacle arkasi SAFE.
                // Feather, moving obstacle safe-area kenarini yumusatir.
                float dangerVisibility =
                    1.0 -
                    smoothstep(
                        visibleDistance - _CoverFeather,
                        visibleDistance + _CoverFeather,
                        distanceToOrigin
                    );

                if (dangerVisibility <= 0.001)
                    discard;

                float radialT =
                    saturate(
                        (distanceToOrigin - _InnerRadius) /
                        max(
                            0.001,
                            _MaxRange - _InnerRadius
                        )
                    );

                float progress =
                    saturate(_Progress);

                float wavePosition =
                    Smooth01(progress);

                float globalFade =
                    Smooth01(progress);

                float revealEnd =
                    min(
                        1.0,
                        wavePosition + _WaveFrontWidth
                    );

                float reveal;

                if (progress >= 0.999)
                {
                    reveal = 1.0;
                }
                else
                {
                    reveal =
                        1.0 -
                        smoothstep(
                            wavePosition,
                            revealEnd,
                            radialT
                        );
                }

                float spatialFade =
                    lerp(
                        _InnerAlphaMultiplier,
                        1.0,
                        Smooth01(radialT)
                    );

                float distanceFromFront =
                    abs(radialT - wavePosition);

                float front =
                    1.0 -
                    saturate(
                        distanceFromFront /
                        max(
                            0.001,
                            _WaveFrontWidth
                        )
                    );

                float alpha =
                    _DangerColor.a *
                    globalFade *
                    spatialFade *
                    reveal *
                    _Opacity *
                    dangerVisibility;

                alpha *=
                    1.0 +
                    front *
                    _WaveFrontBoost;

                alpha = saturate(alpha);

                if (alpha <= 0.001)
                    discard;

                float brightness =
                    lerp(
                        _InnerBrightness,
                        1.0,
                        Smooth01(radialT)
                    );

                float3 rgb =
                    saturate(
                        _DangerColor.rgb *
                        brightness
                    );

                // Strike moment shockwave:
                // Damage uygulanmis oldugu frame'den hemen sonra Boss merkezinden
                // disariya dogru ilerleyen parlak halka. Cover arkasinda
                // dangerVisibility zaten 0 oldugu icin shockwave de gorunmez.
                if (_StrikeWaveProgress >= 0.0)
                {
                    float strikePosition =
                        saturate(_StrikeWaveProgress);

                    float strikeDistance =
                        abs(radialT - strikePosition);

                    float strikeRing =
                        1.0 -
                        smoothstep(
                            _StrikeWaveWidth * 0.15,
                            _StrikeWaveWidth,
                            strikeDistance
                        );

                    // Halkanin ic kenarina daha sert bir "impact" cekirdegi.
                    float strikeCore =
                        1.0 -
                        smoothstep(
                            0.0,
                            max(0.001, _StrikeWaveWidth * 0.32),
                            strikeDistance
                        );

                    float strikeAlpha =
                        saturate(
                            (strikeRing * 0.75 +
                             strikeCore * 0.65) *
                            _StrikeWaveBoost *
                            dangerVisibility
                        );

                    alpha =
                        saturate(
                            alpha +
                            strikeAlpha * 0.72
                        );

                    // Kirmiziyi strike halkasinda beyaza/pembe-beyaza yaklastir.
                    float3 strikeColor =
                        lerp(
                            _DangerColor.rgb,
                            float3(1.0, 0.82, 0.82),
                            saturate(
                                strikeRing * 0.65 +
                                strikeCore * 0.85
                            )
                        );

                    rgb =
                        lerp(
                            rgb,
                            strikeColor,
                            saturate(
                                strikeRing * 0.85 +
                                strikeCore
                            )
                        );
                }

                return float4(
                    saturate(rgb),
                    saturate(alpha)
                );
            }

            ENDHLSL
        }
    }

    Fallback Off
}
