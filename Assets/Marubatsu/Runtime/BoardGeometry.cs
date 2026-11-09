using System;
using UnityEngine;

namespace Marubatsu
{
    /// <summary>Row-major cells viewed from the printed sheet's top edge; target plane is XZ.</summary>
    public static class BoardGeometry
    {
        public const float TargetWidthMeters = 0.27f;
        public const float TargetHeightMeters = 0.18f;

        public static float CellSize(Vector2 targetSize)
        {
            if (targetSize.x <= 0 || targetSize.y <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetSize));
            return Mathf.Min(targetSize.x, targetSize.y) * 0.20f;
        }

        public static Vector3 CellCenter(int index, Vector2 targetSize)
        {
            if (index < 0 || index >= 9) throw new ArgumentOutOfRangeException(nameof(index));
            float cellSize = CellSize(targetSize);
            float x = (index % 3 - 1) * cellSize;
            float z = (1 - index / 3) * cellSize;
            return new Vector3(x, 0, z);
        }
    }
}
