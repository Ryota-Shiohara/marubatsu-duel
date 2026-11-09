using UnityEngine;

namespace Marubatsu.Presentation
{
    public sealed class MatchCharacterRoster : ScriptableObject
    {
        public const string ResourcePath = "MarubatsuMatchCharacters";
        public const int CharacterCount = 3;
        static readonly string[] JapaneseFonts =
            { "Yu Gothic UI", "Meiryo", "Yu Gothic", "メイリオ", "游ゴシック", "Noto Sans CJK JP" };
        static readonly string[] JapaneseNames = { "やがぽん", "みたぬき", "矢口君" };
        static readonly string[] EnglishNames = { "Yagapon", "Mitanuki", "Yaguchi" };

        public GameObject[] Prefabs;
        public Texture2D[] Portraits;

        internal static string CharacterName(int index, bool japanese) =>
            japanese ? JapaneseNames[index] : EnglishNames[index];

        internal static Font CreateJapaneseFont(int size)
        {
            string[] installed = Font.GetOSInstalledFontNames();
            foreach (string candidate in JapaneseFonts)
            {
                if (!System.Array.Exists(installed, name =>
                    string.Equals(name, candidate, System.StringComparison.OrdinalIgnoreCase))) continue;
                Font font = Font.CreateDynamicFontFromOSFont(candidate, size);
                if (font != null) return font;
            }
            return null;
        }

        public bool IsComplete =>
            Prefabs != null && Prefabs.Length == CharacterCount &&
            System.Array.TrueForAll(Prefabs, prefab => prefab != null) &&
            Portraits != null && Portraits.Length == CharacterCount &&
            System.Array.TrueForAll(Portraits, portrait => portrait != null);
    }
}
