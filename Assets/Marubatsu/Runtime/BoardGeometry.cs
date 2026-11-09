using System;
using UnityEngine;

namespace Marubatsu
{
    /// <summary>Printed board positions in target-local space, with the target plane on XZ.</summary>
    public static class BoardGeometry
    {
        public const float TargetWidthMeters = 0.27f;
        public const float TargetHeightMeters = 0.18f;
        // Board_A4.svg uses x=410 and x=2290 in the target's 2700-wide viewBox.
        const float TurnStandOffset = (1350f - 410f) / 2700f;

        public static Vector3 TurnStandCenter(bool onLeft, Vector2 targetSize)
        {
            float x = targetSize.x * TurnStandOffset;
            return new Vector3(onLeft ? -x : x, 0f, 0f);
        }

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
