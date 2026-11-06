# Main scene foundation — SCRUM-100

Unity **6000.6.6f1**. Open `Assets/Scenes/Main.unity` and select a landscape Game view
(1280 × 720 for 16:9, 1600 × 720 for 20:9). Main is the first enabled build scene.

## Ownership and integration

Andrej owns Main.unity, its camera, shared rendering rules and final integration.
Each teammate edits their own prefab instead of Main.unity.

| Scene location | Content / owner |
| --- | --- |
| `Systems` | Camera, single AudioListener and single Input System EventSystem / Andrej |
| `World/Environment` | Island, water, coastline, paths / Lukáš |
| `World/Buildings` | Building prefab group / Simon |
| `World/Decorations` | Optional separate environment decoration group / Lukáš |
| `World/Characters` | Reserved for later NPC work |
| `UI/MainCanvas/SafeArea/HUD_IntegrationSlot` | HUD content / Erik |
| `UI/MainCanvas/SafeArea/PauseMenu` | Connected SCRUM-104 prefab / Henrieta |

**Current layout:** the expanded `island_base_large_v2.png` terrain is installed in
`World/Environment/IslandEnvironment` at 28 world units wide. The large front beach
and the sheltered harbor bay on the right remain free for separate props and docks.
`World/Buildings/ResortBuildings_L1` contains all six building types at level 1:
hotel, restaurant, beach bar, bungalow, pool and spa. These are editable scene
objects using the existing building art and ground-point SortingGroups.
`World/Decorations/CoastalPalms` contains the two coastal palm examples.

Henrieta's merged pause menu stays connected as a prefab instance. The HUD slot is
intentionally empty. Teammates' final environment/building/HUD prefabs have not yet
been integrated. When they arrive, replace the corresponding layout group under
its container and check the result. Keep the camera and EventSystem in Systems;
do not import cameras, AudioListeners or EventSystems from demo scenes.

## Shared 2D rules

- The isometric angle is already drawn into the sprites. Use the XY plane, Z = 0;
  the orthographic camera faces forward from Z = -10 with zero rotation.
- Keep sprite imports at **100 pixels per unit**. Use a prefab root at scale (1,1,1)
  and scale the Visual child to the intended footprint. The current hotel is 4.6
  world units wide; the expanded island is 28 units wide.
- For objects that can overlap, put the root at the ground contact point and add a
  **SortingGroup** on `WorldObjects`, Order in Layer **0**. Offset the Visual child
  above that point. Lower root Y draws in front, for buildings, trees and NPCs alike.
- Both camera and `Assets/Settings/Renderer2D.asset` use **Custom Axis (0,1,0)**.
  Separate Building and Decoration sorting layers would prevent correct interleaving.
- Layer order: `Background` (sea), `Terrain` (island/paths; use Order in Layer for
  flat overlays), `WorldObjects` (all overlapping ground objects), `WorldOverlay`
  (selection indicators), `UI`. Default is preserved.
- Existing building slicing and GUIDs are preserved; the largest slice is used.
  The new island is imported as one complete sprite at 100 PPU, with alpha,
  bilinear filtering, no mipmaps and no texture compression.

`IslandCameraFraming.WorldBounds` defines the area to keep visible. Update that
Inspector field when the final island footprint changes. Current bounds are
28.6 by 16.3 world units, excluding the transparent outer padding. The camera
reserves 8% of safe height at the top, 4% at the bottom and 3.5% at either side.
It adapts to screen size and safe area without rotating the art. `OceanBackdrop`
keeps the sea behind the complete view, including wider displays.

## UI handoff

The shared overlay Canvas uses a 1280 × 720 reference and Scale With Screen Size,
match = 0.5. Put HUD content inside the SafeArea container. If a delivered prefab
already has a Canvas, keep it nested, stretch its root and disable its duplicate
CanvasScaler on the instance. PauseMenu uses this setup with sorting order 100.
Its source prefab is unchanged. A future HUD pause button can call
`PauseMenuController.OpenPauseMenu()`; the prefab's current Pause button already works.

## Verification and remaining acceptance

Use **Tools → Paradise Tycoon → Validate main scene foundation** while Main is open.
It checks references, scene containers, one camera/listener/EventSystem, framing at
16:9 and 20:9, a notched safe area, portrait fallback, rendered Y sorting, and the
build entry scene. It creates temporary test objects and removes them afterward.

Play-mode checks performed: advancing frames, opening pause, Time.timeScale = 0,
resume restoring time, music reference, UI pointer input and landscape captures.
No gameplay/economy simulation or optional camera controls are included.

SCRUM-105/106/107 have their technical implementation prepared. SCRUM-108 still
needs the delivered environment, building and HUD prefabs, followed by another
team member opening and testing the complete scene. The parent task must not be
considered fully accepted before that handoff.

The creation menu refuses to overwrite an existing Main.unity. Use it only in a
checkout without that scene; integrate future modules into the existing scene.
