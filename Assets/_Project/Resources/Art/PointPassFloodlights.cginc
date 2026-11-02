#ifndef POINTPASS_FLOODLIGHTS_INCLUDED
#define POINTPASS_FLOODLIGHTS_INCLUDED
// The same four fixture positions/focus points are exposed by StadiumFixtures.
float4 _StadiumOrigin;
float LightPool(float2 p, float2 focus)
{
    float2 delta=(p-focus)/float2(5.8,4.1);
    return exp(-dot(delta,delta));
}
float StadiumPools(float2 p)
{
    return LightPool(p,float2(-6,3.5))+LightPool(p,float2(6,3.5))
        +LightPool(p,float2(-6,-3.5))+LightPool(p,float2(6,-3.5));
}
float3 StadiumIllumination(float2 p)
{
    // Cool evening ambient plus broad neutral-white overlapping floodlight pools.
    return float3(0.70,0.77,0.90)+float3(0.98,0.99,1.0)*StadiumPools(p)*0.23;
}
float3 StadiumActorShade(float2 p, float2 uv)
{
    float2 key=float2(p.x<0 ? -10.55 : 10.55,p.y<0 ? -6.38 : 6.38);
    float2 fromPoint=key-p;
    float edge=dot((uv-0.5),fromPoint/max(length(fromPoint),0.01))*0.075;
    return StadiumIllumination(p)+edge;
}
#endif
