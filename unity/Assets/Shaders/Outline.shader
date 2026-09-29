// Cel-Shading-Kontur: Inverted-Hull-Outline.
// Das Outline-Mesh ist eine minimal vergroesserte Kopie des Stein-Quaders;
// durch Cull Front bleiben nur die Innenseiten sichtbar, die rund um die
// Silhouette herausragen -> dunkle Kontur um jeden Stein.
Shader "Mahjong/Outline"
{
    Properties
    {
        _Color ("Konturfarbe", Color) = (0.11, 0.09, 0.06, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag() : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}