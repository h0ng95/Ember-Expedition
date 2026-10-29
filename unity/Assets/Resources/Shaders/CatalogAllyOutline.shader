Shader "Expedition/CatalogAllyOutline"
{
    Properties { _MainTex ("Sheet", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZTest Always ZWrite Off Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 d = _MainTex_TexelSize.xy * 2.0;
                fixed a = tex2D(_MainTex, i.uv).a;
                fixed n = min(min(tex2D(_MainTex, i.uv + float2(d.x,0)).a,
                    tex2D(_MainTex, i.uv - float2(d.x,0)).a),
                    min(tex2D(_MainTex, i.uv + float2(0,d.y)).a,
                    tex2D(_MainTex, i.uv - float2(0,d.y)).a));
                // Inner contour stays inside every frame/region, including cropped props.
                return fixed4(1,1,1,step(.5,a) * (1-step(.5,n)));
            }
            ENDCG
        }
    }
}
