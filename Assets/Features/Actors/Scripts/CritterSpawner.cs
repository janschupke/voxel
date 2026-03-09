using UnityEngine;
using Voxel.Debug;
using Voxel.Pure;

namespace Voxel
{
    /// <summary>
    /// Spawns critters at a world object (environment or building). Attached when placing objects with CritterSpawnerConfig.
    /// </summary>
    public class CritterSpawner : MonoBehaviour
    {
        [SerializeField] private CritterSpawnerConfig config;
        [SerializeField] private WorldBootstrap worldBootstrap;

        private void Start()
        {
            if (worldBootstrap == null)
                worldBootstrap = FindAnyObjectByType<WorldBootstrap>();
            if (config == null || worldBootstrap == null) return;

            SpawnCritters();
        }

        public void Initialize(CritterSpawnerConfig spawnerConfig, WorldBootstrap bootstrap)
        {
            config = spawnerConfig;
            worldBootstrap = bootstrap;
        }

        private void SpawnCritters()
        {
            if (config?.CritterDefinition == null || config.CritterDefinition.Prefab == null) return;
            if (worldBootstrap == null) return;

            var def = config.CritterDefinition;
            var actorsParent = worldBootstrap.GetOrCreateParentForEntry(PlacedObjectKeys.Actors);
            if (actorsParent == null) return;

            var spawnPoint = transform;
            var rangeBlocks = config.BehaviorConfigOverride?.WanderRadiusBlocks ?? 5f;
            var rangeCells = rangeBlocks > 0.001f ? (int)(rangeBlocks + 0.5f) : 5;

            for (int i = 0; i < config.SpawnCount; i++)
            {
                var actorGo = Object.Instantiate(def.Prefab, spawnPoint.position, Quaternion.identity);
                actorGo.name = def.Name + " (Critter)";
                actorGo.transform.SetParent(actorsParent);

                var behavior = ActorSpawner.AddCritterBehavior(actorGo, def);
                if (behavior != null)
                {
                    behavior.Initialize(worldBootstrap, def, spawnPoint, rangeCells, OperationalRangeType.Square);
                    GameDebugLogger.Log($"[CritterSpawner] Spawned {def.Name} at {spawnPoint.position}");
                }
            }
        }
    }
}
