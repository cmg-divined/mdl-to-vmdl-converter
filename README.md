# MDL to VMDL Converter

Direct Source 1 (`.mdl`) to s&box ModelDoc (`.vmdl`) converter.

No FBX/OBJ step.

## Status

Working for production model imports with:
- mesh + skin weights
- skeleton + bodygroups
- hitboxes
- physics bodies + joints
- VMT/VTF to VMAT/TGA conversion
- model-relative output path preservation (`models/...`)

## Input / Output

Input:
- `.mdl`
- companion `.vvd` and `.vtx` (`.dx90.vtx` / `.dx80.vtx` / `.sw.vtx` / `.vtx`)
- optional `.phy`

Output:
- `.vmdl`
- one `.smd` per mesh/bodygroup choice
- optional material package (`.vmat` + `.tga`)

## What Works

- Mesh extraction from MDL/VVD/VTX
- Bone hierarchy + skin weights
- Bodygroups
- Hitbox sets
- Physics shape list
- Physics body markup list
- Physics joints
- Bone/body name canonicalization for ModelDoc (`ValveBiped.*` -> `bip01_*` naming)
- Material conversion pipeline:
  - VMT parsing
  - VTF decoding
  - profile detection (`Source`, `ExoPBR`, `GPBR`, `MWB`, `BFT`, `MadIvan18`, eyes)
  - VMAT generation + `MaterialGroupList` remaps in VMDL
- Correct VMAT texture slots:
  - PBR: `Color`, `Normal`, `Roughness`, `Metalness`, `AmbientOcclusion`
  - Eyes: `Iris`, `Cornea`
- SMD file references in VMDL are written with model-relative paths (`models/...`) so ModelDoc resolves them correctly

## GUI Usage

Run without arguments to open the GUI.

1. Pick your `.mdl`.
2. Pick GMod root (`...\GarrysMod\garrysmod`) or let auto-detect fill it.
3. Pick output root.
4. Keep `Preserve models/... output path` enabled.
5. Click `Convert`.

Example input:
`D:\SteamLibrary\steamapps\common\GarrysMod\garrysmod\models\madivan18\ww2\ukpara\sum\chr_alfred_a1pa.mdl`

With preserve-path enabled, model output lands at:
`<outputRoot>\models\madivan18\ww2\ukpara\sum`

Materials are written under:
`<outputRoot>\materials\...`

Custom shaders from `gmod_mount/assets/shaders` are copied to:
`<outputRoot>\shaders`

## CLI Usage

Basic:

```powershell
dotnet run --project MdlToVmdlConverter.csproj -- --mdl "C:\path\model.mdl"
```

Path-preserving export from GMod:

```powershell
dotnet run --project MdlToVmdlConverter.csproj -- `
  --mdl "D:\SteamLibrary\steamapps\common\GarrysMod\garrysmod\models\madivan18\ww2\ukpara\sum\chr_alfred_a1pa.mdl" `
  --gmod-root "D:\SteamLibrary\steamapps\common\GarrysMod\garrysmod" `
  --out "C:\exports\my_addon" `
  --preserve-path
```

Force a material profile (optional):

```powershell
--profile madivan18
```

Supported `--profile` values:
- `auto`
- `source`
- `exo`
- `gpbr`
- `mwb`
- `bft`
- `madivan18`

## Material Notes

The converter writes `MaterialGroupList` remaps from original SMD material names to generated VMATs.

Material source lookup order:
1. MDL texture directories
2. model-relative fallback path
3. raw material token fallback

If a VMT/VTF cannot be resolved, a fallback PBR VMAT is generated so model import still completes.

## Known Limitations

- Animation/sequence conversion is not implemented yet.
- Attachments / IK / pose parameters are not emitted yet.
- Physics fitting is still conservative (box/sphere fallback strategy).
- Joint orientation is still basic (`anchor_angles` kept neutral).

## Build

```powershell
dotnet build MdlToVmdlConverter.csproj
```
