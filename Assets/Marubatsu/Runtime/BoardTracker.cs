using System;
using UnityEngine;
using Vuforia;

namespace Marubatsu
{
    [RequireComponent(typeof(ImageTargetBehaviour))]
    public sealed class BoardTracker : MonoBehaviour
    {
        public event Action<bool> TrackingChanged;
        public bool IsTracked { get; private set; }
        ImageTargetBehaviour target;

        void OnEnable()
        {
            target = GetComponent<ImageTargetBehaviour>();
            target.OnTargetStatusChanged += OnStatusChanged;
            OnStatusChanged(target, target.TargetStatus);
        }

        void OnDisable()
        {
            if (target != null) target.OnTargetStatusChanged -= OnStatusChanged;
            SetTracked(false);
        }

        void OnStatusChanged(ObserverBehaviour observer, TargetStatus status)
        {
            Debug.Log($"[BoardTracker] {status.Status} / {status.StatusInfo}", this);
            // Extended tracking is insufficient for accepting handwriting on a visible board.
            SetTracked(status.Status == Status.TRACKED);
        }

        void SetTracked(bool tracked)
        {
            if (IsTracked == tracked) return;
            IsTracked = tracked;
            TrackingChanged?.Invoke(tracked);
        }
    }
}
