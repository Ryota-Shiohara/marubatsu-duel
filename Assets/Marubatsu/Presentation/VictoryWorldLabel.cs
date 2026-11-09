using UnityEngine;

namespace Marubatsu.Presentation
{
    sealed class VictoryWorldLabel : MonoBehaviour
    {
        static readonly Color Ink = new Color32(8, 12, 30, 255);
        static readonly Vector2[] OutlineDirections =
        {
            Vector2.up, Vector2.down, Vector2.left, Vector2.right,
            new Vector2(.707f, .707f), new Vector2(-.707f, .707f),
            new Vector2(.707f, -.707f), new Vector2(-.707f, -.707f)
        };

        readonly TextMesh[] nameOutline = new TextMesh[OutlineDirections.Length];
        TextMesh headline, headlineShadow, winnerName, nameAccent;
        Transform sign, header, nameGroup;
        Font systemFont;
        bool supportsJapanese;
        Camera viewCamera;
        float unit;

        public void Build(float cellSize)
        {
            unit = cellSize;
            Font display = Resources.Load<Font>(ArcadeHudArt.DisplayFontPath);
            if (display == null) systemFont = MatchCharacterRoster.CreateJapaneseFont(48);
            supportsJapanese = display != null || systemFont != null;
            Font font = display != null ? display : systemFont;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            FontStyle style = display != null ? FontStyle.Normal : FontStyle.Bold;

            sign = Group("Floating result", transform);
            header = Group("Result header", sign);
            nameGroup = Group("Winner name", sign);
            headline = Text(header, "Headline", font, .59f, Vector3.zero, style);
            headlineShadow = Text(header, "Headline shadow", font, .59f, new Vector3(.035f, -.045f, .04f), style);
            winnerName = Text(nameGroup, "Name", font, .94f, Vector3.zero, style);
            nameAccent = Text(nameGroup, "Name accent", font, .94f, new Vector3(.047f, -.090f, .08f), style);
            for (int i = 0; i < nameOutline.Length; i++)
            {
                Vector2 edge = OutlineDirections[i] * .048f;
                nameOutline[i] = Text(nameGroup, "Name outline", font, .94f,
                    new Vector3(edge.x, edge.y, .04f), style);
            }
            Hide();
        }

        static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        TextMesh Text(Transform parent, string label, Font font, float size, Vector3 offset, FontStyle style)
        {
            var mesh = new GameObject(label).AddComponent<TextMesh>();
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = offset * unit;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.font = font;
            mesh.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            mesh.fontSize = 96;
            mesh.fontStyle = style;
            mesh.characterSize = unit * size * 10f / mesh.fontSize;
            return mesh;
        }

        public void SetResult(bool isDraw, bool circle, VictoryFighter fighter)
        {
            string symbol = supportsJapanese ? (circle ? "○" : "×") : (circle ? "O" : "X");
            winnerName.text = isDraw ? "" :
                $"{symbol} {MatchCharacterRoster.CharacterName((int)fighter, supportsJapanese)}";
            nameAccent.text = winnerName.text;
            foreach (TextMesh outline in nameOutline) outline.text = winnerName.text;
            viewCamera = Camera.main;
        }

        public void Show(string title, Vector3 anchor, float opacity, float scale)
        {
            if (!Place(anchor, opacity, scale)) return;
            nameGroup.gameObject.SetActive(false);
            header.localPosition = Vector3.zero;
            header.localScale = Vector3.one;
            SetHeadline(title, new Color(1f, .96f, .78f, opacity));
        }

        public void ShowWinner(Vector3 anchor, Color accent, float progress)
        {
            float ease = Mathf.SmoothStep(0, 1, progress);
            float opacity = Mathf.Clamp01(progress * 5f);
            if (!Place(anchor, opacity, 1f)) return;
            header.localPosition = Vector3.up * (unit * .78f);
            header.localScale = Vector3.one * .68f;
            SetHeadline("WINNER!", new Color(1f, .91f, .65f, opacity));

            nameGroup.gameObject.SetActive(true);
            float punch = Mathf.Sin(ease * Mathf.PI) * .26f;
            nameGroup.localScale = Vector3.one * (Mathf.Lerp(.65f, 1f, ease) + punch);
            nameGroup.localPosition = Vector3.down * (unit * .23f * (1f - ease));
            var gold = new Color32(255, 211, 60, 255);
            winnerName.color = WithOpacity(Color.Lerp(gold, Color.white, 1f - ease), opacity);
            nameAccent.color = WithOpacity(accent, opacity);
            foreach (TextMesh outline in nameOutline) outline.color = WithOpacity(Ink, opacity);
        }

        bool Place(Vector3 anchor, float opacity, float scale)
        {
            if (opacity <= .005f)
            {
                Hide();
                return false;
            }
            sign.gameObject.SetActive(true);
            sign.localPosition = anchor;
            sign.localScale = Vector3.one * scale;
            if (viewCamera != null)
            {
                // The sign follows the paper. Only its reading angle follows the camera.
                sign.rotation = viewCamera.transform.rotation;
            }
            return true;
        }

        void SetHeadline(string title, Color color)
        {
            if (headline.text != title) headline.text = headlineShadow.text = title;
            headline.color = color;
            headlineShadow.color = WithOpacity(Ink, color.a);
        }

        static Color WithOpacity(Color color, float opacity)
        {
            color.a = opacity;
            return color;
        }

        public void Hide() => sign.gameObject.SetActive(false);

        void OnDestroy()
        {
            if (systemFont != null) Destroy(systemFont);
        }
    }
}
