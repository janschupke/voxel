# Project Audit Report

**Date:** 2026-03-04  
**Project:** Voxel (3D top-down voxel game)  
**Audit Criteria:** `.cursor/rules/voxel-architecture.mdc`, `voxel-csharp-unity.mdc`, Unity 6.3 best practices

---

## Executive Summary

The Voxel project has a solid foundation with clear Pure/Core/Features structure, domain types in `Pure/`, and interfaces (`IItemRegistry`, `IPlacedObjectRegistry`) defined for registries. However, several refactoring and performance opportunities were identified that align with the project's architecture rules.

**Priority areas:**
1. **Performance (High):** Per-frame allocations in `RemovalPreview.SetBlocks`, repeated `GetPathGraph()` per node in `ActorBehavior.IsPathNodeWalkable`, uncached `Camera.main` in Update loops, repeated `GetComponent<BuildingInventory>()` in hot paths.
2. **Architecture (Medium):** `IItemRegistry` and `IPlacedObjectRegistry` exist but are underused; no `IBuildingInventory`; domain logic coupled to Unity in actor behaviors.
3. **Refactoring (Medium):** Oversized classes (`ActorBehavior` 460 lines, `ChunkMeshBuilder` 440 lines), duplicated logic (GetBlockUnderMouse, debug visibility, Shuffle), mixed concerns in controllers.
4. **Service Locator (Low):** Widespread `FindAnyObjectByType` in Awake/Start—prefer `[SerializeField]` or DI where possible.

---

## Refactoring Opportunities

| Location | Issue | Impact | Recommendation |
|----------|-------|--------|----------------|
| `Assets/Features/Actors/Scripts/ActorBehavior.cs` (460 lines) | State machine, pathfinding, movement, visibility in one class | Hard to test, maintain, extend | Split: extract `ActorPathfindingService`, `ActorVisibilityController`; keep `ActorBehavior` as coordinator |
| `Assets/Core/Rendering/ChunkMeshBuilder.cs` (440 lines) | Static mesh builder with many responsibilities | Single responsibility violation | Extract water/road mesh building into separate helpers; keep main orchestration |
| `Assets/Features/World/PlacedObjectManager.cs` (311 lines) | Placement, removal, persistence, road overlay | Multiple responsibilities | Extract `PlacedObjectPersistence`; keep manager for placement/removal coordination |
| `Assets/Features/Placement/ObjectPlacementController.cs` (289 lines) | Placement mode, preview, input, validation | Mixed concerns | Extract shared `GetBlockUnderMouse`; consider `PlacementInputHandler` |
| `Assets/Features/Placement/RemovalController.cs` | Duplicated `GetBlockUnderMouse`, `UIPanelUtils.IsPointerOverBlockingUI`, right-click cancel | Code duplication | Extract shared `PlacementInputUtils` or base class with `GetBlockUnderMouse`, pointer-over-UI check |
| `Assets/Features/Selection/SelectionController.cs` L265–276 | `root.Query(className: "debug-only").ToList()` | Duplicated pattern | Extract `UpdateDebugControlsVisibility(root, visible)` shared with HUDController |
| `Assets/Features/UI/HUDController.cs` L132–142 | Same debug visibility pattern | Same as above | Use shared utility |
| `CarrierActorBehavior`, `WoodchuckActorBehavior` | Duplicated `Shuffle<T>` implementation | Code duplication | Extract to `Voxel.Utils.Shuffle` or `ListExtensions.Shuffle` |
| `CarrierActorBehavior`, `WoodchuckActorBehavior` | Both use `GetHomeInventory()` with `GetComponent<BuildingInventory>()` | Coupling to Unity in domain logic | Introduce `IBuildingInventory`; inject or resolve via interface |
| `ActorBehavior` L182–201, L234–253 | Duplicated movement logic in `UpdateGoingToTarget` and `UpdateReturning` | DRY violation | Extract `UpdateMovementAlongPath(path, speed)` shared method |
| `FloatingTextService.cs` L23 | `worldBootstrap.ItemRegistry as ItemRegistry` | Unnecessary cast | Use `IItemRegistry` if only interface methods needed |

---

## Performance Opportunities

| Location | Issue | Impact | Recommendation |
|----------|-------|--------|----------------|
| `Assets/Features/Actors/Scripts/ActorBehavior.cs` L434–436 | `IsPathNodeWalkable(node)` calls `GetPathGraph()` every invocation | `GetPathGraph()` builds graph; called per node per frame when actor on road | Cache graph for current frame or pass graph into movement logic; call `GetPathGraph()` once per Update |
| `Assets/Features/Actors/Scripts/ActorBehavior.cs` L108 | `GetComponentsInChildren<Renderer>(includeInactive: true)` in `UpdateVisibility` | Allocates array every frame when `_cachedRenderers == null` | Cache in Awake; invalidate only when hierarchy changes (e.g. after `RestoreState`) |
| `Assets/Features/Placement/RemovalPreview.cs` L30–31 | `new List<Transform>(32)`, `new HashSet<Transform>()` in `SetBlocks()` | Allocates every `UpdatePreview()` call during drag | Use instance fields `_transformsBuffer`, `_outlinedSet`; Clear() and reuse |
| `Assets/Features/Placement/RemovalController.cs` L136, L147 | `new List<(int x, int y, int z)>()`, `new[] { ... }` in `UpdatePreview()` | Per-frame allocations when preview active | Reuse instance buffer; resize only when needed |
| `Assets/Features/Selection/SelectionController.cs` L145 | `Camera.main` every Update | `Camera.main` does FindGameObjectWithTag internally | Cache in Start/Awake; refresh if camera changes |
| `Assets/Features/Placement/ObjectPlacementController.cs` L157, L174 | `Camera.main` in `GetBlockUnderMouse` and `UpdatePreview` | Same as above | Cache camera reference |
| `Assets/Features/Placement/RemovalController.cs` L109, L146 | `Camera.main` in `GetBlockUnderMouse` and `UpdatePreview` | Same as above | Cache camera reference |
| `Assets/Features/UI/FloatingText/FloatingTextInstance.cs` L35, L68 | `Camera.main` in Update | Per-frame lookup | Cache in Start; refresh if camera changes |
| `Assets/Features/Actors/Scripts/CarrierActorBehavior.cs` L45, L86 | `_sourceBuilding.GetComponent<BuildingInventory>()`, `building.GetComponent<BuildingInventory>()` in hot path | Repeated component lookup | Cache when source/target changes; `_cachedSourceInventory`, `_cachedTargetInventory` |
| `Assets/Features/Selection/SelectionRangeOverlay.cs` L55–98 | `ShowOverlay()` in LateUpdate: `OperationalRange.GetOutlineVertices`, loop, `PlacementUtility.GetTopSolidY` per step | Heavy work per frame when overlay visible | Consider throttling (e.g. every N frames) or caching outline when selection unchanged |
| `Assets/Features/Selection/SelectionController.cs` L270 | `root.Query(className: "debug-only").ToList()` | LINQ allocates; called on debug toggle | Low frequency; acceptable; consider caching element list if toggled often |
| `Assets/Features/UI/HUDController.cs` L137 | Same LINQ pattern | Same as above | Same |
| `Assets/Core/Rendering/ChunkManager.cs` L63–70 | `config.Bands.Select(...).ToArray()` | LINQ in Start only | Low priority; replace with loop for consistency |

---

## Architecture Alignment

### Pure / Core / Features Layout

**Good:**
- `Pure/Grid/`, `Pure/Terrain/`, `Pure/Pathfinding/`, `Pure/Noise/`, `Pure/Data/` — no Unity references
- `Pure/RoadOverlay.cs`, `Pure/Grid/OperationalRange.cs` — pure C#
- `Core/Rendering/`, `Core/Registries/`, `Core/Shared/` — appropriate for shared Unity infrastructure
- Feature folders (Actors, Placement, Selection, UI, World) follow co-location

### Domain vs Unity Separation

**Good:**
- `InventoryService` — pure domain, no Unity
- `BuildingInventory` — MonoBehaviour wrapping `InventoryService`
- `SelectionRaycaster` — uses `IPlacedObjectRegistry`
- `BlockQueries.GetTopSolidY`, `PlacementUtility` — pure/utility usage

**Issues:**
- `ActorBehavior` and subclasses: pathfinding and state logic mixed with `WorldBootstrap`, `Transform`, `GetComponent`
- No `IBuildingInventory` — `BuildingInventory` used directly in many places
- `PlacedObjectManager`, `ActorSpawner`, `CarrierActorBehavior`, `HUDController`, `SelectionController`, `SelectionRangeOverlay`, `ObjectPlacementController` use `PlacedObjectRegistry` directly instead of `IPlacedObjectRegistry`
- `SelectionController`, `HUDController`, `FloatingTextService` use `ItemRegistry` directly instead of `IItemRegistry`

### Service Locator / FindAnyObjectByType

| Location | Usage | Recommendation |
|----------|-------|----------------|
| `RemovalController` L30–33 | Awake: WorldBootstrap, UIDocument, ObjectPlacementController, SelectionController | Use `[SerializeField]` or inject via bootstrap |
| `SelectionController` L43–47 | Awake: WorldBootstrap, ObjectPlacementController, RemovalController | Same |
| `HUDController` L38–41, L110 | Awake: WorldBootstrap, controllers, DebugLogService | Same |
| `ObjectPlacementController` L37, L41 | Awake: WorldBootstrap, UIDocument | Same |
| `SelectionRangeOverlay` L23–24 | Awake: WorldBootstrap, SelectionController | Same |
| `FloatingTextService` L21 | Awake: WorldBootstrap | Same |
| `ActorSpawner` L28 | Awake: WorldBootstrap | Same |

---

## Prioritized Action Plan

### High Priority

1. **Cache `GetPathGraph()` in ActorBehavior**
   - In `Update`, call `GetPathGraph()` once; pass graph to `IsPathNodeWalkable` or inline check.
   - Avoid building graph per node when validating path during movement.

2. **Eliminate per-frame allocations in RemovalPreview**
   - Add `_transformsBuffer` and `_outlinedSet` as instance fields; Clear and reuse in `SetBlocks()`.

3. **Cache `Camera.main` in controllers**
   - SelectionController, ObjectPlacementController, RemovalController, FloatingTextInstance: cache in Start/Awake.

4. **Cache `GetComponent<BuildingInventory>()` in CarrierActorBehavior**
   - Cache source/target inventory when building reference changes; avoid lookup in `TakeFromSource` and `TryGetReachableTarget`.

### Medium Priority

5. **Introduce `IBuildingInventory` and use registry interfaces**
   - Define `IBuildingInventory` with `AddItem`, `GetCount`, `HasSpaceFor`, etc.
   - Use `IItemRegistry` / `IPlacedObjectRegistry` where only lookups are needed (SelectionController, FloatingTextService, CarrierActorBehavior).

6. **Extract shared placement/removal input logic**
   - `GetBlockUnderMouse`, pointer-over-UI check, right-click cancel into `PlacementInputUtils` or base class.

7. **Extract shared `UpdateDebugControlsVisibility`**
   - SelectionController and HUDController both use `root.Query(className: "debug-only").ToList()`; extract to shared utility.

8. **Extract shared `Shuffle<T>`**
   - CarrierActorBehavior and WoodchuckActorBehavior duplicate Shuffle; move to common utility.

9. **Replace FindAnyObjectByType with serialized references**
   - Prefer `[SerializeField]` for WorldBootstrap, controllers; reduce scene scans at startup.

### Low Priority

10. **Split ActorBehavior**
    - Extract pathfinding, state machine, visibility into separate components or services.

11. **Reduce duplication in ActorBehavior movement**
    - Extract `UpdateMovementAlongPath` shared by `UpdateGoingToTarget` and `UpdateReturning`.

12. **Split ChunkMeshBuilder**
    - Extract water/road mesh building into helper methods or classes.

---

## Appendix: File and Line Counts

| File | Lines | Notes |
|------|-------|-------|
| ActorBehavior.cs | 460 | Oversized; state machine + pathing + visibility |
| ChunkMeshBuilder.cs | 440 | Oversized; many mesh-building responsibilities |
| PlacedObjectManager.cs | 311 | Multiple responsibilities |
| TopDownCamera.cs | 298 | Camera + input |
| ObjectPlacementController.cs | 289 | Placement + preview + input |
| SelectionController.cs | 280 | Selection + UI + inventory |
| WorldPersistenceService.cs | 278 | Save/load |
| WorldBootstrap.cs | 261 | Bootstrap coordinator |
| HUDController.cs | 252 | HUD + debug controls |
| PlacementExecutor.cs | 224 | Placement execution |
| PlacementPreview.cs | 221 | Preview rendering |

---

## References

- `.cursor/rules/voxel-architecture.mdc` — Core/Features structure, domain/Unity separation, DI, interfaces
- `.cursor/rules/voxel-csharp-unity.mdc` — No LINQ in Update, cache GetComponent, reuse collections
- Unity 6.3 Manual — Physics.RaycastAllNonAlloc, Camera.main, hot path best practices
