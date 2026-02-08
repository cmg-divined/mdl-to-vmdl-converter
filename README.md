<img width="1075" height="744" alt="image" src="https://github.com/user-attachments/assets/079745d6-b63b-4f67-a12b-debec8e7e8f6" />


# MDL to VMDL Converter

Direct Source 1 (`.mdl`) to s&box ModelDoc (`.vmdl`) converter.

No FBX/OBJ step.

## Current Status

Working in day-to-day imports:
- Mesh extraction from MDL/VVD/VTX
- Bone hierarchy + skin weights
- Attachment export (`AttachmentList`)
- Bodygroups
- Skin-family (`$texturegroup`) to MaterialGroup export
- Hitbox sets
- Physics shape/body/joint export
- Sequence animation export (`.smd`) + `AnimationList` in `.vmdl`
- VMT/VTF to VMAT/TGA conversion
- Eye materials mapped to Source 2 `shaders/eyeball.shader`
- Flex/morph channel export (meshes with morphs are written as `.dmx`)
- Model-relative output path preservation (`models/...`)
- Batch conversion with recursion + multithreaded workers
- Auto GMod root detection from input path

## Input / Output

Input:
- `.mdl`
- companion `.vvd` and `.vtx` (`.dx90.vtx` / `.dx80.vtx` / `.sw.vtx` / `.vtx`)
- optional `.phy`

Output:
- `.vmdl`
- `.smd` meshes when no morph data is present
- `.dmx` meshes when morph data is present
- optional `.smd` animation clips + `AnimationList` entries in `.vmdl`
- optional material package (`.vmat` + `.tga`)

## GUI Usage

Run without arguments to open the GUI.

Single model:
1. Pick your `.mdl`. Make sure if you are extracting from an addon you extract the materials/models folders to GarrysMod\garrysmod\ or else this won't work.
2. Pick GMod root (`...\GarrysMod\garrysmod`) or let auto-detect fill it.
3. Pick output root.
4. Keep `Preserve models/... output path` enabled.
5. Click `Convert`.

Batch:
1. Enable `Batch mode`.
2. Set `Batch Root` to a folder that contains `.mdl` files.
3. GMod root is auto-detected from that path when possible (`.../garrysmod`).
4. Keep `Recursive` enabled to include subfolders.
5. Set `Threads` for parallel workers.
6. Click `Convert`.

Example input:
`D:\SteamLibrary\steamapps\common\GarrysMod\garrysmod\models\madivan18\ww2\ukpara\sum\chr_alfred_a1pa.mdl`

With preserve-path enabled, model output lands at:
`<outputRoot>\models\madivan18\ww2\ukpara\sum`

Materials are written under:
`<outputRoot>\materials\...`

Custom shaders are copied to:
`<outputRoot>\shaders`

Shader copy source resolution order:
1. `--shader-src <dir>` if provided
2. nearest `shaders` folder in/above current tool location (including `MdlToVmdlConverter/shaders`)
3. fallback `gmod_mount/assets/shaders` if found

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

Batch convert a folder recursively with 8 workers:

```powershell
dotnet run --project MdlToVmdlConverter.csproj -- `
  --batch "D:\SteamLibrary\steamapps\common\GarrysMod\garrysmod\models\madivan18" `
  --gmod-root "D:\SteamLibrary\steamapps\common\GarrysMod\garrysmod" `
  --out "C:\exports\my_addon" `
  --threads 8 `
  --recursive
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

Other useful flags:
- `--batch <dir>` convert all `.mdl` files under a folder
- `--recursive` / `--no-recursive` folder traversal behavior in batch mode
- `--threads <n>` parallel worker count in batch mode
- `--animations` / `--no-animations` export sequence clips
- `--copy-shaders` / `--no-copy-shaders`
- `--shader-src <dir>` explicit shader source override

## Material Notes

The converter writes `MaterialGroupList` remaps from original material references to generated VMATs.
When MDL skin families are present, additional non-default material groups are generated to mirror Source skin selection.

Material source lookup order:
1. MDL texture directories
2. model-relative fallback path
3. raw material token fallback

If a VMT/VTF cannot be resolved, a fallback PBR VMAT is generated so import still completes.

Eye material behavior:
- writes Source 2 `shaders/eyeball.shader`
- maps `TextureColor` + `TextureIrisMask` from iris texture
- maps `IrisNormal` from cornea texture
- sets occlusion to neutral defaults to avoid black-eye artifacts in converted assets

## Morph Notes

- Flexes are exported from MDL mesh anim blocks into DMX morph channels.
- Channel display names are normalized for ModelDoc readability.
- This does not execute full Source QC flexcontroller expression logic (`localvar`/`%` rules), so some advanced controller behavior can still differ from StudioMDL.

## Known Limitations

- IK / pose parameters are not emitted.
- Physics fitting is intentionally conservative (box/sphere fallback strategy).
- Joint orientation is still basic (`anchor_angles` neutral).
- Complex QC-driven facial controller behavior is not fully replicated yet.
- Animation export currently uses direct local sequence/anim data and chooses the primary blend entry; complex pose-parameter blend behavior can still differ from StudioMDL.

## Build

```powershell
dotnet build MdlToVmdlConverter.csproj
```
