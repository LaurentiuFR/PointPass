Shader "PointPass/Menu Native Backdrop"
{
    Properties { _MainTex ("Match", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
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
                float2 step = _MainTex_TexelSize.xy * 3;
                fixed4 c = tex2D(_MainTex, i.uv) * 4;
                c += tex2D(_MainTex, i.uv + float2(step.x, 0)) * 2;
                c += tex2D(_MainTex, i.uv - float2(step.x, 0)) * 2;
                c += tex2D(_MainTex, i.uv + float2(0, step.y)) * 2;
                c += tex2D(_MainTex, i.uv - float2(0, step.y)) * 2;
                c += tex2D(_MainTex, i.uv + step);
                c += tex2D(_MainTex, i.uv - step);
                c += tex2D(_MainTex, i.uv + float2(step.x, -step.y));
                c += tex2D(_MainTex, i.uv + float2(-step.x, step.y));
                return c / 16;
            }
            ENDCG
        }
    }
}
