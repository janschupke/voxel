using System.Collections.Generic;
using UnityEngine;
using Voxel.Core;
using Voxel.Pathfinding;

namespace Voxel
{
    /// <summary>
    /// Base behavior for actors operating in a building's range. Handles state machine, pathing, and movement.
    /// Subclass to implement job-specific target selection (e.g. WoodchuckActorBehavior for trees).
    /// </summary>
    public abstract class ActorBehavior : MonoBehaviour
    {
        [SerializeField] private WorldBootstrap worldBootstrap;

        protected WorldBootstrap WorldBootstrap => worldBootstrap;
        protected ActorDefinition Definition { get; private set; }
        protected Transform HomeBuilding { get; private set; }
        public Transform HomeBuildingTransform => HomeBuilding;
        protected float RangeBlocks { get; private set; }
        protected WorldScale WorldScale { get; private set; }

        private ActorState _state = ActorState.Idle;
        private ActorState _prevState = ActorState.Idle;
        private Vector3 _targetWorld;
        private IReadOnlyList<GridNode> _path;
        private int _pathIndex;
        private float _workTimer;
        private float _blockedTimer;
        private float _fullCheckTimer;

        public void Initialize(WorldBootstrap bootstrap, ActorDefinition definition, Transform homeBuilding, float rangeBlocks)
        {
            worldBootstrap = bootstrap;
            Definition = definition;
            HomeBuilding = homeBuilding;
            RangeBlocks = rangeBlocks;
            WorldScale = new WorldScale(bootstrap.WorldParameters != null ? bootstrap.WorldParameters.BlockScale : 1f);
            transform.position = homeBuilding.position;
            UnityEngine.Debug.Log($"[Actor] {gameObject.name} Initialize: home={homeBuilding.position} range={rangeBlocks} pathing={definition.PathingMode}");
        }

        protected virtual void Update()
        {
            if (worldBootstrap == null || Definition == null || HomeBuilding == null) return;

            if (_state != _prevState)
            {
                UnityEngine.Debug.Log($"[Actor] {gameObject.name} state: {_prevState} -> {_state} (visible={_state == ActorState.GoingToTarget || _state == ActorState.WorkingOutside || _state == ActorState.Returning})");
                _prevState = _state;
            }

            switch (_state)
            {
                case ActorState.Idle:
                    UpdateIdle();
                    break;
                case ActorState.Blocked:
                    UpdateBlocked();
                    break;
                case ActorState.GoingToTarget:
                    UpdateGoingToTarget();
                    break;
                case ActorState.WorkingOutside:
                    UpdateWorkingOutside();
                    break;
                case ActorState.Returning:
                    UpdateReturning();
                    break;
                case ActorState.WorkingInside:
                    UpdateWorkingInside();
                    break;
                case ActorState.Full:
                    UpdateFull();
                    break;
            }

            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            bool visible = _state == ActorState.GoingToTarget || _state == ActorState.WorkingOutside || _state == ActorState.Returning;
            foreach (var r in GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                if (r.enabled != visible)
                    r.enabled = visible;
            }
        }

        private void UpdateIdle()
        {
            var (target, hadCandidates) = TryGetReachableTarget();
            if (target.HasValue)
            {
                _targetWorld = target.Value;
                _path = BuildPathTo(HomeBuilding.position, target.Value);
                if (_path != null && _path.Count > 0)
                {
                    _pathIndex = 0;
                    _state = ActorState.GoingToTarget;
                }
                else
                {
                    UnityEngine.Debug.Log($"[Actor] {gameObject.name} Idle: found target but path is null/empty -> Blocked");
                    _state = ActorState.Blocked;
                    _blockedTimer = Definition.BlockedRetryDelaySeconds;
                }
            }
            else if (hadCandidates)
            {
                UnityEngine.Debug.Log($"[Actor] {gameObject.name} Idle: trees in range but no valid path -> Blocked");
                _state = ActorState.Blocked;
                _blockedTimer = Definition.BlockedRetryDelaySeconds;
            }
        }

        private void UpdateBlocked()
        {
            _blockedTimer -= Time.deltaTime;
            if (_blockedTimer <= 0f)
            {
                UnityEngine.Debug.Log($"[Actor] {gameObject.name} Blocked: retry -> Idle");
                _state = ActorState.Idle;
            }
        }

        private void UpdateGoingToTarget()
        {
            if (_path == null || _pathIndex >= _path.Count)
            {
                _state = ActorState.WorkingOutside;
                _workTimer = Definition.WorkDurationOutside;
                return;
            }

            var node = _path[_pathIndex];
            int topY = PlacementUtility.GetTopSolidY(worldBootstrap.Grid, node.X, node.Z, worldBootstrap.Grid.Height);
            if (topY < 0)
            {
                _pathIndex++;
                return;
            }

            int surfaceY = topY + 1;
            var targetPos = WorldScale.BlockToWorld(node.X + 0.5f, surfaceY, node.Z + 0.5f);
            float blockScale = WorldScale.BlockScale;
            float moveDist = Definition.MoveSpeed * blockScale * Time.deltaTime;

            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveDist);

            if (Vector3.Distance(transform.position, targetPos) < 0.1f * blockScale)
                _pathIndex++;
        }

        private void UpdateWorkingOutside()
        {
            _workTimer -= Time.deltaTime;
            if (_workTimer <= 0f)
            {
                _path = BuildPathTo(transform.position, HomeBuilding.position);
                _pathIndex = 0;
                _state = ActorState.Returning;
            }
        }

        private void UpdateReturning()
        {
            if (_path == null || _pathIndex >= _path.Count)
            {
                _state = ActorState.WorkingInside;
                _workTimer = Definition.WorkDurationInside;
                return;
            }

            var node = _path[_pathIndex];
            int topY = PlacementUtility.GetTopSolidY(worldBootstrap.Grid, node.X, node.Z, worldBootstrap.Grid.Height);
            if (topY < 0)
            {
                _pathIndex++;
                return;
            }

            int surfaceY = topY + 1;
            var targetPos = WorldScale.BlockToWorld(node.X + 0.5f, surfaceY, node.Z + 0.5f);
            float blockScale = WorldScale.BlockScale;
            float moveDist = Definition.MoveSpeed * blockScale * Time.deltaTime;

            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveDist);

            if (Vector3.Distance(transform.position, targetPos) < 0.1f * blockScale)
                _pathIndex++;
        }

        private void UpdateWorkingInside()
        {
            _workTimer -= Time.deltaTime;
            if (_workTimer <= 0f)
            {
                OnWorkCompletedInside();
                _state = IsBuildingInventoryFull() ? ActorState.Full : ActorState.Idle;
            }
        }

        private void UpdateFull()
        {
            _fullCheckTimer -= Time.deltaTime;
            if (_fullCheckTimer <= 0f)
            {
                _fullCheckTimer = Definition.BlockedRetryDelaySeconds;
                if (!IsBuildingInventoryFull())
                    _state = ActorState.Idle;
            }
        }

        /// <summary>
        /// Called when the actor finishes working inside the building. Override to produce items, etc.
        /// </summary>
        protected virtual void OnWorkCompletedInside() { }

        /// <summary>
        /// Returns true when the building inventory is at capacity and the actor should stop working.
        /// Override in subclasses that produce items (e.g. WoodchuckActorBehavior).
        /// </summary>
        protected virtual bool IsBuildingInventoryFull()
        {
            return false;
        }

        /// <summary>
        /// Try to get a reachable target. Returns (target, hadCandidates).
        /// If target is non-null, a valid path exists. If null and hadCandidates, no path to any candidate (Blocked).
        /// If null and !hadCandidates, no candidates (stay Idle).
        /// </summary>
        protected abstract (Vector3? Target, bool HadCandidates) TryGetReachableTarget();

        protected IReadOnlyList<GridNode> BuildPathTo(Vector3 fromWorld, Vector3 toWorld)
        {
            var graph = GetPathGraph();
            if (graph == null) return null;

            var (fx, _, fz) = WorldScale.WorldToBlock(fromWorld);
            var (tx, _, tz) = WorldScale.WorldToBlock(toWorld);

            var start = FindWalkableAdjacent(fx, fz, graph);
            var goal = FindWalkableAdjacent(tx, tz, graph);

            if (!start.HasValue)
            {
                UnityEngine.Debug.Log($"[Actor] {gameObject.name} BuildPath: no walkable block adjacent to start ({fx},{fz})");
                return null;
            }
            if (!goal.HasValue)
            {
                UnityEngine.Debug.Log($"[Actor] {gameObject.name} BuildPath: no walkable block adjacent to goal ({tx},{tz})");
                return null;
            }

            return AStarPathfinder.FindPath(graph, start.Value, goal.Value);
        }

        private GridNode? FindWalkableAdjacent(int bx, int bz, IGridGraph<GridNode> graph)
        {
            var node = new GridNode(bx, bz);
            if (graph.IsWalkable(node))
                return node;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var n = new GridNode(bx + dx, bz + dz);
                    if (graph.IsWalkable(n))
                        return n;
                }
            }
            return null;
        }

        private IGridGraph<GridNode> GetPathGraph()
        {
            var grid = worldBootstrap.Grid;
            var roadOverlay = worldBootstrap.GetRoadOverlay();
            int waterLevelY = worldBootstrap.WaterConfig != null
                ? worldBootstrap.WaterConfig.GetWaterLevelY(grid.Height)
                : 0;

            bool isBlockValid(int x, int y, int z) =>
                !worldBootstrap.HasBlockingObjectAtBlock(x, y, z);

            return Definition.PathingMode switch
            {
                ActorPathingMode.Road => new RoadPathGraph(grid, waterLevelY, roadOverlay),
                ActorPathingMode.Free => new SurfacePathGraph(grid, waterLevelY, isBlockValid),
                ActorPathingMode.Smart => new SmartSurfacePathGraph(grid, waterLevelY, roadOverlay, isBlockValid),
                _ => new SurfacePathGraph(grid, waterLevelY, isBlockValid)
            };
        }
    }
}
