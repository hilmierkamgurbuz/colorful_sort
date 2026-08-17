---
paths:
  - "Assets/Data/**"
---
<!-- Path-scoped rule — loading behavior: see the note in ui.md. -->

# Data layer rules

- Content lives only here; no magic numbers/tables in code.
- Every data type's authority is defined in fingerprint.md; no data type is
  added without a defined authority. `Data/` is read-only at runtime — nothing
  in a build writes back to a ScriptableObject.
- **Levels (`Data/Levels/`)** store logical colour ids only. Never an RGB value,
  never a symbol name, never a mesh reference. A level records: index,
  difficulty label, grid layout (rows × columns), and per column its capacity,
  its kind (normal / ice / covered / mystery) and its contents bottom-up.
- **Skins (`Data/Blocks/`)** own the one mapping `colourId → (colour, symbol
  mesh)`, in a single `BlockSkinSet` asset. Re-skinning the game — the cat
  becoming a moon — is an edit to that asset and to nothing else. If a second
  place in the project also decides what a colour looks like, that is the
  single-writer invariant broken.
- **Config (`Data/Config/`)** holds tuning: hearts and their refill, booster
  costs and charges, animation timings, camera framing. A number a designer
  might want to change lives here, not in a serialized field default.
- Level assets are authored by the level-editor tool, not hand-edited as YAML.
  A level that will not round-trip through the tool is a broken level.
- Asset file names are stable and meaningful (`Level_0079`, `Skin_Moon`).
  Renaming an asset is a task, not a cleanup — GUID references survive, but the
  level database's ordering must be rechecked.
