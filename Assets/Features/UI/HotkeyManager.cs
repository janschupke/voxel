using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Voxel
{
    /// <summary>
    /// Display-only info for a hotkey binding (used by Hotkeys UI).
    /// </summary>
    public readonly struct HotkeyDisplayInfo
    {
        public string DisplayKey { get; }
        public string Description { get; }
        public string Context { get; }

        public HotkeyDisplayInfo(string displayKey, string description, string context = null)
        {
            DisplayKey = displayKey ?? "";
            Description = description ?? "";
            Context = context ?? "";
        }
    }

    /// <summary>
    /// Centralized hotkey manager. Registers key bindings, dispatches on input, and exposes bindings for UI display.
    /// Replaces EscapeHandler. Controllers register in Start, unregister in OnDestroy.
    /// </summary>
    public class HotkeyManager : MonoBehaviour
    {
        public static HotkeyManager Instance { get; private set; }

        /// <summary>Priority for UI overlay windows (e.g. Inventory panel).</summary>
        public const int PriorityUiOverlay = 100;

        /// <summary>Priority for placement/removal mode cancellation.</summary>
        public const int PriorityModeCancel = 75;

        /// <summary>Priority for clearing selection.</summary>
        public const int PriorityDeselect = 50;

        /// <summary>Default priority for single-key bindings (I, R, F2).</summary>
        public const int PriorityDefault = 0;

        private readonly List<Binding> _bindings = new();

        private struct Binding
        {
            public Key Key;
            public string DisplayKey;
            public string Description;
            public string Context;
            public Func<bool> Action;
            public int Priority;
        }

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

        /// <summary>Register a hotkey. For keys with multiple handlers (e.g. ESC), higher priority runs first; first true consumes.</summary>
        public void Register(Key key, string displayKey, string description, string context, Func<bool> action, int priority = PriorityDefault)
        {
            if (action == null) return;
            _bindings.Add(new Binding
            {
                Key = key,
                DisplayKey = displayKey ?? key.ToString(),
                Description = description ?? "",
                Context = context ?? "",
                Action = action,
                Priority = priority
            });
        }

        /// <summary>Unregister by delegate reference. Must pass the same delegate used in Register.</summary>
        public void Unregister(Func<bool> action)
        {
            if (action == null) return;
            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                if (_bindings[i].Action == action)
                {
                    _bindings.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>Returns all bindings for UI display (no action delegates). Excludes ESC. Deduplicated by key+description+context.</summary>
        public IReadOnlyList<HotkeyDisplayInfo> GetAllBindings()
        {
            var seen = new HashSet<string>();
            var result = new List<HotkeyDisplayInfo>();
            foreach (var b in _bindings.OrderByDescending(x => x.Priority).ThenBy(x => x.Key.ToString()))
            {
                if (b.Key == Key.Escape) continue;
                var key = $"{b.Key}|{b.Description}|{b.Context}";
                if (seen.Add(key))
                    result.Add(new HotkeyDisplayInfo(b.DisplayKey, b.Description, string.IsNullOrEmpty(b.Context) ? null : b.Context));
            }
            return result;
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            var keysToCheck = _bindings.Select(b => b.Key).Distinct().ToList();
            foreach (var key in keysToCheck)
            {
                if (!Keyboard.current[key].wasPressedThisFrame) continue;

                var bindingsForKey = _bindings.Where(b => b.Key == key).OrderByDescending(b => b.Priority).ToList();
                foreach (var b in bindingsForKey)
                {
                    try
                    {
                        if (b.Action())
                            break;
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogException(ex);
                    }
                }
            }
        }
    }
}
