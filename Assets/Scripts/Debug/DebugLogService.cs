using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voxel.Debug
{
    public struct LogEntry
    {
        public string Message;
        public float Timestamp;
        public LogType Type;

        public LogEntry(string message, float timestamp, LogType type)
        {
            Message = message;
            Timestamp = timestamp;
            Type = type;
        }
    }

    /// <summary>
    /// Intercepts Unity log messages and routes them to subscribers (e.g. HUD MessageLog).
    /// Maintains a runtime list of log entries. Use Log() for programmatic debug logs.
    /// </summary>
    public class DebugLogService : MonoBehaviour
    {
        [SerializeField] private int maxEntries = 500;

        private readonly List<LogEntry> _entries = new List<LogEntry>();

        public event Action<LogEntry> LogReceived;

        public IReadOnlyList<LogEntry> Entries => _entries;

        private void OnEnable()
        {
            Application.logMessageReceived += HandleLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= HandleLog;
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            var entry = new LogEntry(logString, Time.realtimeSinceStartup, type);
            _entries.Add(entry);
            if (_entries.Count > maxEntries)
                _entries.RemoveAt(0);

            LogReceived?.Invoke(entry);
        }

        /// <summary>
        /// Log a message programmatically. Does not trigger Unity's debug log (avoids recursion).
        /// </summary>
        public void Log(string message)
        {
            var entry = new LogEntry(message, Time.realtimeSinceStartup, LogType.Log);
            _entries.Add(entry);
            if (_entries.Count > maxEntries)
                _entries.RemoveAt(0);

            LogReceived?.Invoke(entry);
        }

        /// <summary>
        /// Static API for programmatic debug logs. Finds DebugLogService in scene and logs.
        /// Safe to call when no service exists (no-op).
        /// </summary>
        public static void LogMessage(string message)
        {
            var service = UnityEngine.Object.FindAnyObjectByType<DebugLogService>();
            service?.Log(message);
        }
    }
}
