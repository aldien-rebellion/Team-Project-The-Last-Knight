using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TheLastKnight.Environment;
using TheLastKnight.Combat;

namespace TheLastKnight.UI
{
    // A cartographic projection of the 2D world's XY layout. Floor surfaces become
    // overhead lanes; room volumes retain their authored positions and dimensions.
    public sealed class MinimapGraphic : MaskableGraphic
    {
        private struct Marker
        {
            public Component Target;
            public int Kind;
            public Marker(Component target, int kind) { Target = target; Kind = kind; }
        }
        private readonly List<Rect> _rooms = new List<Rect>();
        private readonly List<Rect> _routes = new List<Rect>();
        private readonly List<Marker> _markers = new List<Marker>();
        private Transform _player;
        private UnityEngine.SceneManagement.Scene _mappedScene;
        public Rect WorldBounds { get; private set; }
        public int RouteCount => _routes.Count;
        public int RoomCount => _rooms.Count;
        public int MarkerCount => _markers.Count;
        private static readonly Color[] Colors = {
            new Color(0.95f, 0.87f, 0.59f), new Color(0.91f, 0.32f, 0.31f),
            new Color(0.47f, 0.70f, 0.96f),
            new Color(0.40f, 0.89f, 0.72f), new Color(0.78f, 0.57f, 0.93f)
        };
        public static Color MarkerColor(int kind) => Colors[Mathf.Clamp(kind, 0, Colors.Length - 1)];

        public void ClearMap()
        {
            _player = null; _rooms.Clear(); _routes.Clear(); _markers.Clear();
            _mappedScene = default;
            WorldBounds = new Rect(-10, -10, 20, 20);
            SetVerticesDirty();
        }

        public void RebuildMap(Transform player)
        {
            bool keepBounds = _mappedScene.IsValid() && _mappedScene == player.gameObject.scene;
            Rect previousBounds = WorldBounds;
            ClearMap();
            _player = player;
            var scene = player.gameObject.scene;
            _mappedScene = scene;
            foreach (var room in Object.FindObjectsByType<CastleRoom>(FindObjectsInactive.Include))
            {
                if (room.gameObject.scene != scene) continue;
                var box = room.CameraConfiner != null ? room.CameraConfiner : room.GetComponent<BoxCollider2D>();
                if (box == null) continue;
                // Transform the authored box, also when its room is currently inactive.
                Vector2 a = box.transform.TransformPoint(box.offset - box.size * 0.5f);
                Vector2 b = box.transform.TransformPoint(box.offset + box.size * 0.5f);
                _rooms.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)));
            }
            foreach (var collider in Object.FindObjectsByType<Collider2D>())
            {
                if (collider.gameObject.scene != scene || !collider.enabled || collider.isTrigger || collider.attachedRigidbody != null) continue;
                if (collider.name.IndexOf("confiner", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                var b = collider.bounds;
                // Walls are not walkable lanes. Do not scan decorative sprite bounds.
                if (b.size.x < 0.5f || b.size.x < b.size.y * 1.2f) continue;
                _routes.Add(new Rect(b.min.x, b.max.y - 1f, b.size.x, 2f));
            }
            AddMarkers<EnemyStats>(scene, 1);
            AddMarkers<ScenePortal>(scene, 2);
            AddMarkers<TeleportDoor>(scene, 2);
            AddMarkers<MedusaSavePoint>(scene, 3);
            AddMarkers<ShadowMarketNPC>(scene, 4);
            var position = (Vector2)player.position;
            Vector2 min = position - Vector2.one, max = position + Vector2.one;
            foreach (var room in _rooms) { min = Vector2.Min(min, room.min); max = Vector2.Max(max, room.max); }
            foreach (var route in _routes) { min = Vector2.Min(min, route.min); max = Vector2.Max(max, route.max); }
            foreach (var marker in _markers)
            {
                var p = (Vector2)marker.Target.transform.position;
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            var padding = new Vector2(Mathf.Max(3, (max.x - min.x) * 0.06f), Mathf.Max(3, (max.y - min.y) * 0.1f));
            WorldBounds = Rect.MinMaxRect(min.x - padding.x, min.y - padding.y, max.x + padding.x, max.y + padding.y);
            // Moving enemies must not make the map continually shift or resize.
            if (keepBounds) WorldBounds = previousBounds;
            SetVerticesDirty();
        }

        private void AddMarkers<T>(UnityEngine.SceneManagement.Scene scene, int kind) where T : Component
        {
            foreach (var target in Object.FindObjectsByType<T>(FindObjectsInactive.Include))
                if (target.gameObject.scene == scene) _markers.Add(new Marker(target, kind));
        }

        public Vector2 WorldToMap(Vector2 position)
        {
            var rect = rectTransform.rect;
            float scale = Mathf.Min(Mathf.Max(1, rect.width - 40) / Mathf.Max(1, WorldBounds.width),
                                    Mathf.Max(1, rect.height - 64) / Mathf.Max(1, WorldBounds.height));
            return rect.center + (position - WorldBounds.center) * scale;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var view = rectTransform.rect;
            var grid = new Color(0.18f, 0.28f, 0.27f, 0.25f);
            for (float x = view.xMin; x < view.xMax; x += 28) Quad(mesh, new Rect(x, view.yMin, 0.6f, view.height), grid);
            for (float y = view.yMin; y < view.yMax; y += 28) Quad(mesh, new Rect(view.xMin, y, view.width, 0.6f), grid);
            foreach (var room in _rooms)
            {
                var r = Project(room);
                Quad(mesh, r, new Color(0.22f, 0.30f, 0.28f, 0.75f));
                Frame(mesh, r, new Color(0.48f, 0.51f, 0.36f, 0.8f), 1.5f);
            }
            foreach (var route in _routes)
            {
                var r = Project(route);
                // Minimum cartographic lane width keeps a long street legible.
                if (r.height < 18) { r.y -= (18 - r.height) * 0.5f; r.height = 18; }
                Quad(mesh, r, new Color(0.22f, 0.30f, 0.29f));
                Frame(mesh, r, new Color(0.48f, 0.48f, 0.33f, 0.9f), 1);
                Quad(mesh, new Rect(r.x + 2, r.center.y - 0.5f, Mathf.Max(0, r.width - 4), 1), new Color(0.67f, 0.61f, 0.40f, 0.35f));
            }
            foreach (var marker in _markers)
            {
                if (marker.Target == null) continue;
                var enemy = marker.Target as EnemyStats;
                if (enemy != null && (!enemy.gameObject.activeInHierarchy || enemy.IsDead)) continue;
                Vector2 p = WorldToMap(marker.Target.transform.position);
                p.x = Mathf.Clamp(p.x, view.xMin + 10, view.xMax - 10);
                p.y = Mathf.Clamp(p.y, view.yMin + 10, view.yMax - 10);
                Color color = MarkerColor(marker.Kind);
                if (marker.Kind != 1) Disc(mesh, p, 11, new Color(color.r, color.g, color.b, 0.10f));
                Disc(mesh, p, marker.Kind == 1 ? 5 : 7, InkColor);
                if (marker.Kind == 2) Diamond(mesh, p, 5, color);
                else if (marker.Kind == 3) { Quad(mesh, new Rect(p.x - 1.5f, p.y - 5, 3, 10), color); Quad(mesh, new Rect(p.x - 5, p.y - 1.5f, 10, 3), color); }
                else Disc(mesh, p, marker.Kind == 1 ? 3 : 4, color);
            }
            if (_player != null)
            {
                var p = WorldToMap(_player.position);
                float pulse = 13 + Mathf.Sin(Time.unscaledTime * 3) * 2;
                Disc(mesh, p, pulse, new Color(0.95f, 0.82f, 0.4f, 0.16f));
                Disc(mesh, p, 8, InkColor);
                Diamond(mesh, p, 6, Colors[0]);
                Disc(mesh, p, 2, Color.white);
            }
            // Compass rose drawn independently of the player's facing direction.
            var compass = new Vector2(view.xMax - 27, view.yMax - 40);
            Diamond(mesh, compass, 9, MinimapUI.Gold);
            Quad(mesh, new Rect(compass.x - 0.7f, compass.y - 15, 1.4f, 30), MinimapUI.Gold);
            Quad(mesh, new Rect(compass.x - 15, compass.y - 0.7f, 30, 1.4f), MinimapUI.Gold);
        }

        private static readonly Color InkColor = new Color(0.02f, 0.03f, 0.04f);
        private Rect Project(Rect world)
        {
            var a = WorldToMap(world.min); var b = WorldToMap(world.max);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }
        internal static void Quad(VertexHelper mesh, Rect rect, Color color)
        {
            if (rect.width <= 0 || rect.height <= 0) return;
            int i = mesh.currentVertCount;
            mesh.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
        internal static void Frame(VertexHelper mesh, Rect r, Color c, float w)
        {
            Quad(mesh, new Rect(r.xMin, r.yMin, r.width, w), c);
            Quad(mesh, new Rect(r.xMin, r.yMax - w, r.width, w), c);
            Quad(mesh, new Rect(r.xMin, r.yMin, w, r.height), c);
            Quad(mesh, new Rect(r.xMax - w, r.yMin, w, r.height), c);
        }
        internal static void Diamond(VertexHelper mesh, Vector2 p, float radius, Color c)
        {
            int i = mesh.currentVertCount;
            mesh.AddVert(p + Vector2.up * radius, c, Vector2.zero);
            mesh.AddVert(p + Vector2.right * radius, c, Vector2.zero);
            mesh.AddVert(p + Vector2.down * radius, c, Vector2.zero);
            mesh.AddVert(p + Vector2.left * radius, c, Vector2.zero);
            mesh.AddTriangle(i, i + 1, i + 2); mesh.AddTriangle(i, i + 2, i + 3);
        }
        internal static void Disc(VertexHelper mesh, Vector2 p, float radius, Color c)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(p, c, Vector2.zero);
            const int sides = 16;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides;
                mesh.AddVert(p + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, c, Vector2.zero);
            }
            for (int i = 0; i < sides; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % sides);
        }
    }
}
