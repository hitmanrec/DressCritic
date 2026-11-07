using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace DressmakerCritic.Diagnostics
{
    // House crest sprites. Prefers embedded 512² parchment assets; falls back to a tiny
    // procedural mark if a PNG is missing. IDs match JuryContract house ids.
    internal static class HouseEmblems
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private static readonly Color Ink = new Color32(67, 46, 43, 255);
        private static readonly Color Gold = new Color32(176, 142, 96, 255);
        private static readonly Color Burgundy = new Color32(120, 54, 58, 255);
        private static readonly Color Sage = new Color32(92, 110, 88, 255);
        private static readonly Color Slate = new Color32(88, 92, 108, 255);
        private static readonly Color Paper = new Color32(241, 231, 222, 255);

        public static Sprite For(string houseId)
        {
            if (string.IsNullOrEmpty(houseId)) return null;
            if (cache.TryGetValue(houseId, out Sprite existing) && existing != null) return existing;
            var sprite = LoadEmbedded(houseId) ?? Bake(houseId);
            cache[houseId] = sprite;
            return sprite;
        }

        public static void Release()
        {
            foreach (var pair in cache)
            {
                if (pair.Value == null) continue;
                if (pair.Value.texture != null) UnityEngine.Object.Destroy(pair.Value.texture);
                UnityEngine.Object.Destroy(pair.Value);
            }
            cache.Clear();
        }

        private static Sprite LoadEmbedded(string id)
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (var stream = asm.GetManifestResourceStream("DressmakerCritic.House." + id + ".png"))
                {
                    if (stream == null) return null;
                    using (var ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        byte[] bytes = ms.ToArray();
                        if (bytes.Length == 0 || bytes.Length > 2 * 1024 * 1024) return null;
                        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                        {
                            filterMode = FilterMode.Bilinear,
                            wrapMode = TextureWrapMode.Clamp,
                            name = "DressCriticHouse-" + id
                        };
                        if (!UnityPng.LoadImage(tex, bytes, true))
                        {
                            UnityEngine.Object.Destroy(tex);
                            return null;
                        }
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
            }
            catch { return null; }
        }

        private static Sprite Bake(string id)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "DressCriticEmblem-" + id };
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);

            Color32 field = id == "harmony" ? new Color32(234, 222, 211, 255)
                : id == "character" ? new Color32(236, 220, 216, 255)
                : id == "avant_garde" ? new Color32(222, 226, 230, 255)
                : new Color32(228, 224, 214, 255);
            Color32 accent = id == "harmony" ? Gold : id == "character" ? Burgundy : id == "avant_garde" ? Slate : Sage;

            FillShield(pixels, size, field, Ink);
            if (id == "harmony") DrawBalance(pixels, size, accent, Ink);
            else if (id == "character") DrawStar(pixels, size, accent, Ink);
            else if (id == "avant_garde") DrawChevron(pixels, size, accent, Ink);
            else DrawNeedle(pixels, size, accent, Ink);

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        }

        private static void FillShield(Color32[] px, int size, Color32 fill, Color32 edge)
        {
            float cx = (size - 1) * 0.5f, cy = (size - 1) * 0.52f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x - cx) / (size * 0.42f);
                float ny = (y - cy) / (size * 0.46f);
                float top = ny + 0.55f;
                float body = nx * nx + Mathf.Max(0f, ny + 0.15f) * Mathf.Max(0f, ny + 0.15f) * 1.35f;
                bool inside = top > 0f && body < 1f && ny > -0.95f;
                float edgeDist = Mathf.Abs(body - 1f);
                if (!inside && edgeDist > 0.08f) continue;
                px[y * size + x] = (!inside || edgeDist < 0.08f || top < 0.08f) ? (Color32)edge : fill;
            }
        }

        private static void DrawBalance(Color32[] px, int size, Color32 accent, Color32 ink)
        {
            HLine(px, size, size / 2, size / 3, size * 2 / 3, ink, 2);
            VLine(px, size, size / 2, size / 4, size * 3 / 4, ink, 2);
            FillCircle(px, size, size / 3, size / 2 + 4, 6, accent);
            FillCircle(px, size, size * 2 / 3, size / 2 + 4, 6, accent);
        }

        private static void DrawStar(Color32[] px, int size, Color32 accent, Color32 ink)
        {
            int cx = size / 2, cy = size / 2 + 2;
            for (int i = 0; i < 5; i++)
            {
                float a0 = -Mathf.PI / 2f + i * Mathf.PI * 2f / 5f;
                float a1 = a0 + Mathf.PI * 2f / 5f;
                int x0 = cx + Mathf.RoundToInt(Mathf.Cos(a0) * 16);
                int y0 = cy + Mathf.RoundToInt(Mathf.Sin(a0) * 16);
                int x1 = cx + Mathf.RoundToInt(Mathf.Cos(a1) * 16);
                int y1 = cy + Mathf.RoundToInt(Mathf.Sin(a1) * 16);
                int mx = cx + Mathf.RoundToInt(Mathf.Cos(a0 + Mathf.PI / 5f) * 7);
                int my = cy + Mathf.RoundToInt(Mathf.Sin(a0 + Mathf.PI / 5f) * 7);
                Line(px, size, x0, y0, mx, my, accent, 2);
                Line(px, size, mx, my, x1, y1, accent, 2);
            }
            FillCircle(px, size, cx, cy, 3, ink);
        }

        private static void DrawChevron(Color32[] px, int size, Color32 accent, Color32 ink)
        {
            Line(px, size, size / 5, size * 2 / 3, size / 2, size / 3, accent, 3);
            Line(px, size, size / 2, size / 3, size * 4 / 5, size * 2 / 3, accent, 3);
            Line(px, size, size / 4, size * 3 / 4, size / 2, size / 2, ink, 2);
            Line(px, size, size / 2, size / 2, size * 3 / 4, size * 3 / 4, ink, 2);
        }

        private static void DrawNeedle(Color32[] px, int size, Color32 accent, Color32 ink)
        {
            Line(px, size, size / 2, size / 5, size / 2, size * 4 / 5, ink, 2);
            FillCircle(px, size, size / 2, size / 5 + 2, 3, accent);
            for (int i = 0; i < 5; i++)
            {
                int y = size / 2 + i * 3;
                HLine(px, size, y, size / 2 - 8 + (i % 2), size / 2 + 8 - (i % 2), accent, 1);
            }
            _ = Paper;
        }

        private static void FillCircle(Color32[] px, int size, int cx, int cy, int r, Color32 color)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= size || y >= size) continue;
                int dx = x - cx, dy = y - cy;
                if (dx * dx + dy * dy <= r2) px[y * size + x] = color;
            }
        }

        private static void HLine(Color32[] px, int size, int y, int x0, int x1, Color32 color, int thickness)
        {
            if (x0 > x1) { int t = x0; x0 = x1; x1 = t; }
            for (int t = -thickness / 2; t <= thickness / 2; t++)
            for (int x = x0; x <= x1; x++)
            {
                int yy = y + t;
                if (x < 0 || yy < 0 || x >= size || yy >= size) continue;
                px[yy * size + x] = color;
            }
        }

        private static void VLine(Color32[] px, int size, int x, int y0, int y1, Color32 color, int thickness)
        {
            if (y0 > y1) { int t = y0; y0 = y1; y1 = t; }
            for (int t = -thickness / 2; t <= thickness / 2; t++)
            for (int y = y0; y <= y1; y++)
            {
                int xx = x + t;
                if (xx < 0 || y < 0 || xx >= size || y >= size) continue;
                px[y * size + xx] = color;
            }
        }

        private static void Line(Color32[] px, int size, int x0, int y0, int x1, int y1, Color32 color, int thickness)
        {
            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy, x = x0, y = y0;
            while (true)
            {
                for (int t = -thickness / 2; t <= thickness / 2; t++)
                for (int u = -thickness / 2; u <= thickness / 2; u++)
                {
                    int xx = x + t, yy = y + u;
                    if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                    px[yy * size + xx] = color;
                }
                if (x == x1 && y == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx) { err += dx; y += sy; }
            }
        }
    }
}
