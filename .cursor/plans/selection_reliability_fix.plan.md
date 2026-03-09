---
name: ""
overview: ""
todos: []
isProject: false
---

# Selection Reliability Fix – Plan

## Root Cause

Selection fails when the cursor is clearly over an object because:

1. **Ray hits terrain first** – Buildings sit ON the ground. The terrain has a collider. When the cursor is over the ground tiles that are part of a building’s footprint (e.g. the 2×3 area of a Warehouse), the ray hits the terrain before or instead of the building mesh.
2. **MeshCollider gaps** – The mesh may not cover the full footprint (courtyards, overhangs, L‑shapes). The ray can pass through gaps and hit the terrain.
3. **Bounds fallback is view‑dependent** – The bounds check only helps when the ray passes through the object’s AABB. If the terrain hit is closer and the building is behind it, the ray may not intersect the building’s bounds before the “logical” hit point.

## Why Block Lookup Is Correct (Not a Hack)

- Buildings are registered at **all blocks in their footprint** via `PlacedObjectManager.RegisterPlacedObject`.
- The footprint is the source of truth for “this block belongs to this building.”
- When the ray hits the terrain at block `(bx, by, bz)`, that block may be part of a building’s footprint.
- Looking up `GetTransformsAtBlock(bx, by, bz)` returns the building that occupies that block.
- This matches the game model: hovering over any block in the footprint should select the building.

## Proposed Fix

### 1. Add block-based lookup to `SelectionRaycaster.TryGetSelectableAtRay`

When the physics raycast hits something that is **not** a selectable object (e.g. terrain):

- Convert the hit point to block coordinates.
- Call `WorldBootstrap.GetTransformsAtBlock(bx, by, bz)` (and optionally `by+1` for surface boundary).
- For each returned transform, check if it is selectable.
- If so, treat it as a hit at the same distance as the terrain hit (so ordering stays correct).

This covers the case: “cursor over footprint ground → ray hits terrain → block lookup finds building.”

### 2. Keep physics and bounds as primary

- Physics raycast: when the ray hits the building’s MeshCollider, use that (closest, most accurate).
- Bounds fallback: when the ray passes through the building’s bounds but physics misses (e.g. MeshCollider edge cases).
- Block lookup: when the ray hits terrain (or other non-selectable) at a block that belongs to a building.

### 3. Hysteresis (optional)

- Keep the current hysteresis in `SelectionController` to reduce flicker when physics is inconsistent.
- Or remove it if the block lookup makes results stable enough.

## Implementation Steps

1. In `SelectionRaycaster`, add a `List<Transform> _transformsBuffer` for `GetTransformsAtBlock`.
2. In the physics raycast loop, when a hit is **not** a selectable object, call a helper `TrySelectFromBlockAtHit(hit, ref closestDistance, ref hitTransform, ref entryName)`.
3. In that helper: `WorldToBlock(hit.point)` → `GetTransformsAtBlock` → filter by selectable → update result if closer.
4. Handle surface Y: check both `by` and `by+1` (terrain vs building surface).
5. Ensure `SelectionRaycaster` has access to `WorldBootstrap` (for `GetTransformsAtBlock`, `WorldParameters`).

## Files to Modify

- `Assets/Features/Selection/Scripts/SelectionRaycaster.cs` – add block-based lookup when ray hits non-selectable.

