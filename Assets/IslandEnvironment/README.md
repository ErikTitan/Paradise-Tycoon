# Island environment — SCRUM-101

Open `Assets/IslandEnvironment/Scenes/EnvironmentReview.unity` for the island alongside the six existing level-one buildings. The source scene `Assets/Scenes/Main.unity` is preserved. This review scene is not added to build settings.

## Handoff

Drag `Prefabs/IslandEnvironment.prefab` into `World/Environment` at local position (0,0,0), rotation (0,0,0), scale (1,1,1). Replace the old IslandEnvironment layout and remove the two old CoastalPalms examples to avoid duplicates. Keep Buildings, camera, HUD and pause menu owned by their existing systems.

The prefab contains ocean, expanded terrain with its continuous beach/rocks/shallow water/foam, hand-placed walking routes, a dock approach, vegetation, coastal rocks, beach furniture and lamps. Coastline features are already drawn into the terrain artwork, so extra foam strips would duplicate them. No cameras, UI, audio sources or scene references are included. Existing sprite imports and GUIDs are preserved.

Terrain is 28 world units wide; source art stays at 100 PPU. Overlapping props use a ground-point SortingGroup on WorldObjects, order 0, following shared Y sorting. Sea uses an opaque unlit mesh in the Background layer, render queue 2000, at Z=5 behind the XY terrain at Z=0. This keeps the textured ocean behind the 2D sprite passes. Paths use Terrain order 10, dock order 12. PathsVisual uses the custom transparent Generated/IslandPaths.png sprite at 100 PPU. The complete network is rasterized as one continuous sand surface with rounded bends and junctions. Individual kerbstones follow only its outside contour, so no end caps or kerbs run across junctions. HandPlacedPaths retains non-rendering Start/End route markers. Short access paths continue underneath the building thresholds to cover transparent sprite padding.

## Entrance anchors (local XY)

| Building | Entrance |
| --- | --- |
| Bungalow | (-7.5, 2.5) |
| Hotel | (-1.4, 2.6) |
| Spa | (4, 3) |
| Restaurant | (-6.5, -2.3) |
| Pool | (-1.6, -1) |
| Beach bar | (5.8, -2.5) |
| Harbor | (8.2, 1.2) |

These match the ground positions of the current Main scene layout. BuildingEntrances contains empty markers, allowing the building owner to align later prefabs. Paths join these anchors; no vegetation/prop ground point is within 0.75 units of an entrance. Keep the front beach and right bay accessible.

## Verification and editing

Tools > Paradise Tycoon > Island > Validate environment checks sprites, materials, root scale, missing scripts, absence of UI/cameras, entrance positions, decoration clearance, path connectivity and access to every entrance. Build environment and review scene rebuilds only this module and its review scene from Main; save any manual changes separately before rebuilding. The source prefab is editable without running the builder.

`IslandEnvironmentBuilder.ReviewFromDisk` reopens and validates the saved review scene, then captures camera views at 1280x720, 1600x720 and 2560x1440 under Logs/IslandReview. Captures omit Screen Space Overlay UI. Final integration and another teammate's scene review belong to SCRUM-108.


Use Tools > Paradise Tycoon > Island > Rebuild custom path sprites to update only the path sprite and prefab while preserving terrain, props, decorations and scenes. IslandPathArtwork creates deterministic artwork with a connected fill and verifies that every entrance is reached in the actual rasterized surface.
