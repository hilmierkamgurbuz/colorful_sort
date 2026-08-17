<!-- stamp: 2026-08-17T13:58Z source-sig:4aa0edee472c scenes:1 prefabs:0 generator:python-fallback status: DEGRADED 3 missing-script -->
# unitymap — scene and prefab structure

Read this instead of opening a `.unity`/`.prefab` file. Tree indentation is
the GameObject hierarchy; `[...]` lists the components on the object;
`refs:` lists serialized reference slots and whether the Inspector has
something in them. `*` marks a prefab instance.

Staleness: `source-sig` is derived from scene/prefab mtimes. Regenerate with
`python3 .claude/hooks/build_unitymap.py` or, for real type information, the
Unity menu item Tools > unity-dev > Export unitymap.

## Findings
- MISSING SCRIPT | Assets/Scenes/SampleScene.unity | Directional Light
- MISSING SCRIPT | Assets/Scenes/SampleScene.unity | Global Volume
- MISSING SCRIPT | Assets/Scenes/SampleScene.unity | Main Camera

## SCENE Assets/Scenes/SampleScene.unity   (3 object(s))
- Main Camera  [AudioListener, Camera, Transform, MISSING SCRIPT (guid:a79441f3)]
- Directional Light  [Light, Transform, MISSING SCRIPT (guid:474bcb49)]
- Global Volume  [MISSING SCRIPT (guid:17251560), Transform]  refs: sharedProfile=set
