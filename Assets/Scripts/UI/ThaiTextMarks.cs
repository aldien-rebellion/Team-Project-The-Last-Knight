using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastKnight.UI
{
    // Legacy Text has no OpenType mark positioning. Keep its existing layout and
    // raise only overlapping Thai tone marks in the generated glyph quads.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class ThaiTextMarks : BaseMeshEffect
    {
        private static readonly Regex RichTags = new Regex(@"</?(?:b|i|size|color|material)(?:=[^>]*)?>", RegexOptions.IgnoreCase);
        private readonly List<char> _characters = new List<char>();
        private readonly List<int> _quads = new List<int>();
        private readonly List<UIVertex> _vertices = new List<UIVertex>();
        private int _glyphVertices;
        private int _original;

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive()) return;
            var label = GetComponent<Text>();
            if (label.font != LocalizedText.Font) return;
            _vertices.Clear();
            vertices.GetUIVertexStream(_vertices);
            // Outline/Shadow may already have duplicated the triangle stream.
            _glyphVertices = label.cachedTextGenerator.vertexCount / 4 * 6;
            if (_glyphVertices == 0 || _vertices.Count % _glyphVertices != 0) return;
            _original = _vertices.Count - _glyphVertices;
            string source = label.supportRichText ? RichTags.Replace(label.text, "") : label.text;
            _characters.Clear();
            _quads.Clear();
            int quad = 0;
            foreach (char character in source)
            {
                _characters.Add(character);
                // Legacy Text emits no glyph quad for whitespace.
                _quads.Add(char.IsWhiteSpace(character) ? -1 : quad++);
            }

            int baseIndex = -1;
            int upperIndex = -1;
            float shiftX = 0;
            for (int i = 0; i < _characters.Count; i++)
            {
                char character = _characters[i];
                int vertex = _quads[i] * 6;
                if (_quads[i] < 0) { baseIndex = upperIndex = -1; continue; }
                if (vertex + 5 >= _glyphVertices) break;
                bool upper = character == '\u0E31' || (character >= '\u0E34' && character <= '\u0E37') || character == '\u0E47' || character == '\u0E4D';
                bool tone = character >= '\u0E48' && character <= '\u0E4C';
                bool lower = character >= '\u0E38' && character <= '\u0E3A';
                if (!upper && !tone && !lower)
                {
                    baseIndex = i;
                    upperIndex = -1;
                    shiftX = 0;
                    continue;
                }
                if (baseIndex < 0) continue;
                char consonant = _characters[baseIndex];
                bool tall = consonant == 'ป' || consonant == 'ฝ' || consonant == 'ฟ';
                if (upper)
                {
                    // Keep upper vowels clear of the tall consonant's right stem.
                    shiftX = tall ? -Height(_quads[baseIndex] * 6) * 0.18f : 0;
                    Move(vertex, new Vector2(shiftX, 0));
                    upperIndex = i;
                }
                else if (tone)
                {
                    int support = upperIndex;
                    // Sara am includes the upper nikhahit circle in the next glyph.
                    if (i + 1 < _characters.Count && _characters[i + 1] == '\u0E33') support = i + 1;
                    float shiftY = 0;
                    if (support >= 0 && _quads[support] * 6 + 5 < _glyphVertices)
                    {
                        float top = Top(_quads[support] * 6);
                        float gap = Height(_quads[baseIndex] * 6) * 0.07f;
                        shiftY = Mathf.Max(0, top + gap - Bottom(vertex));
                    }
                    float toneX = upperIndex >= 0 ? shiftX : tall ? -Height(_quads[baseIndex] * 6) * 0.18f : 0;
                    Move(vertex, new Vector2(toneX, shiftY));
                }
            }
            vertices.Clear();
            vertices.AddUIVertexTriangleStream(_vertices);
        }

        private float Top(int index)
        {
            float value = float.NegativeInfinity;
            for (int i = 0; i < 6; i++) value = Mathf.Max(value, _vertices[_original + index + i].position.y);
            return value;
        }

        private float Bottom(int index)
        {
            float value = float.PositiveInfinity;
            for (int i = 0; i < 6; i++) value = Mathf.Min(value, _vertices[_original + index + i].position.y);
            return value;
        }

        private float Height(int index) => Top(index) - Bottom(index);

        private void Move(int index, Vector2 offset)
        {
            for (int block = 0; block < _vertices.Count; block += _glyphVertices)
            for (int i = 0; i < 6; i++)
            {
                var vertex = _vertices[block + index + i];
                vertex.position += (Vector3)offset;
                _vertices[block + index + i] = vertex;
            }
        }
    }
}
