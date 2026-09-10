TRD – Magnifying Glass HOG + Spatial Memory Game

**Title:** Hidden Mosaic  
**Working titles:** Little Mosaic / Mosaic Hunt  
Version: 1.3

Date: 9 September 2026

Target Engine: Unity 6 (URP 2D)

Level Generation Tool: Python (primary)

**Reference:** [Little Things Remastered](https://klicktock.itch.io/little-things-remastered) (KlickTock) — primary inspiration for the magnifying-glass silhouette mosaic HOG feel.

1. Overview & Vision
A modern, cozy hidden-object game inspired by [Little Things Remastered](https://klicktock.itch.io/little-things-remastered).
Players use a movable magnifying glass to scan a large silhouette collage made of hundreds of tiny objects. Objects are intentionally too small to identify clearly without the glass.
Core differentiator: Strong spatial memory mechanic.

Players must remember locations they previously scanned with the glass to find items faster later.
Levels are procedurally / semi-automatically generated from a single simple object image (horse, watch, dog, etc.) that is turned into a dense mosaic while preserving the overall shape and internal structure of the original object.

2. Goals
Primary Goals

Deliver a polished, relaxing, skillful HOG experience on mobile + PC.
Make level creation scalable via automation (Python tool).
Keep each tiny object as an independent, clickable entity in Unity for accurate hit detection.
Support full editor preview + manual fine-tuning inside Unity.

Secondary Goals

High replayability through randomized object placement within constraints.
Strong “memory satisfaction” loop.
Easy content pipeline so new silhouettes can be added quickly.


3. High-Level Architecture

```text
[Source Image]
      ↓
Python Level Generator  (NixinStudioUnityCore/tools/mosaic-level-gen)
      ↓
Level Data File (.json)
      ↓
Shared Unity mosaic package  (NixinStudioUnityCore/packages/com.nixin.mosaic)
      ↓
HiddenMosaic play scene  (HOGGame/HiddenMosaic)  → hunt gameplay
```

Shared vs game-owned:

- **`com.nixin.mosaic`**: JSON contract, spawn-one-GameObject-per-item, editor preview/tweak/save. No scoring, no find-list, no packing.
- **`tools/mosaic-level-gen`**: silhouette mask + packing. Other titles can call the same CLI.
- **`HiddenMosaic`**: hunt rules, play scene, HUD, level catalog. Scoring/story stay in `Assets/Game/Core/`.

Do **not** reimplement packing in C#. Python owns generation; Unity owns load + manual polish.

4. Python Level Generation Tool (Detailed)
4.1 Input

A single clean object image (PNG/JPG) with relatively simple silhouette (horse, watch, kangaroo, bird, etc.).
Optional: target color palette or style guide.

4.2 Processing Pipeline

Silhouette Extraction
Convert to binary mask (threshold + morphological clean-up).
Extract outer contour.

Internal Segmentation
Edge detection (Canny / structured forests).
Color-based segmentation (K-means or Mean-Shift) + watershed.
Shape-aware segmentation to identify distinct regions (eye, ear, mane, strap, face, body, etc.).
Output: list of regions with their own masks + average color + importance weight.

Asset Library
Large library of tiny object cutouts (PNG with alpha).
Metadata per object: name, preferred size range, color families it can accept, tags.

Packing / Placement Algorithm
For each segmented region:
Choose suitable tiny objects (color-matched or color-adjusted).
Pack them using a constrained packing algorithm (rejection sampling + collision detection + density control).
Allow slight overlap, rotation, scale variation.
Assign z-layer (depth order) so larger/important objects can sit on top if needed.

Preserve overall silhouette density so the big shape remains readable from a distance.

Color Adjustment
Tint or recolor tiny objects to better match the target region while keeping recognizability.

Output
JSON file containing one entry per placed object. Positions are **image pixels, origin top-left, Y down**. `width` / `height` are always written so Unity can center the mosaic.

```json
{
  "level_id": "horse_01",
  "silhouette": "horse",
  "background_color": "#F8A5C2",
  "width": 400,
  "height": 300,
  "items": [
    {
      "name": "circle",
      "position": [342.5, 189.2],
      "scale": 0.85,
      "rotation": 23.4,
      "z_layer": 12,
      "color_tint": [0.82, 0.55, 0.31, 1.0],
      "region": "body"
    }
  ]
}
```

If `width` / `height` are omitted, the Unity codec infers them from max item coordinates.

4.3 Tool Requirements

Command-line + simple GUI (optional).
Configurable density, min/max object size, overlap tolerance, color matching strength.
Preview image generation (final assembled look).
Batch mode for multiple source images.
Export both the data file and a reference PNG of the assembled mosaic.


5. Unity Implementation
5.1 Asset Setup

All tiny objects stored in one or more Sprite Atlases (tight packing, readable).
Naming convention must match the name field in the level data.
Optional secondary atlases for variants.

Phase 1 uses procedural primitive stamps instead of a painted atlas (`circle`, `square`, `triangle`, `star`, `blob`). `IMosaicStampCatalog` is the swap point for real sprites later.

5.2 Level Loading System

Parser for the JSON file (`MosaicLevelCodec` in `Nixin.Mosaic.Core`, no `UnityEngine`).
Instantiates one GameObject per item (`MosaicAssembler`).
Applies position, scale, rotation, z-order (`SpriteRenderer.sortingOrder`), and color tint.
Parent all items under `LevelRoot`.
Each item gets a `BoxCollider2D` and `MosaicItemView` (name, region, source index).
Image → world: `MosaicCoords` at 100 pixels per unit, origin at image center, Unity Y up.
Supports both runtime loading and Editor loading.

5.3 Unity Editor Window (“Mosaic Level Editor”)
Required features:

Load level data file.
Visual preview of the assembled mosaic.
Select individual items.
Move, rotate, scale, change z-layer, change tint via handles / inspector.
Add / remove / replace items.
Snap to grid or free placement.
“Regenerate from Python” button (optional integration).
Save modified data back to file.
Play-mode test from the editor window.

Phase 1 ships the load / scene preview / select / move-rotate-scale / save subset. Remaining editor features are Phase 2.

5.4 Runtime Gameplay Requirements

Each tiny object must remain a separate collider (or use a custom precise hit-test) so the player can click exactly on it.
Magnifying glass system:
Movable (mouse / touch).
Reveals clear version of objects under it (higher resolution or desaturated → full color, or scale-up effect).
Outside the glass, objects appear very small / slightly blurred / low contrast.

Memory system:
When an area is scanned, leave a temporary “memory imprint” (faint outline or particles).
Imprints fade over time or after a number of finds.

Click detection must work correctly under the glass and outside it.
Support for randomized variants of the same level (different object sets while keeping silhouette).

Phase 2 glass should compose with existing `com.nixin.pinhole` Zoom rather than a new shader from scratch.


6. Core Gameplay Loop

Player is shown a large mosaic silhouette + a short list of items to find.
Player moves the magnifying glass to scan.
Player remembers locations of interesting objects.
Player clicks the correct tiny object when it is under the glass (or after remembering its position).
Found items are removed or highlighted.
New items appear on the list or the player proceeds to the next level.

Difficulty increases via:

Smaller objects
Higher density
Faster memory fade
Smaller glass
More complex silhouettes

Phase 1 loop is click-to-find by stamp name (no glass, no memory fade). Duplicate names on the list need separate clicks.


7. Technical Specifications

| Area | Recommendation | Phase 1 actual |
| --- | --- | --- |
| Unity Version | 2022.3 LTS or 6000.x | **6000.5.9f1** |
| Render Pipeline | URP | **URP 2D** (`com.unity.template.2d-cross-platform-2d`) |
| Target Platforms | Android, iOS, Windows, macOS | Editor / desktop first |
| Sprite Atlases | Mandatory | Procedural stamps still work; Python bakes Unity Multiple-sprite PNG + `.atlas.json` |
| Data Format | JSON (preferred) | JSON via `MosaicLevelCodec` |
| Python Stack | OpenCV, Pillow, NumPy, scikit-image | OpenCV, Pillow, NumPy, pytest (no scikit-image yet) |
| Version Control | Git + Git LFS for atlases | Game under `HOGGame/`; shared code in `NixinStudioUnityCore` |

8. Risks & Mitigations

| Risk | Mitigation |
| --- | --- |
| Generated mosaics look messy | Strong region-based packing + color matching + manual editor polish |
| Click detection inaccurate on dense levels | Per-object colliders + optional pixel-perfect testing under glass |
| Performance on low-end mobile | Aggressive atlas usage + object pooling + LOD for distant view |
| Memory of scanned areas feels unfair | Tunable fade time + visual feedback |
| Silhouette readability lost | Density limits + larger “anchor” objects in key regions |

9. Deliverables (Phased)
Phase 1 – Foundation — **shipped 9 September 2026**

Python silhouette + one-region packing (full mask; not multi-region segmentation yet)
Unity level loader + simple Editor window
Playable click-to-find hunt on a generated demo level

Phase 2 – Full Pipeline — **shipped 9 September 2026**

Multi-region packing
Color tinting (region-aware mix of per-pixel + region average)
Complete Editor tooling (add/remove/replace, snap, regenerate-from-Python)
Magnifying glass + basic memory system (`com.nixin.pinhole` Zoom)
Python GUI + stamp atlas bake (folder of icons, or one sheet; Unity `.png.meta`)

Phase 3 – Gameplay & Polish — **shipped 9 September 2026**

Full memory mechanics (fade + decay on find + imprint cap)
Progression, UI, juice (catalog waves, next silhouette, find punch + audio)
Multiple silhouettes (horse / watch / bird)
Performance optimization (atlas batching, GO reuse, RT size, disable found colliders)
Real PNG icon library + Sprite Atlas (`stamps.png` / `.atlas.json`)


10. Success Criteria

A new silhouette can go from source image → playable Unity level in under 30 minutes (mostly automated).
Player can clearly recognize the big shape from a distance.
Clicking individual tiny objects feels precise and satisfying.
Memory mechanic is noticeable and rewarding, not frustrating.
Runs smoothly on mid-range Android devices at 60 fps.


11. Phase 1 implementation (as built)

11.1 Layout

```text
GameStudio/
  HOGGame/
    TRD-2d-mosaic-hog.md
    HiddenMosaic/                          # Unity 6 URP 2D project
  NixinStudioUnityCore/
    packages/
      com.nixin.mosaic/                    # shared mosaic runtime + editor
    tools/
      mosaic-level-gen/                    # Python CLI, sibling of packages
```

UPM path from HiddenMosaic (same pattern as Unlock Disk):

```json
"com.nixin.mosaic": "file:../../../NixinStudioUnityCore/packages/com.nixin.mosaic"
```

Also opted in: `com.nixin.game.core`, `com.nixin.boot`, `com.nixin.icons`, `com.nixin.audio`, `com.nixin.ui`, `com.unity.pipeline`.

11.2 Shared package `com.nixin.mosaic`

Mirrors `com.nixin.maze` / `com.nixin.pinhole`. No hunt rules.

| Layer | Assembly | Contents |
| --- | --- | --- |
| `Runtime/Core/` | `Nixin.Mosaic.Core` (`noEngineReferences`) | `MosaicLevel` / `MosaicItem` / `MosaicTint`, `MosaicLevelCodec`, `MosaicLevelValidator`, `MosaicCoords`, `HexColor`, `MosaicStampNames` |
| `Runtime/Unity/` | `Nixin.Mosaic` | `MosaicAssembler`, `IMosaicStampCatalog`, `PrimitiveMosaicStampCatalog`, `MosaicItemView`, lab HUD |
| `Editor/` | `Nixin.Mosaic.Editor` | Level Editor window, lab scene builder |

Menus:

- **Nixin Studio / Mosaic / Level Editor** — load JSON, assemble into `MosaicEditorHost`, select items, Scene-view move/rotate/scale, save JSON
- **Nixin Studio / Mosaic / Build Lab Scene** — writes `Assets/Scenes/MosaicLab.unity`

World mapping: JSON pixels → Unity at `MosaicCoords.DefaultPixelsPerUnit` (100). Stamp textures are 32×32 with the same PPU, so `scale` 1.0 is 32 image pixels wide.

Tests: `dotnet test packages/com.nixin.mosaic/DotNet~/Nixin.Mosaic.Core.sln`

Catalog entry lives in `packages/.cursor/skills/studio-packages/SKILL.md`.

11.3 Python tool `tools/mosaic-level-gen`

CLI (no GUI in Phase 1):

```bash
cd NixinStudioUnityCore/tools/mosaic-level-gen
python3 -m venv .venv
.venv/bin/pip install -r requirements.txt
.venv/bin/python -m mosaic_gen --input samples/blob.png \
  --out out/demo.json --preview out/demo.png \
  --level-id demo --silhouette blob --seed 1
```

Flags: `--density` (default 0.55), `--min-size` (0.5), `--max-size` (0.95), `--overlap` (0.22), `--seed`.

Pipeline (one region = full silhouette):

1. Load BGR image; Otsu threshold; invert if the border is “on”; morphological close/open.
2. Rejection sampling on mask pixels; reject if stamp circle overlaps existing stamps beyond `--overlap`, or hangs off the image.
3. Stamp name chosen from `circle` / `square` / `triangle` / `star` / `blob`.
4. Tint sampled from the source pixel at the centroid; background hex from image corners.
5. Write JSON + optional assembled preview PNG.

`make_blob_image()` builds a synthetic horse-like blob for tests and the first demo.

Tests: `.venv/bin/python -m pytest` (centroids inside mask; JSON has required fields).

11.4 Game `HOGGame/HiddenMosaic`

Studio folders: `Assets/Game/Core|Application|Unity/`, `DotNet/` for Core tests.

- `Game.Core.HuntSession` — target name list, `TryFind(name)`, remaining / found / complete. Duplicate names need separate finds. No glass, no memory fade.
- `PlayHost` — reads silhouette JSON from `StreamingAssets/SilhouetteJson/` via the Hunt Catalog asset, assembles via atlas or primitives.
- `HuntHud` — top bar list of remaining names.
- Menu **Nixin Studio / Hidden Mosaic / Rebuild Play Scene** writes `Assets/Scenes/Play.unity` (startup scene).

Demo silhouette JSON: `Assets/StreamingAssets/SilhouetteJson/` (generated from `tools/mosaic-level-gen`).

Tests: `dotnet test HiddenMosaic/DotNet/Game.Core.sln`

Cursor Unity MCP: `HiddenMosaic/.cursor/mcp.json` and `HOGGame/.cursor/mcp.json` point at this project.

11.5 Phase 1 out of scope (moved to Phase 2–3)

Phase 2 is section 12. Phase 3 is section 13. Still later if needed: Git LFS for huge painted atlases, mobile 60 fps profiling on device.


12. Phase 2 implementation (as built)

12.1 Multi-region packing (`tools/mosaic-level-gen`)

K-means in Lab-ish BGR on mask pixels (`--regions`, default 4; `1` = Phase 1 single mask). Tiny clusters are dropped. Each region is packed with shared collision so stamps do not stack across region borders.

Tint: `lerp(source pixel, region average, --color-match)` (default 0.45).

`--atlas path.atlas.json` selects stamps from a packed sheet (color-matched to the region, then tinted). Preview composites those same sprites.

GUI: `.venv/bin/python -m mosaic_gen --gui`

12.2 Stamp atlas tool

```text
python -m mosaic_gen atlas --from-dir icons/ --out out --knockout
python -m mosaic_gen atlas --from-sheet sheet.png --out out --knockout
python -m mosaic_gen atlas --from-sheet sheet.png --out out --knockout --in-place
```

Writes `stamps.png` + `stamps.atlas.json` + Unity `stamps.png.meta` (Sprite Mode = Multiple; Unity Y is bottom-left, JSON Y is top-left). `--knockout` turns a flat background into alpha 0. `--in-place` keeps the sheet layout and writes slice JSON (plus `_alpha.png` when knockout ran).

12.3 Mosaic package

- JSON `atlas` field. `AtlasMosaicStampCatalog` loads PNG+JSON at runtime (StreamingAssets or next to the level file).
- `MosaicScanMemory` — world-space imprints that fade over a duration.
- `MosaicMemoryMarks` — faint circle sprites on those imprints.
- Level Editor: add / remove / replace stamp name, snap-to-grid on save, **Regenerate from Python** (venv under `tools/mosaic-level-gen`).

12.4 HiddenMosaic glass

`MosaicGlass` renders the mosaic layer into an RT and feeds `PinholeSurface` **Zoom** (live dim outside, magnified hole). Clicks count if the item is under the glass *or* still covered by a memory imprint. HUD: “Find (scan with the glass)”.

`com.nixin.pinhole` is in HiddenMosaic `Packages/manifest.json`. Rebuild play scene: **Nixin Studio / Hidden Mosaic / Rebuild Play Scene**.


13. Phase 3 implementation (as built)

Full memory: imprints still fade over time, cap at 12, and **shrink on each find** (`find_decay` seconds). Marks sit on the glass object (not inside `LevelRoot`) so level switches can reuse stamp GameObjects.

Progression: `HuntCatalog` ScriptableObject (`Assets/Game/Unity/Data/HuntCatalog.asset`) lists silhouettes for the Inspector. Silhouette JSON lives in `StreamingAssets/SilhouetteJson/`. Python still writes `catalog.json` there; **Nixin Studio → Hidden Mosaic → Import Catalog JSON into Asset** refreshes the SO. Difficulty steps up (smaller glass, faster fade, denser pack). HUD shows title + wave; found stamps punch scale and play `NixinAudio` click/win.

Multiple silhouettes: horse, watch, bird — generated by `python -m mosaic_gen campaign` (a content bake, not a play mode).

Performance: one shared stamp atlas texture (`FilterMode.Point`); assembler reuses `LevelRoot` children on reload; glass RT is 512 or 1024; found colliders disable.

Icon library: 24 drawn PNGs packed to `StreamingAssets/Atlases/stamps.png` + `stamps.atlas.json`. Swap painted icons with the atlas tool, then **re-pack silhouette JSON** if sprite pixel sizes changed. Names must match; size is not stored as pixels in the JSON — only a scale factor — so a bigger `heart` PNG will draw bigger unless you regenerate.

```bash
.venv/bin/python -m mosaic_gen campaign --out out/campaign \
  --unity-dest ../../../HOGGame/HiddenMosaic/Assets/StreamingAssets
```

