Shader "PointPass/Club Kit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Player",2D)="white" {}
        _ClubColor ("Club colour",Color)=(0.25,0.52,0.86,1)
        _WorldLighting ("Apply stadium lighting",Float)=1
        [PerRendererData] _StadiumOrigin ("Stadium origin",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "PointPassFloodlights.cginc"
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float2 world:TEXCOORD1; };
            sampler2D _MainTex; fixed4 _ClubColor; float _WorldLighting;
            v2f vert(appdata_full v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord; o.color=v.color;
                o.world=mul(unity_ObjectToWorld,v.vertex).xy-_StadiumOrigin.xy; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv);
                float blue=saturate((c.b-max(c.r,c.g)-0.08)*5);
                float red=saturate((c.r-max(c.g,c.b)-0.26)*5)*(1-step(0.35,c.g));
                float strength=max(blue,red);
                float value=max(c.r,max(c.g,c.b));
                c.rgb=lerp(c.rgb,_ClubColor.rgb*(0.35+value*0.8),strength);
                c.rgb*=lerp(float3(1,1,1),StadiumActorShade(i.world,i.uv),saturate(_WorldLighting));
                return c*i.color;
            }
            ENDCG
        }
    }
}
