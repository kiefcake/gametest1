# Dungeon Prop Modeling Spec

Companion to the earlier "Low-poly creature modeling spec" -- same deal, different
target. That spec produced `creatures.js`: real Three.js geometry-construction code
built from a handful of shared primitives, which got exported through a Node.js
pipeline into OBJ+MTL pairs under `Assets/Resources/Models/`. This spec is written to
produce the prop equivalent: `props.js`.

Everything currently dressing the dungeons (`Assets/Scripts/World/DungeonLayout.cs`,
`Assets/Scripts/World/Chest.cs`) is built from bare Unity primitives at runtime --
cubes, cylinders, capsules, spheres, flat-colored, no real silhouette. That's fine for
blockout but it's the reason torches, chests, pillars, and hazard pools all read as
"a cube" or "a colored disc" instead of an actual object. This spec is the prompt for
replacing them with real low-poly models, using the exact same pipeline that already
worked for the bestiary.

## What to build

**Write a script named `props.js`**, structured exactly like `creatures.js` was:

- A small set of **shared primitive helpers** (reuse `creatures.js`'s own `mat`, `ball`,
  `lathe`/`seg`, `spike`, `ring` where they fit -- a lot of these props are lathed
  revolve shapes, which `lathe`/`seg` already does well) plus a few new ones this set
  needs that creatures didn't: `box(w,h,d,color)` for a plain flat-shaded cube mesh, and
  `plane(w,h,color)` for flat panels (banners, grate tops).
- A **`PROPS` registry** object, one entry per prop below, each a function that returns
  a `THREE.Group` -- same shape as `CREATURES`.
- The same **`fit(group, targetHeight)`** helper creatures.js used: scales the whole
  group so its tallest dimension matches a target size, then grounds it at y=0 and
  centers it on x/z. Every prop below lists its own target height/footprint for this.
- **`THREE.ColorManagement.enabled = false`** before creating any Color/Material --
  this project's hex-to-material convention is a direct 0-255-to-float mapping with no
  gamma correction, and skipping this line was the exact cause of the "missing colors"
  bug the creature batch shipped with (colors get sRGB-darkened silently otherwise).
- **`flatShading: true`** on every material, matching creatures.js -- these are meant
  to read as chunky low-poly props, not smooth-shaded ones. Remember this only takes
  effect through the exporter if triangles get expanded to 3 unique vertices with a
  computed per-face normal (the exporter already does this; just don't fight it by
  trying to pre-smooth normals in the generator script itself).
- **Poly budget**: keep every prop under ~150 triangles, most well under 60. These sit
  in rooms with several enemies, hazards, and other props all live at once -- they need
  to be cheap, not just simple-looking.
- **Pivot convention**: every prop's origin is its base-center (bottom of its footprint,
  centered in X/Z) after `fit()` grounds it -- this matches how creatures.js's models
  import (each part sits at local-transform identity in Unity, positioned in world
  space by the exporter), and it's what lets `DungeonLayout.cs` place a prop with a
  single `transform.position = pos` and no manual offset hunting.
- **No moving parts baked into the mesh.** A chest's lid, a torch's flame, a grate's
  hinge -- none of that needs to be a separate rigged part. Where a prop currently has
  runtime behavior (a chest that opens, a torch that flickers, a hazard pool that
  glows), that behavior stays exactly where it already lives in C# (`Chest.cs`,
  `TorchFlicker.cs`, `PortalGlow.cs`) driving the WHOLE model's transform/material, not
  a sub-part. If a prop's silhouette really benefits from a distinct lid/door piece
  (the chest does), model it as a separate top-level mesh in the same OBJ so Unity
  imports it as a sibling child object the way the multi-part creatures already do --
  not as a rigged hierarchy.

## Shared palette reference

Match these hex values so a prop reads correctly against the room it's dropped into.
Reuse `mat(hex)` for all of them, same as creatures.js. Every dungeon shares torches,
chests, pillars, and rubble (same trim colors below, no theme override) -- only the
hazard pool and per-theme clutter families change their base color per theme.

| Theme             | Wall stone | Trim/cap  | Hazard pool base | Hazard glow |
|--------------------|-----------|-----------|-------------------|-------------|
| Abyss              | #1c090c   | #3a1418   | #e65a0d (lava)     | #ff9928     |
| Frozen Crypt       | #adccf1   | #ffffff   | #8cd9ff (ice)      | #c0f2ff     |
| Sunken Ruins       | #2e4738   | #4a6b52   | #596b1f (bog)      | #8c9433     |
| Snake Pit          | #664c2e   | #8a6b3f   | #241c14 (grate)    | n/a         |
| Wraithbound Sanctum | #291a33  | #4a3352   | #59263f (grave)    | #8cd980     |

Torch flame stays `#ff9926` in every theme (fire is fire regardless of dungeon).
Metal fittings (chest bindings, torch brackets, grate bars, banner rods) stay a
consistent worn-iron `#2b2622` with a `#5c554c` highlight edge across every theme --
these are the one "always the same regardless of theme" family, same rule creatures.js
used for weapon/claw metal.

## The prop list

### A. Light sources

**`wallTorch`** -- target height 0.9. An angled iron bracket (a bent `lathe` profile
or two beveled boxes at an angle) mounted flush against a wall, holding a fuel bowl at
its top with a stylized flame shape rising out of it (a tall thin cone or a 2-3-segment
`spike`-style tapered shape, NOT a plain sphere -- the current placeholder's ball-flame
is the single most obviously-placeholder thing in every room). Replaces
`DungeonLayout.BuildTorch`'s cube-holder + sphere-flame. Keep the flame as its own
top-level part so `TorchFlicker.cs` can keep independently scaling/recoloring just that
piece for the flicker effect, same as it does today.

**`standingTorch`** -- target height 1.6. Same bracket/bowl/flame language as
`wallTorch` but on a floor-standing iron pole with a small tripod or flared foot base,
for corridor junctions and branch pockets where there's no wall to mount to.

**`brazier`** -- target height 1.1, footprint radius 0.5. A wide iron bowl on three or
four short splayed legs, bigger fuel capacity than a torch reads as -- for boss rooms
and vault rooms where a single small flame undersells the space. Same flame-piece
convention as the torches (separate top-level part for `TorchFlicker.cs`).

### B. Rewards

**`chestCommon`** -- target height 0.75, footprint 0.9 x 0.6. A banded wooding chest:
slightly domed or flat lid, 2-3 iron corner/edge bindings, a simple front clasp. Model
the lid as its own separate top-level part (matching `Chest.cs`'s existing
`lidVisual`), pivoted so it can rotate open around its back edge -- give it a hinge-line
vertex seam at the back-top edge, not a full separate pivot object; `Chest.cs` already
just rotates the lid transform on open, this only needs the mesh's own geometry to
rotate believably around that back edge without visibly detaching from the base.

**`chestRare`** -- target height 0.85, footprint 1.0 x 0.65. Same shape language as
`chestCommon` but with a stone/dark-metal body instead of wood, gold trim bindings, and
a more ornate clasp -- reserved for vault-room and boss-drop rewards, should visually
outclass the common chest at a glance, same rarity-tell principle the item-icon system
already uses (see `Visuals/IconFactory.cs`).

**`brokenUrn`** -- target height 0.5. A cracked lathed urn (this one's an easy
`lathe`/`seg` job -- a classic urn revolve profile) lying on its side or tilted with a
visible crack/chip on one side, replacing `DungeonLayout.BuildShatteredReliquary`'s
plain cylinder. Reused across all themes as generic "something valuable was here"
dressing, not just Wraithbound Sanctum.

### C. Hazards

**`hazardBasin`** -- target footprint radius 2.5 (matches every dungeon's current pool
radius), height 0.15. A shallow stone basin/crater rim (a lathed ring profile, open in
the middle) instead of the current flat colored disc -- the liquid/glow itself stays a
flat emissive plane at the basin's floor, built and animated in C# exactly like
`PortalGlow` already does (this model only needs to provide the rim geometry the glow
plane sits inside). One shared model, recolored per theme via the palette table above
(lava/ice/bog/grave-glow) -- do not model four separate basins.

**`spikeTrap`** -- target footprint 1.0 x 1.0, height 0.5 extended / 0.05 retracted.
A small square floor grate/plate with 4-6 metal spikes rising through slots in it.
Model it in its EXTENDED (spikes-up, dangerous) pose -- the retract/extend animation is
a runtime job (a simple Y-scale or Y-position lerp on the spike cluster, which should
be its own top-level part separate from the base plate for exactly that reason), not
baked into two separate meshes. This is a genuinely new hazard type with no existing
C# component yet (`LavaHazard.cs` is a trigger-volume-only hazard, not a
telegraphed/retracting one) -- flag that back for a follow-up gameplay pass once the
model exists; this spec covers the asset, not the trap logic.

### D. Structural dressing

**`pillar`** -- target height matches room `wallHeight` at import scale (model at a
neutral height like 3.0 and let Unity's existing per-room `localScale.y` stretch handle
the rest, same as how `DungeonLayout.BuildPillar` already just scales a cube to
`wallHeight`) footprint 0.7 x 0.7. A square column with a distinct capital (wider block)
at the top and a matching base plinth at the bottom, plus 2-4 vertical fluting grooves
down the shaft -- replaces the current perfectly-plain stretched cube, which is the
single flattest-reading piece of geometry in every room.

**`wallBanner`** -- target height 1.8, width 0.8, near-flat (0.05 deep). A hanging
cloth banner on a short horizontal rod, with a slightly wavy bottom edge (3-4 segments
along the bottom, gently offset in Z) rather than a perfectly flat rectangle -- new
prop, no current equivalent. Model in a neutral light gray/cream so it can be
recolored per theme at runtime via the same `SetColor`/material-swap convention every
other prop uses (deep red for Abyss, ice-blue for Frozen Crypt, mossy green for Sunken
Ruins, brown/ochre for Snake Pit, purple for Wraithbound), rather than modeling five
separate banners.

**`doorArch`** -- target opening width matches `corridorWidth` at import scale (model
at a neutral 6-unit opening and let Unity scale it, same convention as `pillar`),
height to clear `wallHeight`. A decorative stone arch/frame -- a simple lintel-and-
jambs shape, NOT a full archway curve (keep it low-poly and let the door OPENING stay a
plain rectangular gap, this only dresses the two side jambs and a lintel bar above) --
to sit around each room's existing door gaps instead of leaving them as bare wall
edges.

### E. Clutter families (per-theme "scattered debris" props)

Each of these already exists as loose primitive capsules/cubes assembled at runtime in
`DungeonLayout.cs`; this section gives each family one real hero mesh instead of a pile
of stretched capsules. `DungeonLayout`'s existing `BuildXCluster` methods should keep
their existing "scatter N of these with random rotation/offset" logic -- they just
instantiate this one mesh instead of `GameObject.CreatePrimitive(PrimitiveType.Capsule)`.

**`boneShard`** (Abyss's `BuildBonePile`) -- target length 0.4. A single curved,
tapered bone fragment -- not a straight capsule. Pair with:

**`skull`** (also `BuildBonePile`) -- target height 0.3. A simple low-poly skull:
rounded cranium, a jaw wedge, two eye-socket indents (a `ball` with a couple of
inset-vertex hollows, or two dark inset discs if true geometry hollows are too many
triangles for the budget -- flat dark material discs are fine here, this is a small
background prop).

**`iceSpike`** (Frozen Crypt's `BuildIceSpikes`) -- target height 0.6. A jagged,
irregular crystal spike (angular, NOT the smooth tapered cone a `spike()` helper alone
would give you -- facet it with a few random-ish extra side cuts so it reads as ice
crystal rather than a horn).

**`reed`** (Sunken Ruins' `BuildReedCluster`) -- target height 0.7. A thin, slightly
curved blade shape (a bent lathe/segment strip) rather than a straight capsule --
reeds should read as bending, not as rigid rods.

**`rubbleShard`** (Wraithbound's `BuildShatteredReliquary`, generalized for reuse
anywhere "broken stone debris" fits) -- target length 0.4. An angular broken stone
chunk, a few flat facets, no two identical (generate 2-3 variants if convenient, the
existing scatter code already randomizes scale/rotation per instance so full geometric
variety per spawn isn't required).

**`snakeGrate`** (Snake Pit's `BuildSnakeGrate`) -- target footprint 2.0 x 2.0, height
0.1. An iron grate of crossed bars over a dark pit-like recess, replacing the current
flat colored disc -- should read as "something could come up through this," matching
its actual gameplay role as a `SnakeGrateSpawner`.

### F. Atmosphere (new, not yet used anywhere -- optional stretch set)

**`statue`** -- target height 2.2. A weathered humanoid or gargoyle-like statue on a
plinth, arms/pose can be simple (arms-at-sides or arms-crossed is fine, this doesn't
need creature-level articulation) -- for boss-room corners and vault-room dressing,
giving the grander rooms something besides pillars and torches to fill their now-larger
footprint (see the "bigger, taller" dungeon rework this batch shipped alongside).

**`cage`** -- target height 1.4, footprint 0.8 x 0.8. A simple barred cage (4-6 vertical
iron bars on a small floor plate, a bar-frame top) -- pure atmosphere for now (an empty
or bone-containing cage reads as "something was kept here"), no gameplay hook required
yet.

**`crate`** / **`barrel`** -- target height 0.6 / 0.7. Plain wooden shipping clutter --
a banded box and a banded lathed barrel -- generic enough to scatter in any room
regardless of theme, the way real dungeon crawlers use crates/barrels as filler texture
in otherwise-empty corners.

## Export/integration notes (same pipeline as the creature batch)

- Export via the same Node.js + real `three` package script pattern used for
  `creatures.js` -- run the generator, walk each `PROPS` entry, expand triangles for
  flat shading, write OBJ+MTL pairs.
- Save to `Assets/Resources/Models/Props/<name>.obj` / `.mtl` (new folder, sibling to
  the existing `Models/Enemies/` and `Models/Bosses/`).
- **Double-check every `.obj`'s `mtllib` line matches the ACTUAL saved `.mtl` filename**
  before calling this done -- this exact mismatch (hyphenated vs. underscored
  filenames, and one file referencing a completely wrong sibling name) was the real,
  shipped bug behind the "missing colors" report on the creature batch. Verify by
  grepping every new `.obj`'s first line against `ls` of the actual `.mtl` files in the
  same folder, not by eyeballing the generator script.
- Check `material.emissive.r/g/b > 0` (not `emissiveIntensity` truthiness, which is
  always true by default and silently darkens non-glow materials) if any prop here
  ends up wanting a genuine glow material (the hazard basin's rim likely doesn't need
  one since the glow plane is a separate runtime-built object, but the torch/brazier
  flame pieces might, if a modeled flame reads better emissive than flat-colored).
