using UnityEngine;

namespace PointPass.Gameplay
{
    /// <summary>Projected 2D floodlight illumination, detailed towers and separate broadcast rigs.</summary>
    public class StadiumFixtures : MonoBehaviour
    {
        public const int TowerCount = 4;
        public const int BroadcastCameraCount = 6;
        private MeshRenderer atmosphericBeams;
        public float Haze { get; private set; }

        public static Vector2 LightPosition(int index) => new Vector2(index % 2 == 0 ? -10.55f : 10.55f,
            index < 2 ? 6.38f : -6.38f);

        public static Vector2 LightFocus(int index) => new Vector2(index % 2 == 0 ? -6f : 6f,
            index < 2 ? 3.5f : -3.5f);

        /// <summary>Broad projected pool, shared with PointPassFloodlights.cginc.</summary>
        public static float PoolAt(Vector2 pitchPosition, int index)
        {
            Vector2 delta = pitchPosition - LightFocus(index);
            delta = new Vector2(delta.x / 5.8f, delta.y / 4.1f);
            return Mathf.Exp(-delta.sqrMagnitude);
        }

        public void Build(Material material)
        {
            PresentationMesh towers = new PresentationMesh();
            PresentationMesh illumination = new PresentationMesh();
            PresentationMesh glows = new PresentationMesh();
            PresentationMesh cameras = new PresentationMesh();
            PresentationMesh beams = new PresentationMesh();
            for (int i = 0; i < TowerCount; i++)
            {
                Vector2 position = LightPosition(i);
                GameObject marker = new GameObject("FloodlightTower" + i);
                marker.transform.SetParent(transform, false); marker.transform.localPosition = position;
                towers.Origin = position; glows.Origin = position;
                Tower(towers, glows);
                Vector2 lamp=position+Vector2.up*0.68f;
                Vector2 target=LightFocus(i);
                Vector2 width=Vector2.Perpendicular((target-lamp).normalized)*1.3f;
                beams.GradientTriangle(lamp,target-width,target+width,new Color(0.78f,0.87f,1f,0.035f),new Color(0.78f,0.87f,1f,0));
                illumination.Ellipse(target,
                    new Vector2(5.8f, 4.1f), new Color(0.86f, 0.92f, 1f, 0.025f), true, 48);
            }
            for (int i = 0; i < BroadcastCameraCount; i++)
            {
                Vector2 position = i < 2 ? new Vector2(i == 0 ? -4.4f : 4.4f, 6.36f)
                    : new Vector2(i < 4 ? -10.9f : 10.9f, i % 2 == 0 ? -2.85f : 2.85f);
                GameObject marker = new GameObject("BroadcastCamera" + i);
                marker.transform.SetParent(transform, false); marker.transform.localPosition = position;
                cameras.Origin = position;
                Vector2 aim = -position.normalized;
                cameras.Rotation = Mathf.Atan2(aim.y, aim.x)*Mathf.Rad2Deg - 90;
                BroadcastCamera(cameras);
            }
            towers.Build(transform, "FloodlightStructures", material, 0);
            cameras.Build(transform, "BroadcastRigs", material, 0);
            illumination.Build(transform, "PitchIllumination", material, -9);
            glows.Build(transform, "LampBloom", material, 3);
            atmosphericBeams=beams.Build(transform,"AtmosphericBeams",material,-7);
            SetHaze(0.3f); // A little evening haze reveals restrained beams; opaque lamps alone do not light turf.
        }

        public void SetHaze(float density)
        {
            Haze=float.IsNaN(density) || float.IsInfinity(density) ? 0f : Mathf.Clamp01(density);
            if (atmosphericBeams!=null) atmosphericBeams.enabled=Haze>0.2f;
        }

        private static void Tower(PresentationMesh mesh, PresentationMesh glow)
        {
            Color steel = new Color(0.24f, 0.3f, 0.33f), edge = new Color(0.56f, 0.63f, 0.65f);
            mesh.Ellipse(new Vector2(0.23f, -0.16f), new Vector2(0.7f, 0.2f), new Color(0,0,0,0.3f), true);
            mesh.Quad(Vector2.zero, new Vector2(0.39f, 0.2f), new Color(0.28f,0.32f,0.3f));
            mesh.Line(new Vector2(-0.09f,0), new Vector2(-0.06f,0.64f), 0.045f, steel);
            mesh.Line(new Vector2(0.09f,0), new Vector2(0.06f,0.64f), 0.045f, edge);
            for (int i=0; i<3; i++)
            {
                float y=i*0.18f;
                mesh.Line(new Vector2(-0.08f,y),new Vector2(0.08f,y+0.18f),0.017f,edge);
                mesh.Line(new Vector2(0.08f,y),new Vector2(-0.08f,y+0.18f),0.017f,steel);
            }
            mesh.Quad(new Vector2(0,0.68f), new Vector2(0.98f,0.35f), new Color(0.075f,0.1f,0.12f));
            mesh.Quad(new Vector2(0,0.86f), new Vector2(1.04f,0.04f), edge);
            for (int row=0; row<2; row++)
                for (int lamp=0; lamp<6; lamp++)
                {
                    Vector2 center = new Vector2(-0.39f+lamp*0.156f,0.6f+row*0.155f);
                    mesh.Quad(center, new Vector2(0.128f,0.12f), steel);
                    mesh.Quad(center, new Vector2(0.104f,0.094f), new Color(0.95f,0.95f,0.82f));
                    mesh.Line(center+new Vector2(-0.035f,0.025f),center+new Vector2(0.035f,0.025f),0.015f,Color.white);
                }
            glow.Ellipse(new Vector2(0,0.68f), new Vector2(0.6f,0.3f), new Color(1,0.95f,0.75f,0.06f), true);
        }

        private static void BroadcastCamera(PresentationMesh mesh)
        {
            Color steel = new Color(0.35f,0.41f,0.43f), dark = new Color(0.055f,0.075f,0.09f);
            mesh.Ellipse(new Vector2(0.1f,-0.07f), new Vector2(0.43f,0.25f),new Color(0,0,0,0.28f),true);
            foreach (Vector2 foot in new[] {new Vector2(-0.24f,-0.21f),new Vector2(0.24f,-0.21f),new Vector2(0,0.22f)})
            {
                mesh.Line(Vector2.zero,foot,0.035f,steel);
                mesh.Ellipse(foot, new Vector2(0.045f,0.022f),dark);
            }
            mesh.Quad(Vector2.zero,new Vector2(0.12f,0.24f),steel);
            // Long lens/hood, matte body, side viewfinder and red tally lamp: no bright lamp bank.
            mesh.Quad(new Vector2(0,0.23f),new Vector2(0.16f,0.3f),dark);
            for (int ring=0;ring<3;ring++)
                mesh.Quad(new Vector2(0,0.23f+ring*0.045f),new Vector2(0.17f,0.013f),steel);
            mesh.Quad(new Vector2(0,0.37f),new Vector2(0.23f,0.07f),new Color(0.025f,0.035f,0.04f));
            mesh.Quad(new Vector2(0,0.405f),new Vector2(0.16f,0.018f),new Color(0.18f,0.29f,0.36f));
            mesh.Line(new Vector2(-0.06f,0.407f),new Vector2(-0.015f,0.407f),0.009f,new Color(0.49f,0.66f,0.7f));
            mesh.Quad(new Vector2(0,0.09f),new Vector2(0.37f,0.27f),new Color(0.16f,0.2f,0.22f));
            mesh.Quad(new Vector2(-0.12f,0.09f),new Vector2(0.045f,0.22f),steel);
            mesh.Line(new Vector2(-0.17f,-0.025f),new Vector2(0.17f,-0.025f),0.017f,new Color(0.28f,0.33f,0.35f));
            mesh.Quad(new Vector2(0.24f,0.04f),new Vector2(0.14f,0.14f),dark);
            mesh.Quad(new Vector2(0.24f,0.05f),new Vector2(0.1f,0.08f),new Color(0.25f,0.42f,0.46f));
            mesh.Line(new Vector2(-0.07f,-0.065f),new Vector2(0.07f,-0.065f),0.035f,dark);
            mesh.Quad(new Vector2(0.1f,0.19f),new Vector2(0.036f,0.026f),new Color(0.95f,0.18f,0.12f));
        }

        private void OnDestroy() => PresentationMesh.ReleaseMeshes(transform);
    }
}
