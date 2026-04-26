Generic Gore Tool
=================

Files
- Runtime/GenericGoreProfile.cs
- Runtime/GenericGoreRig.cs
- Editor/GenericGoreGeneratorWindow.cs

What it does
- Generates one SkinnedMeshRenderer per sever group from a single source SkinnedMeshRenderer
- Keeps the creature fully animated until a part is severed
- Bakes only the severed part into a physics debris mesh at runtime
- Uses the source materials, so it is URP-safe if your creature already uses URP materials

Current limitations
- Best with one source SkinnedMeshRenderer
- Does not generate true geometry caps yet. Use a wound VFX prefab or decal for the cut surface.
- Boundary triangles are assigned by majority bone influence, so tiny seams can happen on some rigs.

Basic setup
1. Put Runtime scripts in a normal folder and the generator script in an Editor folder.
2. Enable Read/Write on the creature mesh import settings.
3. Open Tools > Generic Gore > Generator.
4. Assign the character root, source SkinnedMeshRenderer and an output folder.
5. Click Auto Create Profile, then refine groups if needed.
6. Click Generate Gore Rig.
7. Call GenericGoreRig.Sever("Tail", hitPoint, hitDirection) or ExplodeAll(hitPoint).
