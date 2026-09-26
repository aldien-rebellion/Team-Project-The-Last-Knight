using System.Collections.Generic;
using UnityEngine;

namespace TheLastKnight.Combat
{
    // Tight sprite vertices exclude the transparent padding in the source sheet.
    public static class SpriteVisualBounds
    {
        private static readonly Dictionary<Sprite, Bounds> Cache = new Dictionary<Sprite, Bounds>();

        public static Bounds GetWorldBounds(SpriteRenderer renderer)
        {
            var sprite = renderer.sprite;
            if (sprite == null) return renderer.bounds;
            if (!Cache.TryGetValue(sprite, out var local))
            {
                var vertices = sprite.vertices;
                if (vertices.Length == 0) return renderer.bounds;
                local = new Bounds(vertices[0], Vector3.zero);
                foreach (var vertex in vertices) local.Encapsulate(vertex);
                Cache[sprite] = local;
            }
            var center = local.center;
            if (renderer.flipX) center.x = -center.x;
            if (renderer.flipY) center.y = -center.y;
            var world = new Bounds(renderer.transform.TransformPoint(center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    world.Encapsulate(renderer.transform.TransformPoint(center + new Vector3(x * local.extents.x, y * local.extents.y)));
            return world;
        }
    }
}
