Shader "Expedition/CatalogSpriteClip"
{
    Properties
    {
        _MainTex ("Sprite Sheet", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.13
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Opaque" }
        Cull Off
        ZTest Always
        ZWrite Off
        Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed _Cutoff;
            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv);
                if (color.a < _Cutoff) return fixed4(0, 0, 0, 0);
                return color;
            }
            ENDCG
        }
    }
}
