Shader "Hidden/PreviewShader"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Mask ("Mask", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _Mask;
            int _ColorSpace;
            float4 _WorldClip;
            float4 _WorldRect;
            float4 _TexSize;
            float _EditorTime;
            float _Loading;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 coords = lerp(_WorldRect.xy, _WorldRect.zw, float2(i.uv.x, 1.0 - i.uv.y));

                if (coords.x > (_WorldClip.z) || coords.x < _WorldClip.x ||coords.y < _WorldClip.y || coords.y > _WorldClip.w )
                    return float4(0, 0, 0, 0);

                fixed4 col = tex2D(_MainTex, i.uv);
                col.a = tex2D(_Mask, i.uv).r;

                //Loading circle
                float2 circleCenter = _TexSize.xy - 24;
                float circleRange = distance(circleCenter, i.uv * _TexSize.xy);
                float circle = smoothstep(20, 18, circleRange) * smoothstep(14, 16, circleRange);


                float2 cone = float2(sin(_EditorTime * 4), cos(_EditorTime * 4));
                float coneSize = sin(_EditorTime * 2);
                coneSize = smoothstep(0, 1, coneSize * 0.5 + 0.5) * 2 -1;
                coneSize *= 0.8;
                float2 uvDirection = normalize(circleCenter - i.uv * _TexSize.xy);

                float inCone = smoothstep(coneSize, coneSize + 0.1, dot(cone, uvDirection));


                col = lerp(col, float4(1, 1, 1, 1), circle * inCone * 0.7 * _Loading);

                //Gamma correction
                if (_ColorSpace == 1)
                    col = pow(col, 0.454545);

                return col;
            }
            ENDCG
        }
    }
}
