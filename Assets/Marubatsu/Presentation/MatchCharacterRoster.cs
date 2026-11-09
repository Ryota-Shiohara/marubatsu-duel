using UnityEngine;

namespace Marubatsu.Presentation
{
    public sealed class MatchCharacterRoster : ScriptableObject
    {
        public const string ResourcePath = "MarubatsuMatchCharacters";
        public const int CharacterCount = 3;

        public GameObject[] Prefabs;
        public Texture2D[] Portraits;

        public bool IsComplete =>
            Prefabs != null && Prefabs.Length == CharacterCount &&
            System.Array.TrueForAll(Prefabs, prefab => prefab != null) &&
            Portraits != null && Portraits.Length == CharacterCount &&
            System.Array.TrueForAll(Portraits, portrait => portrait != null);
    }
}
