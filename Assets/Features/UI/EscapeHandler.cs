using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Voxel
{
    /// <summary>
    /// Central handler for ESC key. Processes registered handlers by priority (high to low).
    /// First handler that returns true consumes the ESC; lower-priority handlers are not called.
    /// Add to a GameObject in the scene (e.g. HUD). Controllers register in Start, unregister in OnDestroy.
    /// </summary>
    public class EscapeHandler : MonoBehaviour
    {
        public static EscapeHandler Instance { get; private set; }

        /// <summary>Priority for UI overlay windows (e.g. Inventory panel).</summary>
        public const int PriorityUiOverlay = 100;

        /// <summary>Priority for placement/removal mode cancellation.</summary>
        public const int PriorityModeCancel = 75;

        /// <summary>Priority for clearing selection.</summary>
        public const int PriorityDeselect = 50;

        private readonly List<(int Priority, Func<bool> Handler)> _handlers = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Register a handler. Returns true if ESC was consumed. Higher priority runs first.</summary>
        public void Register(int priority, Func<bool> handler)
        {
            if (handler == null) return;
            _handlers.Add((priority, handler));
        }

        /// <summary>Unregister a handler. Must pass the same delegate reference used in Register.</summary>
        public void Unregister(Func<bool> handler)
        {
            if (handler == null) return;
            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                if (_handlers[i].Handler == handler)
                {
                    _handlers.RemoveAt(i);
                    return;
                }
            }
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
                return;

            var ordered = _handlers.OrderByDescending(h => h.Priority).ToList();
            foreach (var (_, handler) in ordered)
            {
                try
                {
                    if (handler())
                        return;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }
    }
}
