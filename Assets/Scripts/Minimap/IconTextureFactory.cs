using System.Collections.Generic;
using UnityEngine;

namespace TreasureHunt.Minimap
{
    /// <summary>
    /// Generates simple procedural circular sprites for minimap icons so that no extra art assets
    /// are required to ship the feature. Each icon type maps to a distinct fill+outline combo and
    /// the resulting <see cref="Sprite"/> instances are cached per type.
    /// </summary>
    public sealed class IconTextureFactory
    {
        private const int Size = 128;
        private const float OuterRadius = 0.48f;
        private const float InnerRadius = 0.40f;
        private static readonly Color OutlineColor = new Color(0f, 0f, 0f, 0.85f);

        private readonly Dictionary<(MinimapIconType type, int tintKey), Sprite> _cache =
            new Dictionary<(MinimapIconType, int), Sprite>();

        public Sprite GetSprite(MinimapIconType type, Color teamTint)
        {
            int tintKey = TintKey(teamTint);
            var key = (type, tintKey);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            Color fill = ResolveFillColor(type, teamTint);
            Sprite sprite = BuildCircleSprite(fill, type);
            _cache[key] = sprite;
            return sprite;
        }

        private static int TintKey(Color c)
        {
            int r = Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f);
            int g = Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f);
            int b = Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f);
            int a = Mathf.RoundToInt(Mathf.Clamp01(c.a) * 255f);
            return (r << 24) | (g << 16) | (b << 8) | a;
        }

        private static Color ResolveFillColor(MinimapIconType type, Color teamTint)
        {
            return type switch
            {
                MinimapIconType.Agent => teamTint,
                MinimapIconType.Base => teamTint,
                MinimapIconType.Golem => new Color(0.65f, 0.25f, 0.25f, 1f),
                MinimapIconType.Treasure => new Color(1f, 0.84f, 0.20f, 1f),
                _ => Color.white
            };
        }

        private static Sprite BuildCircleSprite(Color fill, MinimapIconType type)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
                name = $"MinimapIcon_{type}"
            };

            var pixels = new Color[Size * Size];
            Vector2 center = new Vector2(Size * 0.5f, Size * 0.5f);
            float outerSq = (Size * OuterRadius) * (Size * OuterRadius);
            float innerSq = (Size * InnerRadius) * (Size * InnerRadius);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float distSq = dx * dx + dy * dy;

                    Color px;
                    if (distSq > outerSq) px = Color.clear;
                    else if (distSq > innerSq) px = OutlineColor;
                    else px = fill;

                    pixels[y * Size + x] = px;
                }
            }

            ApplyTypeMark(pixels, type);

            tex.SetPixels(pixels);
            tex.Apply(false, true);

            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, Size, Size),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: Size);
            sprite.name = $"MinimapIconSprite_{type}";
            return sprite;
        }

        private static void ApplyTypeMark(Color[] pixels, MinimapIconType type)
        {
            switch (type)
            {
                case MinimapIconType.Treasure:
                    DrawCenterStar(pixels, new Color(0.6f, 0.4f, 0f, 1f));
                    break;
                case MinimapIconType.Golem:
                    DrawCenterCross(pixels, new Color(0.1f, 0.05f, 0.05f, 1f));
                    break;
                case MinimapIconType.Base:
                    DrawCenterDot(pixels, new Color(1f, 1f, 1f, 1f));
                    break;
            }
        }

        private static void DrawCenterDot(Color[] pixels, Color color)
        {
            int half = Size / 2;
            int r = Size / 8;
            int rSq = r * r;
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y <= rSq)
                        pixels[(half + y) * Size + (half + x)] = color;
                }
            }
        }

        private static void DrawCenterCross(Color[] pixels, Color color)
        {
            int half = Size / 2;
            int len = Size / 4;
            int thickness = Size / 16;
            for (int t = -thickness; t <= thickness; t++)
            {
                for (int i = -len; i <= len; i++)
                {
                    int x1 = half + i;
                    int y1 = half + t;
                    int x2 = half + t;
                    int y2 = half + i;
                    pixels[y1 * Size + x1] = color;
                    pixels[y2 * Size + x2] = color;
                }
            }
        }

        private static void DrawCenterStar(Color[] pixels, Color color)
        {
            int half = Size / 2;
            int len = Size / 4;
            int thickness = Size / 18;
            for (int t = -thickness; t <= thickness; t++)
            {
                for (int i = -len; i <= len; i++)
                {
                    int dx = i + t;
                    int dy = i - t;
                    if (Mathf.Abs(dx) >= Size / 2 || Mathf.Abs(dy) >= Size / 2) continue;

                    int x1 = half + i;
                    int y1 = half + t;
                    int x2 = Mathf.Clamp(half + dx, 0, Size - 1);
                    int y2 = Mathf.Clamp(half + dy, 0, Size - 1);
                    int x3 = Mathf.Clamp(half - dx, 0, Size - 1);
                    int y3 = Mathf.Clamp(half + dy, 0, Size - 1);
                    pixels[y1 * Size + x1] = color;
                    pixels[y2 * Size + x2] = color;
                    pixels[y3 * Size + x3] = color;
                }
            }
        }
    }
}
