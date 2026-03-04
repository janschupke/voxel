# Project Audit Report

**Date:** 2026-03-04  
**Project:** Voxel (3D top-down voxel game)  
**Audit Criteria:** `.cursor/rules/voxel-architecture.mdc`, `voxel-csharp-unity.mdc`, Unity 6.3 best practices

---

## Executive Summary

The Voxel project has a solid foundation with clear separation in some areas (e.g., `Voxel.Core` assembly with no engine references, domain types like `VoxelGrid`). However, several refactoring and performance opportunities were identified that align with the project's architecture rules.

**Priority areas:**
1. **Performance (High):** Per-frame allocations and repeated `GetComponent`/`FindAnyObjectByType` in hot paths—directly violates `voxel-csharp-unity.mdc`.
2. **Architecture (High):** Project structure diverges from `Core/` and `Features/` layout; domain logic (inventory) is coupled to `MonoBehaviour`; no interfaces for registries.
3. **Refactoring (Medium):** Oversized controllers (`WorldBootstrap` 351 lines, `SelectionController` 415 lines, `ObjectPlacementController` 484 lines); verbose debug logging to standard out (Unity console) in production paths.
4. **Assembly Definitions (Low):** Only `Voxel.Core` has an asmdef; Editor and runtime code are not isolated.

---

## Refactoring Opportunities

| Location | Issue | Impact | Recommendation |
|----------|-------|--------|----------------|
| `Assets/Scripts/BuildingInventory.cs` | Domain logic (inventory rules) in `MonoBehaviour` | Couples business logic to Unity; violates architecture rule | Extract `InventoryService` (pure C#) in `Core/` or `Features/Inventory/`; keep `BuildingInventory` as thin MonoBehaviour that holds/wires `InventoryService` |
| `Assets/Scripts/Data/ItemRegistry.cs` | ScriptableObject used directly by domain code | Domain depends on Unity types | Define `IItemRegistry` interface; domain uses interface; ScriptableObject implements it |
| `Assets/Scripts/Data/PlacedObjectRegistry.cs` | Same as ItemRegistry | Same coupling | Define `IPlacedObjectRegistry`; domain uses interface |
| `Assets/Scripts/WorldBootstrap.cs` (351 lines) | Multiple responsibilities: world creation, persistence, camera, placement parents, road overlay, blocking checks | Hard to test, maintain, extend | Split: `WorldCreationService`, `PlacedObjectManager`, keep `WorldBootstrap` as thin coordinator |
| `Assets/Scripts/SelectionController.cs` (415 lines) | Selection, outline rendering, inventory UI, raycasting, bounds logic in one class | Single responsibility violation | Extract `SelectionOutlineRenderer`, `SelectionRaycaster`; keep controller for input/coordination |
| `Assets/Scripts/ObjectPlacementController.cs` (484 lines) | Placement, preview, area/line logic, validation in one class | Same as above | Extract `PlacementValidator`, `PlacementPreviewController`; keep controller for input |
| Project structure | `Assets/Scripts/` flat/mixed vs `Assets/Core/` and `Assets/Features/` | Inconsistent with architecture rule | Migrate: `Scripts/Core/*` → `Core/`, `Scripts/Actors` → `Features/Actors`, `Scripts/Placement` → `Features/Placement`, etc. |
| `ActorBehavior`, `WoodchuckActorBehavior`, `ActorSpawner`, `TreeScatterStage` | Verbose `Debug.Log` in hot paths and normal flow | Performance overhead, log spam to standard out in builds | Refactor into a toggleable logging system; route debug output through it instead of `Debug.Log`. Do not output debug to standard out. Keep UI debugging (MessageLog, DebugLogService) as is. |
| `DebugLogService.LogMessage()` | Static method calls `FindAnyObjectByType` on every log | Expensive per-call; can be called frequently | Inject `DebugLogService` where needed; avoid static lookup in hot paths |

---

## Performance Opportunities

| Location | Issue | Impact | Recommendation |
|----------|-------|--------|----------------|
| `Assets/Features/UI/HUDController.cs` L133–134 | `FindAnyObjectByType<ObjectPlacementController>()` and `FindAnyObjectByType<SelectionController>()` in `Update` when escape pressed | Expensive scene scan on every escape key press | Cache references in `Start` or via `[SerializeField]`; avoid repeated Find calls |
| `Assets/Scripts/SelectionController.cs` L100, L368 | `GetComponent<BuildingInventory>()` in `UpdateHighlights` and `RefreshInventoryDisplay` | Repeated component lookup when building selected | Cache `BuildingInventory` when selection changes; store in `_cachedInventory` alongside `_selectedObject` |
| `Assets/Scripts/Actors/WoodchuckActorBehavior.cs` L60, L67 | `GetComponent<BuildingInventory>()` in `OnWorkCompletedInside` and `IsBuildingInventoryFull` | Called every work cycle per actor | Cache `BuildingInventory` in `Initialize` or first access; `HomeBuilding` is stable |
| `Assets/Scripts/Actors/ActorBehavior.cs` L84 | `GetComponentsInChildren<Renderer>(includeInactive: true)` in `UpdateVisibility` | Allocates array every frame per actor | Cache renderers in `Awake` or when visibility state first changes; invalidate cache if hierarchy changes |
| `Assets/Scripts/SelectionRangeOverlay.cs` L64, L92 | `new List<Vector3>()` and `positions.ToArray()` in `ShowOverlay` (called from `LateUpdate`) | Per-frame allocations when overlay visible | Reuse a `List<Vector3>` and `Vector3[]` buffer; resize only when needed |
| `Assets/Scripts/SelectionController.cs` L232 | `Physics.RaycastAll(ray)` | Allocates `RaycastHit[]` every call | Use `Physics.RaycastAllNonAlloc` with a cached `RaycastHit[]` buffer |
| `Assets/Scripts/SelectionController.cs` L123, L159, L295 | `GetComponentsInChildren<MeshFilter>()`, `GetComponentsInChildren<MeshRenderer>()`, `GetComponentsInChildren<Renderer>()` | Allocations on selection/hover changes | Acceptable for infrequent events; consider caching if selection changes very often |
| `Assets/Scripts/Rendering/ChunkManager.cs` L63–70 | `config.Bands.Select(...).ToArray()` in `ResolveMaterials` | LINQ in initialization path | Called once at startup; low priority; replace with loop if desired for consistency |
| `Assets/Scripts/VoxelGridRenderer.cs` L36 | `GetComponent<TerrainMaterialDebugger>()` in `Initialize` | One-time; acceptable | No change needed |

---

## Prioritized Action Plan

### High Priority

1. **Cache `FindAnyObjectByType` and `GetComponent` in hot paths**
   - HUDController: Cache `ObjectPlacementController` and `SelectionController` in `Start`.
   - SelectionController: Cache `BuildingInventory` when selection changes.
   - WoodchuckActorBehavior: Cache `BuildingInventory` from `HomeBuilding` in `Initialize` or on first use.

2. **Eliminate per-frame allocations**
   - SelectionRangeOverlay: Reuse `List<Vector3>` and `Vector3[]` for overlay positions.
   - ActorBehavior: Cache `Renderer[]` from `GetComponentsInChildren<Renderer>`; refresh only when hierarchy changes.
   - SelectionController: Use `Physics.RaycastAllNonAlloc` with cached buffer.

3. **Extract domain logic from BuildingInventory**
   - Create `InventoryService` (pure C#) with `AddItem`, `GetCount`, `HasSpaceFor`, etc.
   - `BuildingInventory` holds `InventoryService` and exposes it; other systems use service via interface.

### Medium Priority

4. **Define registry interfaces**
   - `IItemRegistry` with `GetDefinition(Item)`.
   - `IPlacedObjectRegistry` with `Entries`, `GetByName(string)`.
   - Domain code depends on interfaces; ScriptableObjects implement them.

5. **Refactor debug logging into a toggleable system**
   - Introduce a toggleable debug logger that routes to DebugLogService/MessageLog (UI) when enabled; no output to standard out.
   - Replace `Debug.Log` calls in ActorBehavior, WoodchuckActorBehavior, ActorSpawner, TreeScatterStage with the new logger.
   - Keep existing UI debugging (MessageLog, DebugLogService) as is.

6. **Split WorldBootstrap**
   - Extract `WorldCreationService` (CreateNewWorld, terrain pipeline).
   - Extract `PlacedObjectManager` (parents, CollectPlacedObjectsForSave, LoadPlacedObjects).
   - Keep `WorldBootstrap` as coordinator with `[SerializeField]` references.

### Low Priority

7. **Align project structure with architecture**
   - Move `Scripts/Core/*` → `Assets/Core/`.
   - Move `Scripts/Actors`, `Scripts/Placement`, etc. → `Assets/Features/`.
   - Update asmdef and meta references incrementally.

8. **Add Assembly Definitions**
   - Create `Voxel.Editor` for Editor-only code (e.g., TerrainMaterialDebugger).
   - Consider `Voxel.Features.UI` if UI grows.

9. **Split SelectionController and ObjectPlacementController**
   - Extract outline rendering and raycasting into separate components.
   - Extract placement validation and preview logic.

---

## Appendix: File and Line Counts

| File | Lines | Notes |
|------|-------|-------|
| WorldBootstrap.cs | 351 | Oversized; multiple responsibilities |
| SelectionController.cs | 415 | Oversized; mixed concerns |
| ObjectPlacementController.cs | 484 | Oversized; placement + preview + validation |
| ActorBehavior.cs | 299 | Moderate; state machine + pathing |
| ChunkManager.cs | 188 | Acceptable |
| BuildingInventory.cs | 59 | Small; but domain logic in MonoBehaviour |

---

## References

- `.cursor/rules/voxel-architecture.mdc` — Core/Features structure, domain/Unity separation, DI, interfaces
- `.cursor/rules/voxel-csharp-unity.mdc` — No LINQ in Update, cache GetComponent, reuse collections, FindAnyObjectByType usage
- Unity 6.3 Manual — Physics.RaycastAllNonAlloc, best practices for hot paths
