using UnityEngine;

namespace Voxel
{
    /// <summary>
    /// Spawns actors for buildings that have AssignedActor. Runs after WorldBootstrap loads the world.
    /// </summary>
    [AddComponentMenu("Voxel/Actor Spawner")]
    [DefaultExecutionOrder(100)]
    public class ActorSpawner : MonoBehaviour
    {
        [SerializeField] private WorldBootstrap worldBootstrap;

        private void Start()
        {
            if (worldBootstrap == null)
                worldBootstrap = FindAnyObjectByType<WorldBootstrap>();

            UnityEngine.Debug.Log("[ActorSpawner] Start - worldBootstrap=" + (worldBootstrap != null) + ", registry=" + (worldBootstrap?.PlacedObjectRegistry != null));

            if (worldBootstrap == null)
            {
                UnityEngine.Debug.LogWarning("[ActorSpawner] WorldBootstrap not found, skipping actor spawn.");
                return;
            }
            if (worldBootstrap.PlacedObjectRegistry == null)
            {
                UnityEngine.Debug.LogWarning("[ActorSpawner] PlacedObjectRegistry is null, skipping actor spawn.");
                return;
            }

            SpawnActorsForBuildings();
        }

        public void SpawnActorsForBuildings()
        {
            var registry = worldBootstrap.PlacedObjectRegistry;
            if (registry == null) return;

            int totalSpawned = 0;
            int entriesWithActor = 0;
            foreach (var entry in registry.Entries)
            {
                if (entry == null) continue;
                if (entry.AssignedActor == null) continue;
                entriesWithActor++;
                if (entry.AssignedActor.Prefab == null)
                {
                    UnityEngine.Debug.LogWarning($"[ActorSpawner] Entry '{entry.Name}' has AssignedActor '{entry.AssignedActor.Name}' but Prefab is null.");
                    continue;
                }
                if (!entry.HasOperationalRange)
                {
                    UnityEngine.Debug.Log($"[ActorSpawner] Entry '{entry.Name}' has no operational range, skipping.");
                    continue;
                }

                var parent = worldBootstrap.GetParentByEntryName(entry.Name);
                if (parent == null)
                {
                    UnityEngine.Debug.Log($"[ActorSpawner] No parent for entry '{entry.Name}' (no buildings placed yet).");
                    continue;
                }

                for (int i = 0; i < parent.childCount; i++)
                {
                    var building = parent.GetChild(i);
                    if (SpawnActorForBuilding(entry, building))
                        totalSpawned++;
                }
            }

            UnityEngine.Debug.Log($"[ActorSpawner] Entries with AssignedActor: {entriesWithActor}, spawned: {totalSpawned}");
        }

        private bool SpawnActorForBuilding(PlacedObjectEntry entry, Transform building)
        {
            if (HasActorForBuilding(building))
                return false;

            var actorDef = entry.AssignedActor;
            var prefab = actorDef.Prefab;

            var actorGo = Instantiate(prefab, building.position, Quaternion.identity);
            actorGo.name = actorDef.Name + " (Actor)";

            var actorsParent = worldBootstrap.GetOrCreateParentForEntry("Actors");
            if (actorsParent != null)
                actorGo.transform.SetParent(actorsParent);

            var behavior = actorGo.GetComponent<ActorBehavior>();
            if (behavior == null)
                behavior = actorGo.AddComponent<WoodchuckActorBehavior>();

            behavior.Initialize(worldBootstrap, actorDef, building, entry.OperationalRangeInBlocks);
            UnityEngine.Debug.Log($"[ActorSpawner] Spawned {actorDef.Name} for building at {building.position}");
            return true;
        }

        private bool HasActorForBuilding(Transform building)
        {
            var actorsParent = worldBootstrap.GetParentByEntryName("Actors");
            if (actorsParent == null) return false;

            foreach (var ab in actorsParent.GetComponentsInChildren<ActorBehavior>(includeInactive: true))
            {
                if (ab.HomeBuildingTransform == building)
                    return true;
            }
            return false;
        }
    }
}
