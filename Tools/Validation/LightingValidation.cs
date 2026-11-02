using System;
using PointPass.Gameplay;
using UnityEngine;

namespace PointPass.Validation
{
    public static class LightingValidation
    {
        public static void Run(Action<bool, string> check)
        {
            for (int i = 0; i < StadiumFixtures.TowerCount; i++)
            {
                Vector2 focus = StadiumFixtures.LightFocus(i);
                check(Mathf.Abs(StadiumFixtures.PoolAt(focus, i) - 1f) < 0.0001f
                    && StadiumFixtures.PoolAt(Vector2.zero, i) < 0.6f,
                    "Floodlight " + i + " projects its strongest pool toward its own field focus");
            }
            check(Mathf.Abs(Total(new Vector2(-4, 2)) - Total(new Vector2(4, -2))) < 0.0001f,
                "Floodlight coverage remains symmetric across both ends of the pitch");
            check(Total(Vector2.zero) > 0.1f && Total(new Vector2(9.7f, 5.7f)) > 0.1f,
                "Overlapping broad light pools cover midfield and field corners");

            GameObject origin = new GameObject("Replay lighting origin");
            Material kitMaterial = null;
            try
            {
                origin.transform.position = new Vector3(1000, 1000);
                GameObject turfObject = new GameObject("Turf", typeof(SpriteRenderer), typeof(TurfSurface));
                turfObject.transform.SetParent(origin.transform, false);
                TurfSurface turf = turfObject.GetComponent<TurfSurface>();
                turf.Init(turfObject.GetComponent<SpriteRenderer>());
                Material turfMaterial = turfObject.GetComponent<SpriteRenderer>().sharedMaterial;
                check(turfMaterial != null && turfMaterial.shader.name == "PointPass/Natural Turf"
                    && turf.Wetness == 0 && turfMaterial.GetFloat("_Wetness") == 0,
                    "Grass starts dry and matte, without wet-pitch reflection");
                check(Vector2.Distance(turfMaterial.GetVector("_StadiumOrigin"), new Vector2(1000, 1000)) < 0.001f,
                    "Turf illumination follows the isolated replay's field origin");
                turf.SetWetness(2f);
                check(turf.Wetness == 1 && turfMaterial.GetFloat("_Wetness") == 1,
                    "Wet sheen is enabled only by the bounded surface parameter");
                turf.SetWetness(float.NaN);
                check(turf.Wetness == 0, "Invalid wetness returns to dry instead of poisoning the material");

                GameObject actor = new GameObject("Actor");
                actor.transform.SetParent(origin.transform, false);
                SpriteRenderer body = new GameObject("Body", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                body.transform.SetParent(actor.transform, false);
                kitMaterial = new Material(Resources.Load<Shader>("Art/ClubKit"));
                body.sharedMaterial = kitMaterial;
                SpriteRenderer ball = new GameObject("Ball", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                ball.transform.SetParent(actor.transform, false);
                StadiumActorLighting light = actor.AddComponent<StadiumActorLighting>();
                light.Init(origin.transform);
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                body.GetPropertyBlock(block);
                check(body.sharedMaterial == kitMaterial && ball.sharedMaterial.shader.name == "PointPass/Stadium Sprite"
                    && block.GetFloat("_WorldLighting") == 1,
                    "Kits retain their material while ball and limb art receive the same stadium light model");
                check(Vector2.Distance(block.GetVector("_StadiumOrigin"), new Vector2(1000, 1000)) < 0.001f,
                    "Actor light pools use replay-local positions rather than the live field's origin");

                StadiumShadow shadow = actor.AddComponent<StadiumShadow>();
                shadow.Init(0.8f, 0.3f, 1);
                bool directed = true;
                for (int i = 0; i < 4; i++)
                {
                    Transform projection = actor.transform.Find("FloodlightShadow" + i);
                    Vector2 offset = projection.position - actor.transform.position;
                    directed &= Vector2.Dot(offset, -StadiumFixtures.LightPosition(i)) > 0;
                }
                check(directed, "Each feathered actor shadow projects away from its corresponding floodlight");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(origin);
                if (kitMaterial != null) UnityEngine.Object.DestroyImmediate(kitMaterial);
            }
        }

        private static float Total(Vector2 position)
        {
            float amount = 0;
            for (int i = 0; i < StadiumFixtures.TowerCount; i++) amount += StadiumFixtures.PoolAt(position, i);
            return amount;
        }
    }
}
