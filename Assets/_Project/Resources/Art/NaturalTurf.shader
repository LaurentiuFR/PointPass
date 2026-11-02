Shader "PointPass/Natural Turf"
{
    Properties
    {
        [PerRendererData] _MainTex ("Grass", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _UIReplay ("UI replay projection",Float) = 0
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
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float2 world:TEXCOORD1; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _PointPassFlash;
            float _UIReplay;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color;
                o.world=mul(unity_ObjectToWorld,v.vertex).xy; return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 grass=tex2D(_MainTex,i.uv);
                float2 d=_MainTex_TexelSize.xy*1.5;
                fixed3 soft=(tex2D(_MainTex,i.uv+d).rgb+tex2D(_MainTex,i.uv-d).rgb
                    +tex2D(_MainTex,i.uv+float2(d.x,-d.y)).rgb+tex2D(_MainTex,i.uv+float2(-d.x,d.y)).rgb)*0.25;
                float detail=dot(lerp(grass.rgb,soft,0.18),float3(0.25,0.6,0.15));
                // Muted ryegrass palette: restrained texture contrast and irregular broad variation.
                float tone=saturate(0.5+(detail-0.43)*1.65);
                float patch=sin(i.uv.x*19+i.uv.y*7)*sin(i.uv.y*23-i.uv.x*3)*0.011;
                float mowing=sin(i.uv.x*62.832)*0.007;
                float2 bladeUV=i.uv*float2(1450,870);
                float2 cell=floor(bladeUV), local=frac(bladeUV);
                float seed=hash(cell);
                float blade=1-smoothstep(0.09,0.09+max(fwidth(bladeUV.x),0.12),abs(local.x-0.5+(local.y-0.5)*(seed-0.5)));
                float fine=(seed-0.5)*0.011+blade*(seed-0.45)*0.019;
                fixed3 turf=lerp(float3(0.17,0.29,0.17),float3(0.34,0.47,0.28),tone)+patch+mowing+fine;
                float2 p=lerp(i.world,(i.uv-0.5)*float2(20,12),_UIReplay);
                float pools=exp(-dot((p-float2(-5,2.4))/float2(5,3.6),(p-float2(-5,2.4))/float2(5,3.6)))
                    +exp(-dot((p-float2(5,2.4))/float2(5,3.6),(p-float2(5,2.4))/float2(5,3.6)))
                    +exp(-dot((p-float2(-5,-2.4))/float2(5,3.6),(p-float2(-5,-2.4))/float2(5,3.6)))
                    +exp(-dot((p-float2(5,-2.4))/float2(5,3.6),(p-float2(5,-2.4))/float2(5,3.6)));
                float sheen=saturate((detail-dot(soft,float3(0.25,0.6,0.15)))*3)*0.06;
                turf*=0.76+pools*0.19;
                turf+=float3(0.95,0.94,0.77)*sheen*pools;
                float flash=exp(-dot(p-_PointPassFlash.xy,p-_PointPassFlash.xy)*0.7)*_PointPassFlash.z;
                turf+=float3(0.72,0.82,1)*flash*0.2;
                return fixed4(turf*i.color.rgb,grass.a*i.color.a);
            }
            ENDCG
        }
    }
}
