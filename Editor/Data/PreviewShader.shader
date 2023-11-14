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

                //Gamma correction
                if (_ColorSpace == 1)
                    col = pow(col, 0.454545);

                return col;
            }
            ENDCG
        }
    }
}
