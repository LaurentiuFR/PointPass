Shader "PointPass/Natural Turf"
{
    Properties
    {
        [PerRendererData] _MainTex ("Grass", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 grass=tex2D(_MainTex,i.uv);
                float2 d=_MainTex_TexelSize.xy*1.5;
                fixed3 soft=(tex2D(_MainTex,i.uv+d).rgb+tex2D(_MainTex,i.uv-d).rgb
                    +tex2D(_MainTex,i.uv+float2(d.x,-d.y)).rgb+tex2D(_MainTex,i.uv+float2(-d.x,d.y)).rgb)*0.25;
                float detail=dot(lerp(grass.rgb,soft,0.55),float3(0.25,0.6,0.15));
                // Muted ryegrass palette: restrained texture contrast and irregular broad variation.
                float tone=saturate(0.5+(detail-0.43)*1.05);
                float patch=sin(i.uv.x*19+i.uv.y*7)*sin(i.uv.y*23-i.uv.x*3)*0.011;
                float mowing=sin(i.uv.x*62.832)*0.007;
                fixed3 turf=lerp(float3(0.19,0.32,0.20),float3(0.35,0.49,0.31),tone)+patch+mowing;
                return fixed4(turf*i.color.rgb,grass.a*i.color.a);
            }
            ENDCG
        }
    }
}
