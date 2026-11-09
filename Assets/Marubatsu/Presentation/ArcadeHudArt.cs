using System;
using UnityEngine;

namespace Marubatsu.Presentation
{
    /// <summary>Shared screen-space art for the arcade screens and the AR overlay.</summary>
    sealed class ArcadeHudArt : IDisposable
    {
        internal const string DisplayFontPath = "MarubatsuArcade/DelaGothicOne-Regular";
        public static readonly Color Ice = new Color32(0, 196, 255, 255);
        public static readonly Color Flame = new Color32(255, 112, 25, 255);
        public static readonly Color Gold = new Color32(255, 191, 29, 255);
        public static readonly Color Steel = new Color32(105, 143, 183, 255);
        public static readonly Color Mist = new Color32(191, 221, 244, 255);
        static readonly Color Ink = new Color32(4, 11, 27, 255);

        public enum Trim { Blue, Orange, Gold, Steel }

        readonly Texture2D arena;
        readonly RenderTexture[] portraits;
        readonly GUIStyle[] frames;
        readonly GUIStyle text, displayText;
        readonly GUIContent labelContent = new GUIContent();
        readonly Texture2D halo;
        readonly Font systemFont;
        public bool SupportsJapanese { get; }

        public ArcadeHudArt(Texture2D[] sourcePortraits)
        {
            arena = Resources.Load<Texture2D>("MarubatsuArcade/Arena");
            portraits = CutOutPortraits(sourcePortraits);
            frames = new[]
            {
                MakeFrame(Ice), MakeFrame(Flame), MakeFrame(Gold, metal: true), MakeFrame(Steel)
            };
            halo = MakeHalo();
            var displayFont = Resources.Load<Font>(DisplayFontPath);
            systemFont = MatchCharacterRoster.CreateJapaneseFont(26);
            SupportsJapanese = systemFont != null || displayFont != null;
            text = new GUIStyle(GUI.skin.label)
            {
                font = systemFont != null ? systemFont : displayFont,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(),
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            displayText = new GUIStyle(text)
            {
                font = displayFont != null ? displayFont : systemFont,
                fontStyle = displayFont != null ? FontStyle.Normal : FontStyle.Bold,
                richText = true
            };
        }

        static RenderTexture[] CutOutPortraits(Texture2D[] sources)
        {
            // Crop around each of the existing character renders before removing its flat backdrop.
            Vector4[] crops =
            {
                new Vector4(.105f, .182f, .77f, .59f),
                new Vector4(.216f, .097f, .56f, .827f),
                new Vector4(.132f, .128f, .736f, .806f)
            };
            var shader = Resources.Load<Shader>("MarubatsuArcade/ArcadePortrait");
            if (shader == null || !shader.isSupported) return null;
            var material = new Material(shader);
            var key = new Color32(31, 38, 51, 255);
            material.SetVector("_Key", new Vector4(key.r / 255f, key.g / 255f, key.b / 255f, 1f));
            var result = new RenderTexture[sources.Length];
            for (int index = 0; index < sources.Length; index++)
            {
                Vector4 crop = crops[index];
                Texture2D source = sources[index];
                var output = new RenderTexture(Mathf.RoundToInt(source.width * crop.z),
                    Mathf.RoundToInt(source.height * crop.w), 0, RenderTextureFormat.ARGB32);
                output.wrapMode = TextureWrapMode.Clamp;
                material.SetVector("_Crop", crop);
                RenderTexture previous = RenderTexture.active;
                Graphics.Blit(source, output, material);
                RenderTexture.active = previous;
                result[index] = output;
            }
            UnityEngine.Object.Destroy(material);
            return result;
        }

        static GUIStyle MakeFrame(Color accent, bool metal = false)
        {
            const int width = 192, height = 128;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Min(x, width - 1 - x);
                float dy = Mathf.Min(y, height - 1 - y);
                float edge = Mathf.Min(Mathf.Min(dx - 8, dy - 8), (dx + dy - 38) * .7071f);
                float v = (float)y / (height - 1);
                Color color;
                if (edge < 0)
                    color = new Color(accent.r, accent.g, accent.b, .56f * Mathf.Exp(edge * .32f));
                else if (edge < 2)
                    color = Color.Lerp(accent, Color.white, .63f);
                else if (edge < 6)
                    color = Color.Lerp(Ink, accent, edge < 4 ? .87f : .40f);
                else if (edge < 8)
                    color = Color.Lerp(accent, Color.white, .41f);
                else
                {
                    float sheen = Mathf.Max(0, 1 - Mathf.Abs(v - .65f) * 3.1f);
                    color = metal
                        ? Color.Lerp(new Color32(221, 95, 0, 255), new Color32(255, 243, 133, 255), sheen)
                        : Color.Lerp(Ink, accent, .07f + sheen * .14f);
                    if (dx < 43 && ((int)(dx + y * .6f) % 17) < 7)
                        color = Color.Lerp(color, accent, metal ? .40f : .30f);
                    if (!metal) color.a = .94f;
                }
                if (dx == 0 || dy == 0) color.a = 0;
                pixels[y * width + x] = color;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var style = new GUIStyle { border = new RectOffset(48, 48, 31, 31) };
            style.normal.background = texture;
            return style;
        }

        static Texture2D MakeHalo()
        {
            const int size = 96;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2(x, y) / (size - 1) * 2 - Vector2.one;
                float alpha = Mathf.Pow(Mathf.Clamp01(1 - uv.sqrMagnitude), 3f);
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        public void Background(Rect rect)
        {
            if (arena != null) GUI.DrawTexture(rect, arena, ScaleMode.ScaleAndCrop);
            else Fill(rect, Ink);
        }

        public void Frame(Rect rect, Trim trim)
        {
            if (Event.current.type == EventType.Repaint)
                frames[(int)trim].Draw(rect, false, false, false, false);
        }

        public void Label(Rect rect, string value, int size, Color color, bool display = false, string shadow = null)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUIStyle style = display ? displayText : text;
            style.fontSize = size;
            FitText(rect.width - 4, value, style);
            DrawText(new Rect(rect.x + 2, rect.y + (display ? 4 : 2), rect.width, rect.height),
                shadow ?? value, style, new Color(.01f, .02f, .06f, .94f));
            DrawText(rect, value, style, color);
        }

        void DrawText(Rect rect, string value, GUIStyle style, Color color)
        {
            labelContent.text = value;
            style.normal.textColor = color;
            // Draw both the label and its shadow in the same style state, even when hovered.
            style.Draw(rect, labelContent, false, false, false, false);
        }

        void FitText(float availableWidth, string value, GUIStyle style)
        {
            labelContent.text = value;
            float textWidth = style.CalcSize(labelContent).x;
            if (textWidth > availableWidth)
                style.fontSize = Mathf.Max(1, Mathf.FloorToInt(style.fontSize * availableWidth / textWidth));
        }

        public bool Button(Rect rect, string value, int size, Trim trim, bool enabled = true)
        {
            bool previous = GUI.enabled;
            GUI.enabled = previous && enabled;
            Frame(rect, enabled ? trim : Trim.Steel);
            Rect hit = new Rect(rect.x + 5, rect.y + 4, rect.width - 10, rect.height - 8);
            if (enabled && hit.Contains(Event.current.mousePosition))
                Glow(hit, trim == Trim.Gold ? new Color(1, 1, .65f, .24f) : new Color(.6f, .9f, 1, .20f));
            if (enabled && trim == Trim.Gold)
            {
                Label(new Rect(rect.x + 28, rect.y, 60, rect.height), "»", size + 12, Color.white, true);
                Label(new Rect(rect.xMax - 88, rect.y, 60, rect.height), "«", size + 12, Color.white, true);
            }
            if (enabled && trim == Trim.Gold)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    var title = new Rect(rect.x + 88, rect.y, rect.width - 176, rect.height);
                    displayText.fontSize = size;
                    FitText(title.width, value, displayText);
                    DrawText(title, value, displayText, Ink);
                }
            }
            else Label(rect, value, size, enabled ? Color.white : Mist, true);
            bool pressed = GUI.Button(hit, GUIContent.none, GUIStyle.none);
            GUI.enabled = previous;
            return pressed;
        }

        public void Portrait(Rect rect, int index, Texture2D[] originals, bool mirror = false)
        {
            Texture portrait = portraits != null ? portraits[index] : originals[index];
            float scale = Mathf.Min(rect.width / portrait.width, rect.height / portrait.height);
            float width = portrait.width * scale, height = portrait.height * scale;
            var fit = new Rect(rect.center.x - width / 2, rect.center.y - height / 2, width, height);
            GUI.DrawTextureWithTexCoords(fit, portrait, mirror ? new Rect(1, 0, -1, 1) : new Rect(0, 0, 1, 1));
        }

        public void Glow(Rect rect, Color tint) => Paint(rect, halo, tint);

        public static void Fill(Rect rect, Color tint) => Paint(rect, Texture2D.whiteTexture, tint);

        static void Paint(Rect rect, Texture texture, Color tint)
        {
            Color previous = GUI.color;
            GUI.color = previous * tint;
            GUI.DrawTexture(rect, texture);
            GUI.color = previous;
        }

        public static void Line(Vector2 start, Vector2 end, float width, Color tint)
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 span = end - start;
            Matrix4x4 previous = GUI.matrix;
            Quaternion rotation = Quaternion.Euler(0, 0, Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
            GUI.matrix = previous * Matrix4x4.TRS(start, rotation, Vector3.one);
            Fill(new Rect(0, -width / 2, span.magnitude, width), tint);
            GUI.matrix = previous;
        }

        public void Dispose()
        {
            foreach (GUIStyle frame in frames) UnityEngine.Object.Destroy(frame.normal.background);
            if (portraits != null)
                foreach (RenderTexture portrait in portraits)
                {
                    portrait.Release();
                    UnityEngine.Object.Destroy(portrait);
                }
            UnityEngine.Object.Destroy(halo);
            if (systemFont != null) UnityEngine.Object.Destroy(systemFont);
        }
    }
}
