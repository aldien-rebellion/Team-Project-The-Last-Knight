using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TheLastKnight.Core;
using static TheLastKnight.UI.MinimapGraphic;

namespace TheLastKnight.UI
{
    // The whole kingdom is an authored overhead atlas. These positions describe
    // geography on the atlas, not elevation in the side-scrolling gameplay scenes.
    public sealed class WorldMapGraphic : MaskableGraphic
    {
        public struct Region
        {
            public string Scene;
            public Vector2 Position;
            public Region(string scene, float x, float y) { Scene = scene; Position = new Vector2(x, y); }
        }

        public static readonly Region[] Regions = {
            new Region("Church", 0.21f, 0.72f),
            new Region("CityCenter", 0.22f, 0.32f),
            new Region("OutdoorMarket", 0.41f, 0.39f),
            new Region("SuburbToForest", 0.60f, 0.51f),
            new Region("DemonCastleEntrance", 0.77f, 0.58f),
            new Region("DemonCastle", 0.85f, 0.77f)
        };
        // Matches the ScenePortal destinations and the castle-gate transition.
        public static readonly Vector2Int[] Connections = {
            new Vector2Int(0, 1), new Vector2Int(1, 2), new Vector2Int(2, 3),
            new Vector2Int(3, 4), new Vector2Int(4, 5)
        };
        public int RegionCount => Regions.Length;
        public int CurrentRegionIndex
        {
            get
            {
                string scene = SceneManager.GetActiveScene().name;
                for (int i = 0; i < Regions.Length; i++) if (Regions[i].Scene == scene) return i;
                return -1;
            }
        }
        private static Color C(float r, float g, float b, float a = 1) => new Color(r, g, b, a);
        private Vector2 P(float x, float y)
        {
            var r = rectTransform.rect;
            return new Vector2(r.xMin + x * r.width, r.yMin + y * r.height);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var view = rectTransform.rect;
            Quad(mesh, view, C(0.045f, 0.095f, 0.107f));
            for (float x = view.xMin; x < view.xMax; x += 34) Quad(mesh, new Rect(x, view.yMin, 0.7f, view.height), C(0.37f, 0.49f, 0.43f, 0.1f));
            for (float y = view.yMin; y < view.yMax; y += 34) Quad(mesh, new Rect(view.xMin, y, view.width, 0.7f), C(0.37f, 0.49f, 0.43f, 0.1f));

            var coast = new[] { P(.07f,.25f), P(.09f,.63f), P(.14f,.90f), P(.32f,.96f), P(.46f,.84f),
                P(.66f,.94f), P(.91f,.95f), P(.97f,.75f), P(.93f,.39f), P(.79f,.25f),
                P(.64f,.17f), P(.49f,.22f), P(.33f,.12f), P(.17f,.15f) };
            Polygon(mesh, coast, C(.22f,.27f,.22f));
            Outline(mesh, coast, C(.50f,.52f,.35f), 2);
            // Terrain contours and biomes are intentionally drawn as overhead shapes.
            Ellipse(mesh, P(.23f,.49f), view.width * .145f, view.height * .39f, C(.30f,.31f,.24f));
            Ellipse(mesh, P(.33f,.42f), view.width * .16f, view.height * .23f, C(.34f,.32f,.24f));
            Ellipse(mesh, P(.60f,.54f), view.width * .16f, view.height * .33f, C(.16f,.29f,.23f));
            Ellipse(mesh, P(.82f,.71f), view.width * .14f, view.height * .25f, C(.27f,.22f,.23f));
            EllipseOutline(mesh, P(.23f,.49f), view.width * .17f, view.height * .43f, C(.65f,.61f,.41f,.16f));
            EllipseOutline(mesh, P(.60f,.54f), view.width * .18f, view.height * .37f, C(.49f,.65f,.47f,.17f));

            // River, bridges and roads share the same overhead plane.
            var river = new[] { P(.46f,.93f), P(.48f,.77f), P(.46f,.66f), P(.49f,.54f), P(.49f,.39f), P(.52f,.24f), P(.56f,.05f) };
            for (int i = 1; i < river.Length; i++)
            {
                Line(mesh, river[i-1], river[i], C(.06f,.13f,.14f), 17);
                Line(mesh, river[i-1], river[i], C(.20f,.38f,.40f), 11);
                Line(mesh, river[i-1], river[i], C(.41f,.59f,.56f,.4f), 1.5f);
            }

            // Deterministic tree canopies: never consume the game's random sequence.
            for (int i = 0; i < 58; i++)
            {
                float x = .53f + Hash(i, 11) * .17f;
                float y = .26f + Hash(i, 29) * .55f;
                if (Vector2.Distance(new Vector2(x, y), Regions[3].Position) < .075f) continue;
                var p = P(x, y);
                Disc(mesh, p + new Vector2(2, -2), 7, C(.04f,.10f,.09f,.7f));
                Disc(mesh, p, 5 + Hash(i, 41) * 3, C(.18f,.38f,.28f));
                Disc(mesh, p + new Vector2(-1.5f, 1.5f), 3.4f, C(.31f,.48f,.32f));
            }
            for (int i = 0; i < 12; i++)
            {
                var p = P(.72f + Hash(i, 3) * .20f, .83f + Hash(i, 17) * .08f);
                Disc(mesh, p, 9, C(.16f,.17f,.17f));
                Disc(mesh, p, 6, C(.35f,.35f,.30f));
                Disc(mesh, p + Vector2.up * 2, 3, C(.49f,.47f,.39f));
            }

            bool locked = GameManager.Instance != null && !GameManager.Instance.State.demonCastleGateUnlocked;
            for (int i = 0; i < Connections.Length; i++)
            {
                var edge = Connections[i];
                var a = P(Regions[edge.x].Position.x, Regions[edge.x].Position.y);
                var b = P(Regions[edge.y].Position.x, Regions[edge.y].Position.y);
                var bend = Vector2.Lerp(a, b, .5f) + new Vector2(i % 2 == 0 ? 12 : -12, 8);
                Road(mesh, a, bend, i == 4 && locked);
                Road(mesh, bend, b, i == 4 && locked);
            }
            var bridge = P(.49f,.44f);
            Quad(mesh, new Rect(bridge.x - 11, bridge.y - 4, 22, 8), C(.68f,.55f,.36f));
            for (int i = 0; i < 6; i++) Line(mesh, bridge + new Vector2(-9 + i * 3.5f,-4), bridge + new Vector2(-9 + i * 3.5f,4), C(.24f,.20f,.16f), 1);

            Town(mesh, P(.22f,.32f), false);
            Town(mesh, P(.41f,.39f), true);
            Church(mesh, P(.21f,.72f));
            var clearing = P(.60f,.51f);
            Ellipse(mesh, clearing, 22, 16, C(.34f,.40f,.28f));
            Disc(mesh, clearing, 9, C(.61f,.54f,.37f));
            Disc(mesh, clearing, 5, C(.30f,.38f,.28f));
            Gate(mesh, P(.77f,.58f), locked);
            Castle(mesh, P(.85f,.77f));

            int current = CurrentRegionIndex;
            if (current >= 0)
            {
                var region = Regions[current];
                var pin = P(region.Position.x, region.Position.y + .075f);
                Disc(mesh, pin, 12 + Mathf.Sin(Time.unscaledTime * 3) * 2, C(.96f,.84f,.48f,.18f));
                Disc(mesh, pin, 7, C(.05f,.06f,.055f));
                Diamond(mesh, pin, 5, C(1f,.89f,.58f));
                Line(mesh, pin + Vector2.down * 7, P(region.Position.x, region.Position.y), C(.95f,.82f,.52f), 1.3f);
            }
            var compass = P(.955f,.18f);
            Disc(mesh, compass, 21, C(.06f,.1f,.10f,.8f));
            EllipseOutline(mesh, compass, 19, 19, MinimapUI.Gold);
            Line(mesh, compass - Vector2.up * 27, compass + Vector2.up * 27, MinimapUI.Gold, 1.4f);
            Line(mesh, compass - Vector2.right * 27, compass + Vector2.right * 27, MinimapUI.Gold, 1.4f);
            Diamond(mesh, compass, 10, MinimapUI.Gold);
        }

        private static float Hash(int i, int salt)
        {
            uint value = unchecked((uint)(i * 374761393 + salt * 668265263));
            value = unchecked((value ^ (value >> 13)) * 1274126177u);
            value ^= value >> 16;
            return (value & 65535u) / 65535f;
        }
        private static void Town(VertexHelper mesh, Vector2 p, bool market)
        {
            var outline = C(.61f,.55f,.37f);
            Quad(mesh, new Rect(p.x - 35, p.y - 23, 70, 46), C(.47f,.43f,.31f));
            Frame(mesh, new Rect(p.x - 35, p.y - 23, 70, 46), outline, 3);
            for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
            {
                if (x == 0 || y == 0) continue;
                var block = new Rect(p.x + x * 21 - 8, p.y + y * 13 - 5, 16, 10);
                Quad(mesh, new Rect(block.x + 1, block.y - 2, block.width, block.height), C(.12f,.15f,.13f));
                Quad(mesh, block, market ? C(.56f,.29f,.28f) : C(.63f,.57f,.43f));
                Line(mesh, new Vector2(block.center.x, block.yMin), new Vector2(block.center.x, block.yMax), outline, 1);
            }
            Disc(mesh, p, 6, market ? C(.67f,.45f,.27f) : C(.35f,.53f,.49f));
            if (!market) for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) Disc(mesh, p + new Vector2(x * 35,y * 23), 5, outline);
        }
        private static void Church(VertexHelper mesh, Vector2 p)
        {
            Ellipse(mesh, p, 34, 28, C(.37f,.39f,.31f));
            Quad(mesh, new Rect(p.x - 10,p.y - 23,20,46), C(.71f,.68f,.54f));
            Quad(mesh, new Rect(p.x - 23,p.y - 3,46,16), C(.71f,.68f,.54f));
            Quad(mesh, new Rect(p.x - 4,p.y - 18,8,34), C(.48f,.45f,.37f));
            Quad(mesh, new Rect(p.x - 18,p.y + 2,36,5), C(.48f,.45f,.37f));
            Disc(mesh, p + Vector2.up * 22, 6, C(.85f,.77f,.54f));
        }
        private static void Castle(VertexHelper mesh, Vector2 p)
        {
            Quad(mesh, new Rect(p.x - 32,p.y - 25,64,50), C(.18f,.17f,.18f));
            Frame(mesh, new Rect(p.x - 32,p.y - 25,64,50), C(.58f,.48f,.44f), 5);
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2)
            {
                Disc(mesh, p + new Vector2(x * 32,y * 25), 9, C(.67f,.54f,.47f));
                Disc(mesh, p + new Vector2(x * 32,y * 25), 5, C(.30f,.23f,.25f));
            }
            Quad(mesh, new Rect(p.x - 13,p.y - 10,26,27), C(.61f,.37f,.36f));
            Frame(mesh, new Rect(p.x - 13,p.y - 10,26,27), C(.78f,.62f,.49f), 2);
        }
        private static void Gate(VertexHelper mesh, Vector2 p, bool locked)
        {
            Quad(mesh, new Rect(p.x - 23,p.y - 5,46,10), C(.56f,.53f,.43f));
            Disc(mesh, p + Vector2.left * 23, 8, C(.71f,.62f,.46f));
            Disc(mesh, p + Vector2.right * 23, 8, C(.71f,.62f,.46f));
            Quad(mesh, new Rect(p.x - 7,p.y - 5,14,10), locked ? C(.76f,.32f,.29f) : C(.33f,.72f,.53f));
        }
        private static void Road(VertexHelper mesh, Vector2 a, Vector2 b, bool locked)
        {
            Line(mesh, a, b, C(.07f,.09f,.08f,.8f), 9);
            Line(mesh, a, b, locked ? C(.68f,.32f,.29f) : C(.70f,.60f,.41f), 4);
            float length = Vector2.Distance(a, b);
            for (float d = 0; d < length; d += 12)
                Disc(mesh, Vector2.Lerp(a,b,d / length), 1.3f, C(.95f,.84f,.58f,.65f));
        }
        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, Color c, float width)
        {
            var direction = (b - a).normalized;
            var n = new Vector2(-direction.y,direction.x) * width * .5f;
            int i = mesh.currentVertCount;
            mesh.AddVert(a-n,c,Vector2.zero); mesh.AddVert(a+n,c,Vector2.zero);
            mesh.AddVert(b+n,c,Vector2.zero); mesh.AddVert(b-n,c,Vector2.zero);
            mesh.AddTriangle(i,i+1,i+2); mesh.AddTriangle(i,i+2,i+3);
        }
        private static void Polygon(VertexHelper mesh, Vector2[] points, Color c)
        {
            var center = Vector2.zero;
            foreach (var p in points) center += p;
            center /= points.Length;
            int start = mesh.currentVertCount;
            mesh.AddVert(center,c,Vector2.zero);
            foreach (var p in points) mesh.AddVert(p,c,Vector2.zero);
            for (int i = 0; i < points.Length; i++) mesh.AddTriangle(start,start+1+i,start+1+(i+1)%points.Length);
        }
        private static void Outline(VertexHelper mesh, Vector2[] points, Color c, float width)
        {
            for (int i = 0; i < points.Length; i++) Line(mesh,points[i],points[(i+1)%points.Length],c,width);
        }
        private static Vector2[] EllipsePoints(Vector2 center, float x, float y)
        {
            var points = new Vector2[32];
            for (int i = 0; i < points.Length; i++)
            {
                float a = i * Mathf.PI * 2 / points.Length;
                points[i] = center + new Vector2(Mathf.Cos(a)*x,Mathf.Sin(a)*y);
            }
            return points;
        }
        private static void Ellipse(VertexHelper mesh, Vector2 center, float x, float y, Color c) => Polygon(mesh,EllipsePoints(center,x,y),c);
        private static void EllipseOutline(VertexHelper mesh, Vector2 center, float x, float y, Color c) => Outline(mesh,EllipsePoints(center,x,y),c,1);
    }
}
