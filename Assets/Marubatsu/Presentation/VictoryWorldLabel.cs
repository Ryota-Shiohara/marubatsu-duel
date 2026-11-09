using UnityEngine;

namespace Marubatsu.Presentation
{
    sealed class VictoryWorldLabel : MonoBehaviour
    {
        TextMesh headline, subtitle, headlineShadow, subtitleShadow;
        Transform sign;
        Font japanese;
        Camera viewCamera;
        float unit;

        public void Build(float cellSize)
        {
            unit = cellSize;
            japanese = MatchCharacterRoster.CreateJapaneseFont(48);
            var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sign = new GameObject("Floating result").transform;
            sign.SetParent(transform, false);
            headline = Text("Headline", fallback, .59f, Vector3.zero, FontStyle.Bold);
            subtitle = Text("Fighter", japanese != null ? japanese : fallback,
                .30f, Vector3.down * unit * .43f, FontStyle.Bold);
            Vector3 shadowOffset = new Vector3(.026f, -.034f, .022f) * unit;
            headlineShadow = Text("Headline shadow", fallback, .59f, shadowOffset, FontStyle.Bold);
            subtitleShadow = Text("Fighter shadow", subtitle.font, .30f,
                subtitle.transform.localPosition + shadowOffset, FontStyle.Bold);
            Hide();
        }

        TextMesh Text(string label, Font font, float size, Vector3 offset, FontStyle style)
        {
            var mesh = new GameObject(label).AddComponent<TextMesh>();
            mesh.transform.SetParent(sign, false);
            mesh.transform.localPosition = offset;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.font = font;
            mesh.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            mesh.fontSize = 64;
            mesh.fontStyle = style;
            mesh.characterSize = unit * size * 10f / mesh.fontSize;
            return mesh;
        }

        public void SetResult(bool isDraw, bool circle, VictoryFighter fighter)
        {
            subtitle.text = isDraw ? "" :
                (japanese != null ? (circle ? "○  " : "×  ") : (circle ? "O  " : "X  ")) +
                MatchCharacterRoster.CharacterName((int)fighter, japanese != null);
            subtitleShadow.text = subtitle.text;
            viewCamera = Camera.main;
        }

        public void Show(string title, Vector3 anchor, Color accent, float opacity, float scale)
        {
            if (opacity <= .005f)
            {
                Hide();
                return;
            }
            sign.gameObject.SetActive(true);
            sign.localPosition = anchor;
            sign.localScale = Vector3.one * scale;
            if (viewCamera != null)
            {
                // The sign follows the paper. Only its reading angle follows the camera.
                sign.rotation = viewCamera.transform.rotation;
            }
            if (headline.text != title) headline.text = headlineShadow.text = title;
            headline.color = new Color(1f, .96f, .78f, opacity);
            headlineShadow.color = subtitleShadow.color = new Color(.08f, .04f, .12f, opacity * .85f);
            accent.a = opacity;
            subtitle.color = accent;
            subtitle.gameObject.SetActive(title == "WINNER!");
            subtitleShadow.gameObject.SetActive(title == "WINNER!");
        }

        public void Hide() => sign.gameObject.SetActive(false);

        void OnDestroy()
        {
            if (japanese != null) Destroy(japanese);
        }
    }
}
