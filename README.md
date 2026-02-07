# MDL to VMDL Converter

Direct Source 1 to s&box model conversion.

Input:
- `.mdl`
- companion `.vvd` and `.vtx` (`.dx90.vtx` / `.dx80.vtx` / `.sw.vtx` / `.vtx`)
- optional `.phy`

Output:
- `.vmdl`
- one `.smd` per mesh/bodygroup choice
- optional material package (`.vmat` + `.tga`) generated from VMT/VTF

No FBX/OBJ conversion step is required.

## What Works

- Mesh extraction from MDL/VVD/VTX
- Bone hierarchy + skin weights
- Bodygroups
- Hitbox sets
- Physics shape list
- Physics body markup list
- Physics joints
- Bone/body canonicalization for ModelDoc (`ValveBiped_*` -> `bip01_*` style)
- Material conversion pipeline:
  - VMT parsing
  - VTF decoding
  - profile detection (`Source`, `ExoPBR`, `GPBR`, `MWB`, `BFT`, `MadIvan18`, eye shaders)
  - VMAT generation + MaterialGroup remaps in VMDL
- Path-preserving export from GMod model paths

## GUI Usage

Run without arguments to open the GUI.

1. Pick your `.mdl`.
2. Pick GMod root (`...\GarrysMod\garrysmod`) or let it auto-detect.
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

The converter generates a `MaterialGroupList` with remaps from SMD material names to generated VMATs.

Material source lookup uses:
1. MDL texture directories
2. model-relative fallback path
3. raw material token fallback

When a VMT/VTF cannot be resolved, a fallback PBR VMAT is generated so import still succeeds.

## Known Limitations

- Animation/sequence conversion is not implemented.
- Attachments/IK/pose params are not emitted yet.
- Physics fitting is still conservative (box/sphere fallback strategy).
- Joint orientation is still basic (`anchor_angles` is neutral).

## Build

```powershell
dotnet build MdlToVmdlConverter.csproj
```
