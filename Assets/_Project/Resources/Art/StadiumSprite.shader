Shader "PointPass/Stadium Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite",2D)="white" {}
        _Color ("Tint",Color)=(1,1,1,1)
        [PerRendererData] _StadiumOrigin ("Stadium origin",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Lighting Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "PointPassFloodlights.cginc"
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; float2 world:TEXCOORD1; };
            sampler2D _MainTex; fixed4 _Color;
            v2f vert(appdata_full v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord; o.color=v.color*_Color;
                o.world=mul(unity_ObjectToWorld,v.vertex).xy-_StadiumOrigin.xy; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 color=tex2D(_MainTex,i.uv)*i.color;
                color.rgb*=StadiumActorShade(i.world,i.uv);
                return color;
            }
            ENDCG
        }
    }
}
