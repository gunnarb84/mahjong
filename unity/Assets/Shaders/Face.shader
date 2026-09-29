// Stein-Symbol (Atlas-Zelle) auf der Oberseite: Alpha-Blend ueber den
// Steinkoerper gezeichnet. Eigener Mini-Shader statt Standard, weil die
// Standard-Fade-Variante (_ALPHABLEND_ON) zur Buildzeit gestrippt wird,
// wenn kein Asset im Build sie referenziert (Editor kompiliert on-demand
// und wirkt deshalb korrekt — Build zeigt opake Quads, siehe Viereck).
Shader "Mahjong/Face"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        _Color ("Faerbung", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST; // Atlas-Zelle via MaterialPropertyBlock
            fixed4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION, float2 uv : TEXCOORD0)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.uv = uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * _Color;
            }
            ENDCG
        }
    }
}