# MDL to VMDL Converter

Direct converter for Source 1 model assets (`.mdl/.vvd/.vtx/.phy`) into s&box-friendly `.vmdl + .smd` output.

This project exists to migrate GMod/Source models to s&box without relying on FBX/OBJ conversion hops.

## Current Status

Working and used in real test models:

- Mesh extraction from `.mdl + .vvd + .vtx`
- Bodygroups
- Bone hierarchy export
- Hitbox set export
- Physics shape export
- Physics body markup export
- Physics joint export
- Bone/body name normalization to match ModelDoc naming (`bip01_*` style)

Not done yet:

- Material remap generation
- Animation/sequence conversion
- Attachments/IK/pose params passthrough
- Authoring-quality physics orientation tuning (anchors are good enough for import, but still basic)

## What It Generates

For each input model, the converter outputs:

- One `.vmdl`
- One `.smd` per render mesh/bodygroup choice
- One internal skeleton-anchor `.smd` used to force full skeleton import

Output is written to `<modelname>_converted` by default (or `--out` if specified).

## Requirements

- .NET 10 SDK
- Source 1 model files:
  - required: `.mdl`, `.vvd`, one `.vtx` variant (`.dx90.vtx`, `.dx80.vtx`, `.sw.vtx`, or `.vtx`)
  - optional: `.phy`

## Build

```powershell
dotnet build MdlToVmdlConverter.csproj
```

## Usage

Basic:

```powershell
dotnet run --project MdlToVmdlConverter.csproj -- "C:\path\model.mdl"
```

Custom output folder:

```powershell
dotnet run --project MdlToVmdlConverter.csproj -- "C:\path\model.mdl" --out "C:\path\out_model"
```

Explicit companion files:

```powershell
dotnet run --project MdlToVmdlConverter.csproj -- `
  --mdl "C:\path\model.mdl" `
  --vvd "C:\path\model.vvd" `
  --vtx "C:\path\model.dx90.vtx" `
  --phy "C:\path\model.phy" `
  --out "C:\path\out_model" `
  --vmdl "model.vmdl" `
  --verbose
```

## How It Works

1. Loads Source model data (`MdlFile`, `VvdFile`, `VtxFile`, optional `PhyFile`).
2. Rebuilds triangles per body part/model from strip groups in VTX.
3. Writes SMD meshes with weighted vertices and full node list.
4. Builds bodygroup choices from MDL body part structure.
5. Converts MDL hitboxes to ModelDoc hitbox capsules.
6. Converts PHY convex data into basic physics shapes (currently box/sphere fallback).
7. Converts PHY ragdoll constraints into conical/revolute joints.
8. Emits a `.vmdl` graph (`RenderMeshList`, `BodyGroupList`, `HitboxSetList`, `PhysicsShapeList`, `PhysicsBodyMarkupList`, `PhysicsJointList`).

## Notes About Bone Naming

ModelDoc often canonicalizes imported Source-style names.  
This converter now canonicalizes the same way for all references:

- `ValveBiped_Bip01_Pelvis` -> `bip01_pelvis`
- same canonical name is used in:
  - SMD node names
  - hitbox `parent_bone`
  - physics shape `parent_bone`
  - physics body/joint names

This avoids `Unknown bone` / `Unknown physics body` errors during compile.

## Known Limitations

- Materials are not fully wired yet (geometry conversion first, shading later).
- Physics shape fitting is coarse for now (AABB from convex hull vertices).
- Joint anchor orientation is minimal (`anchor_angles` currently defaulted).
- The skeleton anchor mesh is internal and hidden through an internal bodygroup, but still part of generated data by design.

## Roadmap

- Add material group generation and remap rules from MDL skin families.
- Improve physics shape fitting (capsule/cylinder heuristics from hulls).
- Better constraint orientation and limit axis mapping.
- Export attachments and optional bone markup controls.
- Optional animation sequence export path.

## Credits

- Uses parser logic adapted from local Source-format tooling in this workspace (`SourceFormats`).
- Big thanks to Crowbar and Source reverse-engineering work over the years; this project stands on that ecosystem.

