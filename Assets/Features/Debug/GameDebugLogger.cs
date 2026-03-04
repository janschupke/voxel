using UnityEngine;

namespace Voxel.Debug
{
    /// <summary>
    /// Toggleable debug logger. Routes to DebugLogService (UI) when enabled.
    /// Never outputs to standard out (Unity console). Use for actor, spawner, terrain debug messages.
    /// </summary>
    public static class GameDebugLogger
    {
        private static DebugLogService _service;
        private static bool _enabled = true;

        public static bool IsEnabled => _enabled;

        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        public static void RegisterService(DebugLogService service)
        {
            _service = service;
        }

        public static void Log(string message)
        {
            if (!_enabled || _service == null) return;
            _service.Log(message);
        }

        public static void LogWarning(string message)
        {
            if (!_enabled || _service == null) return;
            _service.Log($"[WARN] {message}");
        }
    }
}
