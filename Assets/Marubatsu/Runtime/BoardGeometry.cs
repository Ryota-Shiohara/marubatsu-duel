using System;
using UnityEngine;

namespace Marubatsu
{
    /// <summary>Row-major cells viewed from the printed sheet's top edge; target plane is XZ.</summary>
    public static class BoardGeometry
    {
        public const float TargetWidthMeters = 0.18f;
        public const float PlayAreaFraction = 0.60f;

        public static Vector3 CellCenter(int index, Vector2 targetSize)
        {
            if (index < 0 || index >= 9) throw new ArgumentOutOfRangeException(nameof(index));
            if (targetSize.x <= 0 || targetSize.y <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetSize));
            float x = ((index % 3 + 0.5f) / 3f - 0.5f) * targetSize.x * PlayAreaFraction;
            float z = (0.5f - (index / 3 + 0.5f) / 3f) * targetSize.y * PlayAreaFraction;
            return new Vector3(x, 0, z);
        }
    }
}
