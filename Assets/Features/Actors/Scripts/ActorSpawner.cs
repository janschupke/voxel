using System;
using System.Collections.Generic;
using UnityEngine;
using Voxel.Debug;
using Voxel.Pure;

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

        private IReadOnlyList<ActorSaveData> _savedActorData;

        public void SetSavedActorData(IReadOnlyList<ActorSaveData> data)
        {
            _savedActorData = data;
        }

        private void Start()
        {
            if (worldBootstrap == null)
                worldBootstrap = FindAnyObjectByType<WorldBootstrap>();

            GameDebugLogger.Log("[ActorSpawner] Start - worldBootstrap=" + (worldBootstrap != null) + ", registry=" + (worldBootstrap?.PlacedObjectRegistry != null));

            if (worldBootstrap == null)
            {
                GameDebugLogger.LogWarning("[ActorSpawner] WorldBootstrap not found, skipping actor spawn.");
                return;
            }
            if (worldBootstrap.PlacedObjectRegistry == null)
            {
                GameDebugLogger.LogWarning("[ActorSpawner] PlacedObjectRegistry is null, skipping actor spawn.");
                return;
            }

            SpawnActorsForBuildings();
        }

        public void DestroyOrphanedActors()
        {
            if (worldBootstrap == null) return;
            var actorsParent = worldBootstrap.GetParentByEntryName("Actors");
            if (actorsParent == null) return;

            var toDestroy = new System.Collections.Generic.List<GameObject>();
            foreach (var ab in actorsParent.GetComponentsInChildren<ActorBehavior>(includeInactive: true))
            {
                if (ab != null && ab.HomeBuildingTransform == null)
                    toDestroy.Add(ab.gameObject);
            }
            foreach (var go in toDestroy)
                UnityEngine.Object.DestroyImmediate(go);
        }

        /// <summary>Spawns an actor for a single building if it has AssignedActor and no actor yet. Use after placing one building.</summary>
        public void SpawnActorForBuildingIfNeeded(PlacedObjectEntry entry, Transform building)
        {
            if (entry == null || building == null || worldBootstrap == null) return;
            if (entry.AssignedActor == null || entry.AssignedActor.Prefab == null) return;
            if (!entry.HasOperationalRange) return;
            SpawnActorForBuilding(entry, building);
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
                    GameDebugLogger.LogWarning($"[ActorSpawner] Entry '{entry.Name}' has AssignedActor '{entry.AssignedActor.Name}' but Prefab is null.");
                    continue;
                }
                if (!entry.HasOperationalRange)
                {
                    GameDebugLogger.Log($"[ActorSpawner] Entry '{entry.Name}' has no operational range, skipping.");
                    continue;
                }

                var parent = worldBootstrap.GetParentByEntryName(entry.Name);
                if (parent == null)
                {
                    GameDebugLogger.Log($"[ActorSpawner] No parent for entry '{entry.Name}' (no buildings placed yet).");
                    continue;
                }

                for (int i = 0; i < parent.childCount; i++)
                {
                    var building = parent.GetChild(i);
                    if (SpawnActorForBuilding(entry, building))
                        totalSpawned++;
                }
            }

            GameDebugLogger.Log($"[ActorSpawner] Entries with AssignedActor: {entriesWithActor}, spawned: {totalSpawned}");
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

            var behavior = GetOrAddBehavior(actorGo, actorDef);

            behavior.Initialize(worldBootstrap, actorDef, building, entry.OperationalRangeCells, entry.OperationalRangeType);

            var saved = FindSavedActorData(entry.Name, building);
            if (saved.HasValue)
            {
                var state = ParseActorState(saved.Value.StateId);
                var position = new Vector3(saved.Value.PosX, saved.Value.PosY, saved.Value.PosZ);
                behavior.RestoreState(state, position);
                if (behavior is CarrierActorBehavior carrier && saved.Value.CarriedItemId >= 0 &&
                    Enum.IsDefined(typeof(Item), saved.Value.CarriedItemId))
                    carrier.SetCarriedItem((Item)saved.Value.CarriedItemId);
            }

            GameDebugLogger.Log($"[ActorSpawner] Spawned {actorDef.Name} for building at {building.position}");
            return true;
        }

        private ActorSaveData? FindSavedActorData(string entryName, Transform building)
        {
            if (_savedActorData == null || _savedActorData.Count == 0) return null;
            var worldScale = new WorldScale(worldBootstrap?.WorldParameters != null ? worldBootstrap.WorldParameters.BlockScale : 1f);
            var (bx, by, bz) = worldScale.WorldToBlock(building.position);
            foreach (var s in _savedActorData)
            {
                if (s.HomeEntryName == entryName && s.HomeBlockX == bx && s.HomeBlockY == by && s.HomeBlockZ == bz)
                    return s;
            }
            return null;
        }

        private static ActorState ParseActorState(int stateId)
        {
            if (stateId >= 0 && Enum.IsDefined(typeof(ActorState), stateId))
                return (ActorState)stateId;
            return ActorState.Idle;
        }

        private static ActorBehavior GetOrAddBehavior(GameObject actorGo, ActorDefinition actorDef)
        {
            var existing = actorGo.GetComponent<ActorBehavior>();
            var neededType = GetBehaviorType(actorDef.BehaviorKind);
            if (existing != null && existing.GetType() == neededType)
                return existing;
            if (existing != null)
            {
                existing.enabled = false;
                UnityEngine.Object.Destroy(existing);
            }
            return (ActorBehavior)actorGo.AddComponent(neededType);
        }

        private static System.Type GetBehaviorType(ActorBehaviorKind kind)
        {
            return kind switch
            {
                ActorBehaviorKind.Carrier => typeof(CarrierActorBehavior),
                ActorBehaviorKind.WheatFarm => typeof(WheatFarmActorBehavior),
                _ => typeof(WoodchuckActorBehavior)
            };
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
