using System;
using UnityEngine;

namespace Voxel
{
    /// <summary>
    /// Event raised when something happens at a world object (building, tree, etc.).
    /// </summary>
    public readonly struct WorldObjectEvent
    {
        public Transform Source { get; }
        public string EventType { get; }
        public object Data { get; }

        public WorldObjectEvent(Transform source, string eventType, object data)
        {
            Source = source;
            EventType = eventType ?? string.Empty;
            Data = data;
        }
    }

    /// <summary>
    /// Event types for WorldObjectEvent.
    /// </summary>
    public static class WorldObjectEventTypes
    {
        /// <summary>Emitted when structures produce inventory items. Payload: (Item item, int amount).</summary>
        public const string UnitProduced = "UnitProduced";
    }

    /// <summary>
    /// Static event bus for world-object events. Any system can raise; consumers subscribe.
    /// </summary>
    public static class WorldObjectEventBus
    {
        public static event Action<WorldObjectEvent> OnEvent;

        public static void Raise(WorldObjectEvent evt) => OnEvent?.Invoke(evt);
    }
}
