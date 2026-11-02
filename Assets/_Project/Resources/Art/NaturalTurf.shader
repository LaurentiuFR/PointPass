Shader "PointPass/Natural Turf"
{
    Properties
    {
        [PerRendererData] _MainTex ("Grass", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _UIReplay ("UI replay projection",Float) = 0
        _Wetness ("Surface wetness",Range(0,1)) = 0
        [HideInInspector] _StadiumOrigin ("Stadium origin",Vector) = (0,0,0,0)
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
            #include "PointPassFloodlights.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float2 world:TEXCOORD1; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _PointPassFlash;
            float _UIReplay, _Wetness;
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
                #ifndef UNITY_COLORSPACE_GAMMA
                // Grade sampled turf in display space, then illuminate in linear space.
                grass.rgb=LinearToGammaSpace(grass.rgb);
                soft=LinearToGammaSpace(soft);
                #endif
                float detail=dot(lerp(grass.rgb,soft,0.78),float3(0.25,0.6,0.15));
                // Muted ryegrass palette: restrained texture contrast and irregular broad variation.
                float tone=saturate(0.5+(detail-0.43)*0.65);
                float patch=sin(i.uv.x*19+i.uv.y*7)*sin(i.uv.y*23-i.uv.x*3)*0.006;
                float mowing=sin(i.uv.x*62.832)*0.005;
                float2 bladeUV=i.uv*float2(1450,870);
                float2 cell=floor(bladeUV), local=frac(bladeUV);
                float seed=hash(cell);
                float blade=1-smoothstep(0.09,0.09+max(fwidth(bladeUV.x),0.12),abs(local.x-0.5+(local.y-0.5)*(seed-0.5)));
                // Derivative filtering suppresses sub-pixel fibre shimmer at match distance.
                float grainFade=1-saturate(max(fwidth(bladeUV.x),fwidth(bladeUV.y))*0.55);
                float fine=((seed-0.5)*0.007+blade*(seed-0.45)*0.012)*grainFade;
                fixed3 turf=lerp(float3(0.12,0.285,0.16),float3(0.235,0.405,0.235),tone)+patch+mowing+fine;
                #ifndef UNITY_COLORSPACE_GAMMA
                turf=GammaToLinearSpace(turf);
                #endif
                float2 p=lerp(i.world-_StadiumOrigin.xy,(i.uv-0.5)*float2(20,12),_UIReplay);
                float pools=StadiumPools(p);
                turf*=StadiumIllumination(p);
                // Dry grass is matte. Only an explicitly wet surface gets a restrained sheen.
                float sheen=saturate((detail-dot(soft,float3(0.25,0.6,0.15)))*6+0.08);
                turf+=float3(0.84,0.91,1)*sheen*pools*saturate(_Wetness)*0.16;
                float flash=exp(-dot(p-_PointPassFlash.xy,p-_PointPassFlash.xy)*0.7)*_PointPassFlash.z;
                turf+=float3(0.72,0.82,1)*flash*0.2;
                return fixed4(turf*i.color.rgb,grass.a*i.color.a);
            }
            ENDCG
        }
    }
}
