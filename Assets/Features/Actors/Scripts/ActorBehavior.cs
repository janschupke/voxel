using System.Collections.Generic;
using UnityEngine;
using Voxel.Pure;
using Voxel.Debug;
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
        /// <summary>Range as integer cell count for OperationalRange.IsCellInRange.</summary>
        protected int RangeCells => RangeBlocks > 0.001f ? (int)(RangeBlocks + 0.5f) : 0;
        protected OperationalRangeType RangeType { get; private set; }
        protected WorldScale WorldScale { get; private set; }

        private ActorState _state = ActorState.Idle;
        private ActorState _prevState = ActorState.Idle;
        private Vector3 _targetWorld;
        private Renderer[] _cachedRenderers;
        private IReadOnlyList<GridNode> _path;
        private int _pathIndex;
        private float _workTimer;
        private float _blockedTimer;
        private float _fullCheckTimer;
        private float _idleCheckCooldown;

        public ActorState CurrentState => _state;

        private void Awake()
        {
            _cachedRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        }

        public void Initialize(WorldBootstrap bootstrap, ActorDefinition definition, Transform homeBuilding, float rangeBlocks, OperationalRangeType rangeType = OperationalRangeType.Square)
        {
            worldBootstrap = bootstrap;
            Definition = definition;
            HomeBuilding = homeBuilding;
            RangeBlocks = rangeBlocks;
            RangeType = rangeType;
            WorldScale = new WorldScale(bootstrap.WorldParameters != null ? bootstrap.WorldParameters.BlockScale : 1f);
            transform.position = homeBuilding.position;
        }

        /// <summary>Restore state from persistence. Clears path and timers.</summary>
        public virtual void RestoreState(ActorState state, Vector3 position)
        {
            _state = state;
            _prevState = state;
            transform.position = position;
            _path = null;
            _pathIndex = 0;
            _workTimer = 0f;
            _blockedTimer = 0f;
            _fullCheckTimer = 0f;
            _idleCheckCooldown = 0f;
            _cachedRenderers = null;
        }

        protected virtual void Update()
        {
            if (worldBootstrap == null || Definition == null || HomeBuilding == null) return;

            if (_state != _prevState)
            {
                GameDebugLogger.Log($"[Actor] {gameObject.name} state: {_prevState} -> {_state} (visible={_state == ActorState.GoingToTarget || _state == ActorState.WorkingOutside || _state == ActorState.Returning || _state == ActorState.ReturningToBlocked})");
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
                case ActorState.ReturningToBlocked:
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
            if (_cachedRenderers == null)
                _cachedRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);

            bool visible = _state == ActorState.GoingToTarget || _state == ActorState.WorkingOutside || _state == ActorState.Returning || _state == ActorState.ReturningToBlocked;
            foreach (var r in _cachedRenderers)
            {
                if (r != null && r.enabled != visible)
                    r.enabled = visible;
            }
        }

        private void UpdateIdle()
        {
            _idleCheckCooldown -= Time.deltaTime;
            if (_idleCheckCooldown > 0f) return;

            _idleCheckCooldown = Definition.IdleCheckCooldownSeconds;
            var (target, hadCandidates) = TryGetReachableTarget();
            if (target.HasValue)
            {
                _targetWorld = target.Value;
                _path = BuildPathTo(HomeBuilding.position, target.Value, fromIsBuilding: true, toIsBuilding: false);
                if (_path != null && _path.Count > 0)
                {
                    _pathIndex = 0;
                    _state = ActorState.GoingToTarget;
                }
                else
                {
                    GameDebugLogger.Log($"[Actor] {gameObject.name} Idle: found target but path is null/empty -> Blocked");
                    _state = ActorState.Blocked;
                    _blockedTimer = Definition.BlockedRetryDelaySeconds;
                }
            }
            else if (hadCandidates)
            {
                GameDebugLogger.Log($"[Actor] {gameObject.name} Idle: trees in range but no valid path -> Blocked");
                _state = ActorState.Blocked;
                _blockedTimer = Definition.BlockedRetryDelaySeconds;
            }
        }

        private void UpdateBlocked()
        {
            _blockedTimer -= Time.deltaTime;
            if (_blockedTimer <= 0f)
            {
                GameDebugLogger.Log($"[Actor] {gameObject.name} Blocked: retry -> Idle");
                _state = ActorState.Idle;
            }
        }

        private void UpdateGoingToTarget()
        {
            if (_path == null || _pathIndex >= _path.Count)
            {
                if (SkipWorkOutside)
                {
                    OnArrivedAtTarget();
                    _path = BuildPathTo(transform.position, HomeBuilding.position, fromIsBuilding: false, toIsBuilding: true);
                    _pathIndex = 0;
                    _state = ActorState.Returning;
                }
                else
                {
                    _state = ActorState.WorkingOutside;
                    _workTimer = Definition.WorkDurationOutside;
                }
                return;
            }

            var node = _path[_pathIndex];
            if (Definition.PathingMode == ActorPathingMode.Road)
            {
                var graph = GetPathGraph();
                if (!graph.IsWalkable(node))
                {
                    TryRecoverFromInvalidPath(wasGoingToTarget: true);
                    return;
                }
            }
            if (UpdateMovementAlongPath())
                _pathIndex++;
        }

        private void UpdateWorkingOutside()
        {
            _workTimer -= Time.deltaTime;
            if (_workTimer <= 0f)
            {
                _path = BuildPathTo(transform.position, HomeBuilding.position, fromIsBuilding: false, toIsBuilding: true);
                _pathIndex = 0;
                _state = ActorState.Returning;
            }
        }

        private void UpdateReturning()
        {
            if (_path == null || _pathIndex >= _path.Count)
            {
                if (_state == ActorState.ReturningToBlocked)
                {
                    _state = ActorState.Blocked;
                    _blockedTimer = Definition.BlockedRetryDelaySeconds;
                    GameDebugLogger.Log($"[Actor] {gameObject.name} arrived home (no path to target) -> Blocked");
                }
                else
                {
                    _state = ActorState.WorkingInside;
                    _workTimer = Definition.WorkDurationInside;
                }
                return;
            }

            var node = _path[_pathIndex];
            if (Definition.PathingMode == ActorPathingMode.Road)
            {
                var graph = GetPathGraph();
                if (!graph.IsWalkable(node))
                {
                    TryRecoverFromInvalidPath(wasGoingToTarget: _state == ActorState.GoingToTarget || _state == ActorState.ReturningToBlocked);
                    return;
                }
            }
            if (UpdateMovementAlongPath())
                _pathIndex++;
        }

        /// <summary>Moves toward current path node. Returns true if reached (advance path index).</summary>
        private bool UpdateMovementAlongPath()
        {
            if (_path == null || _pathIndex >= _path.Count) return false;
            var node = _path[_pathIndex];
            int topY = PlacementUtility.GetTopSolidY(worldBootstrap.Grid, node.X, node.Z, worldBootstrap.Grid.Height);
            if (topY < 0) return true;

            int surfaceY = topY + 1;
            var targetPos = WorldScale.BlockToWorld(node.X + 0.5f, surfaceY, node.Z + 0.5f);
            float blockScale = WorldScale.BlockScale;
            float moveDist = Definition.MoveSpeed * blockScale * Time.deltaTime;

            transform.position = Vector3.MoveTowards(transform.position, targetPos, moveDist);

            return Vector3.Distance(transform.position, targetPos) < 0.1f * blockScale;
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
        /// When true, skip WorkingOutside and go straight to Returning when arriving at target.
        /// Override in subclasses that pick up instantly (e.g. CarrierActorBehavior).
        /// </summary>
        protected virtual bool SkipWorkOutside => false;

        /// <summary>
        /// Called when arriving at target if SkipWorkOutside is true. Override to pick up items, etc.
        /// </summary>
        protected virtual void OnArrivedAtTarget() { }

        /// <summary>
        /// Try to get a reachable target. Returns (target, hadCandidates).
        /// If target is non-null, a valid path exists. If null and hadCandidates, no path to any candidate (Blocked).
        /// If null and !hadCandidates, no candidates (stay Idle).
        /// </summary>
        protected abstract (Vector3? Target, bool HadCandidates) TryGetReachableTarget();

        /// <summary>
        /// Builds path from-to using the actor's pathing mode (Road/Free/Smart). No fallback.
        /// </summary>
        protected IReadOnlyList<GridNode> BuildPathTo(Vector3 fromWorld, Vector3 toWorld, bool fromIsBuilding = false, bool toIsBuilding = false)
        {
            return BuildPathToInternal(fromWorld, toWorld, fromIsBuilding, toIsBuilding, GetPathGraph());
        }

        /// <summary>
        /// Build path home with fallback. ONLY used when actor was on road and path became invalid mid-path.
        /// Tries Road first, then Smart (road-preferring land) for Road mode.
        /// </summary>
        private IReadOnlyList<GridNode> BuildPathToHomeWithFallback()
        {
            var path = BuildPathToInternal(transform.position, HomeBuilding.position, fromIsBuilding: false, toIsBuilding: true, GetPathGraph());
            if (path != null && path.Count > 0) return path;
            if (Definition.PathingMode == ActorPathingMode.Road)
            {
                path = BuildPathToInternal(transform.position, HomeBuilding.position, fromIsBuilding: false, toIsBuilding: true, GetFallbackPathGraph());
                if (path != null && path.Count > 0)
                    GameDebugLogger.Log($"[Actor] {gameObject.name} path invalid on road, using Smart fallback for return home");
            }
            return path;
        }

        private IReadOnlyList<GridNode> BuildPathToInternal(Vector3 fromWorld, Vector3 toWorld, bool fromIsBuilding, bool toIsBuilding, IGridGraph<GridNode> graph)
        {
            if (graph == null) return null;

            var (fx, _, fz) = WorldScale.WorldToBlock(fromWorld);
            var (tx, _, tz) = WorldScale.WorldToBlock(toWorld);

            var start = fromIsBuilding ? FindOptimalWalkableAdjacentCardinal(fx, fz, graph, tx, tz) : FindWalkableAdjacent(fx, fz, graph);
            var goal = toIsBuilding ? FindOptimalWalkableAdjacentCardinal(tx, tz, graph, fx, fz) : FindWalkableAdjacent(tx, tz, graph);

            if (!start.HasValue)
            {
                GameDebugLogger.Log($"[Actor] {gameObject.name} BuildPath: no walkable block adjacent to start ({fx},{fz})");
                return null;
            }
            if (!goal.HasValue)
            {
                GameDebugLogger.Log($"[Actor] {gameObject.name} BuildPath: no walkable block adjacent to goal ({tx},{tz})");
                return null;
            }

            return AStarPathfinder.FindPath(graph, start.Value, goal.Value);
        }

        /// <summary>Returns the first walkable block at or adjacent to (bx,bz). Used for targets (tree, building).</summary>
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

        /// <summary>Returns the walkable block in a cardinal direction from (bx,bz) that is closest to (targetBx, targetBz). Used for buildings.</summary>
        private GridNode? FindOptimalWalkableAdjacentCardinal(int bx, int bz, IGridGraph<GridNode> graph, int targetBx, int targetBz)
        {
            GridNode? best = null;
            float bestDistSq = float.MaxValue;

            foreach (var (dx, dz) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var n = new GridNode(bx + dx, bz + dz);
                if (!graph.IsWalkable(n)) continue;

                int dx2 = (bx + dx) - targetBx;
                int dz2 = (bz + dz) - targetBz;
                float distSq = dx2 * dx2 + dz2 * dz2;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    best = n;
                }
            }
            return best;
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

        /// <summary>Fallback graph for Road mode when roads become unavailable. Uses Smart (road-preferring land).</summary>
        private IGridGraph<GridNode> GetFallbackPathGraph()
        {
            var grid = worldBootstrap.Grid;
            var roadOverlay = worldBootstrap.GetRoadOverlay();
            int waterLevelY = worldBootstrap.WaterConfig != null
                ? worldBootstrap.WaterConfig.GetWaterLevelY(grid.Height)
                : 0;
            bool isBlockValid(int x, int y, int z) =>
                !worldBootstrap.HasBlockingObjectAtBlock(x, y, z);
            return new SmartSurfacePathGraph(grid, waterLevelY, roadOverlay, isBlockValid);
        }

        /// <summary>Path node became unwalkable while actor on road. Try path home with fallback; if exists, return home (Blocked on arrival if was GoingToTarget). Else go Blocked.</summary>
        private void TryRecoverFromInvalidPath(bool wasGoingToTarget)
        {
            var pathHome = BuildPathToHomeWithFallback();
            if (pathHome != null && pathHome.Count > 0)
            {
                _path = pathHome;
                _pathIndex = 0;
                _state = wasGoingToTarget ? ActorState.ReturningToBlocked : ActorState.Returning;
                GameDebugLogger.Log($"[Actor] {gameObject.name} path invalid, recalculated path home (wasGoingToTarget={wasGoingToTarget})");
            }
            else
            {
                _path = null;
                _state = ActorState.Blocked;
                _blockedTimer = Definition.BlockedRetryDelaySeconds;
                GameDebugLogger.Log($"[Actor] {gameObject.name} path invalid, no path home -> Blocked");
            }
        }
    }
}
