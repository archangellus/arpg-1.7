using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GenericGore.Editor
{
    public class GenericGoreGeneratorWindow : EditorWindow
    {
        private GameObject characterRoot;
        private SkinnedMeshRenderer sourceRenderer;
        private GenericGoreProfile profile;
        private DefaultAsset outputFolder;

        [MenuItem("Tools/Generic Gore/Generator")]
        public static void Open()
        {
            GetWindow<GenericGoreGeneratorWindow>("Generic Gore Generator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Generic Gore Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This version uses per-triangle clipping instead of whole-triangle assignment. That lets it generate real cut boundaries and filled caps for non-humanoid skinned creatures in URP projects.\n\n" +
                "For best results, enable Read/Write on the source mesh and keep your gore groups fairly coarse, like tail, neck, upper leg, wing, forearm or head.",
                MessageType.Info);

            characterRoot = (GameObject)EditorGUILayout.ObjectField("Character Root", characterRoot, typeof(GameObject), true);
            sourceRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Source Renderer", sourceRenderer, typeof(SkinnedMeshRenderer), true);
            profile = (GenericGoreProfile)EditorGUILayout.ObjectField("Profile", profile, typeof(GenericGoreProfile), false);
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selected"))
                    PullFromSelection();

                if (GUILayout.Button("Auto Create Profile"))
                    AutoCreateProfile();
            }

            GUI.enabled = characterRoot != null && sourceRenderer != null && profile != null && outputFolder != null;
            if (GUILayout.Button("Generate Gore Rig", GUILayout.Height(36f)))
                GenerateRig();
            GUI.enabled = true;
        }

        private void PullFromSelection()
        {
            if (Selection.activeGameObject == null)
                return;

            characterRoot = Selection.activeGameObject;
            sourceRenderer = characterRoot.GetComponentInChildren<SkinnedMeshRenderer>();
        }

        private void AutoCreateProfile()
        {
            if (characterRoot == null || sourceRenderer == null)
            {
                EditorUtility.DisplayDialog("Generic Gore", "Assign Character Root and Source Renderer first.", "OK");
                return;
            }

            if (outputFolder == null)
            {
                EditorUtility.DisplayDialog("Generic Gore", "Assign an Output Folder first.", "OK");
                return;
            }

            string folderPath = AssetDatabase.GetAssetPath(outputFolder);
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                EditorUtility.DisplayDialog("Generic Gore", "Output Folder must be inside the Unity project.", "OK");
                return;
            }

            Transform rootBone = sourceRenderer.rootBone != null ? sourceRenderer.rootBone : sourceRenderer.transform;
            GenericGoreProfile newProfile = CreateInstance<GenericGoreProfile>();
            newProfile.name = sourceRenderer.name + "_GoreProfile";
            newProfile.fallbackGroup = 0;

            var core = new GoreGroupDefinition
            {
                name = "Core",
                pivotBonePath = string.Empty,
                bonePaths = new List<string> { string.Empty },
                forceMultiplier = 1f
            };
            newProfile.groups.Add(core);

            foreach (Transform child in rootBone)
            {
                GoreGroupDefinition group = new GoreGroupDefinition
                {
                    name = child.name,
                    pivotBonePath = GetPath(rootBone, child),
                    bonePaths = CollectPaths(rootBone, child),
                    forceMultiplier = 1f
                };
                newProfile.groups.Add(group);
            }

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{newProfile.name}.asset");
            AssetDatabase.CreateAsset(newProfile, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            profile = newProfile;
            EditorGUIUtility.PingObject(profile);
        }

        private void GenerateRig()
        {
            if (!ValidateInputs())
                return;

            string folderPath = AssetDatabase.GetAssetPath(outputFolder);
            Mesh sourceMesh = sourceRenderer.sharedMesh;
            if (sourceMesh == null)
            {
                EditorUtility.DisplayDialog("Generic Gore", "Source renderer is missing a shared mesh.", "OK");
                return;
            }

            if (!sourceMesh.isReadable)
            {
                EditorUtility.DisplayDialog("Generic Gore",
                    "The source mesh is not readable. Enable Read/Write on the FBX or mesh import settings first.",
                    "OK");
                return;
            }

            int[] vertexGroups = BuildVertexGroupLookup(sourceRenderer, profile);
            Mesh[] groupMeshes = PartitionMesh(sourceMesh, vertexGroups, profile.groups.Count, profile.fallbackGroup);

            Transform rootBone = sourceRenderer.rootBone != null ? sourceRenderer.rootBone : sourceRenderer.transform;

            Transform existingRig = characterRoot.transform.Find("GenericGoreRig");
            if (existingRig != null)
                DestroyImmediate(existingRig.gameObject);

            GameObject rigRoot = new GameObject("GenericGoreRig");
            Undo.RegisterCreatedObjectUndo(rigRoot, "Create Generic Gore Rig");
            rigRoot.transform.SetParent(characterRoot.transform, false);

            Material[] partMaterials = BuildPartMaterials(sourceRenderer.sharedMaterials, profile.capMaterial);
            var parts = new List<GoreRuntimePart>();

            for (int i = 0; i < groupMeshes.Length; i++)
            {
                if (groupMeshes[i] == null || groupMeshes[i].vertexCount == 0)
                    continue;

                string meshPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{characterRoot.name}_{profile.groups[i].name}.asset");
                AssetDatabase.CreateAsset(groupMeshes[i], meshPath);

                GameObject partObject = new GameObject(profile.groups[i].name);
                Undo.RegisterCreatedObjectUndo(partObject, "Create Gore Part");
                partObject.transform.SetParent(rigRoot.transform, false);

                SkinnedMeshRenderer partRenderer = partObject.AddComponent<SkinnedMeshRenderer>();
                CopyRendererSettings(sourceRenderer, partRenderer);
                partRenderer.sharedMesh = groupMeshes[i];
                partRenderer.rootBone = rootBone;
                partRenderer.bones = sourceRenderer.bones;
                partRenderer.sharedMaterials = partMaterials;
                partRenderer.updateWhenOffscreen = sourceRenderer.updateWhenOffscreen;
                partRenderer.quality = sourceRenderer.quality;

                Transform pivot = ResolvePath(rootBone, profile.groups[i].pivotBonePath);
                if (pivot == null)
                    pivot = rootBone;

                parts.Add(new GoreRuntimePart
                {
                    name = profile.groups[i].name,
                    liveRenderer = partRenderer,
                    pivot = pivot,
                    forceMultiplier = profile.groups[i].forceMultiplier,
                    mass = Mathf.Max(0.1f, groupMeshes[i].bounds.size.magnitude)
                });
            }

            GenericGoreRig goreRig = characterRoot.GetComponent<GenericGoreRig>();
            if (goreRig == null)
                goreRig = Undo.AddComponent<GenericGoreRig>(characterRoot);

            goreRig.EditorAssign(characterRoot.GetComponentInChildren<Animator>(), sourceRenderer, profile, parts);

            sourceRenderer.enabled = false;
            EditorUtility.SetDirty(characterRoot);
            EditorUtility.SetDirty(goreRig);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(rigRoot);
        }

        private bool ValidateInputs()
        {
            if (characterRoot == null || sourceRenderer == null || profile == null || outputFolder == null)
            {
                EditorUtility.DisplayDialog("Generic Gore", "Character Root, Source Renderer, Profile and Output Folder are required.", "OK");
                return false;
            }

            if (profile.groups == null || profile.groups.Count == 0)
            {
                EditorUtility.DisplayDialog("Generic Gore", "Profile has no groups. Auto create one or add groups manually.", "OK");
                return false;
            }

            return true;
        }

        private static Material[] BuildPartMaterials(Material[] sourceMaterials, Material capMaterial)
        {
            Material fallback = null;
            if (sourceMaterials != null && sourceMaterials.Length > 0)
                fallback = sourceMaterials[sourceMaterials.Length - 1];

            Material[] result = new Material[(sourceMaterials?.Length ?? 0) + 1];
            if (sourceMaterials != null && sourceMaterials.Length > 0)
                Array.Copy(sourceMaterials, result, sourceMaterials.Length);

            result[result.Length - 1] = capMaterial != null ? capMaterial : fallback;
            return result;
        }

        private static void CopyRendererSettings(SkinnedMeshRenderer source, SkinnedMeshRenderer destination)
        {
            destination.shadowCastingMode = source.shadowCastingMode;
            destination.receiveShadows = source.receiveShadows;
            destination.skinnedMotionVectors = source.skinnedMotionVectors;
            destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            destination.motionVectorGenerationMode = source.motionVectorGenerationMode;
            destination.lightProbeUsage = source.lightProbeUsage;
            destination.reflectionProbeUsage = source.reflectionProbeUsage;
            destination.probeAnchor = source.probeAnchor;
            destination.localBounds = source.localBounds;
        }

        private static int[] BuildVertexGroupLookup(SkinnedMeshRenderer renderer, GenericGoreProfile goreProfile)
        {
            Mesh mesh = renderer.sharedMesh;
            BoneWeight[] boneWeights = mesh.boneWeights;
            Transform[] bones = renderer.bones;
            Transform rootBone = renderer.rootBone != null ? renderer.rootBone : renderer.transform;

            int[] boneToGroup = new int[bones.Length];
            for (int i = 0; i < boneToGroup.Length; i++)
                boneToGroup[i] = -1;

            for (int groupIndex = 0; groupIndex < goreProfile.groups.Count; groupIndex++)
            {
                foreach (string bonePath in goreProfile.groups[groupIndex].bonePaths)
                {
                    Transform bone = ResolvePath(rootBone, bonePath);
                    if (bone == null)
                        continue;

                    for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
                    {
                        if (bones[boneIndex] == bone)
                            boneToGroup[boneIndex] = groupIndex;
                    }
                }
            }

            int[] vertexGroups = new int[boneWeights.Length];
            for (int vertexIndex = 0; vertexIndex < boneWeights.Length; vertexIndex++)
            {
                BoneWeight bw = boneWeights[vertexIndex];
                int bestGroup = -1;
                float bestWeight = -1f;

                Consider(bw.boneIndex0, bw.weight0);
                Consider(bw.boneIndex1, bw.weight1);
                Consider(bw.boneIndex2, bw.weight2);
                Consider(bw.boneIndex3, bw.weight3);

                vertexGroups[vertexIndex] = bestGroup >= 0 ? bestGroup : Mathf.Clamp(goreProfile.fallbackGroup, 0, goreProfile.groups.Count - 1);

                void Consider(int boneIndex, float weight)
                {
                    if (boneIndex < 0 || boneIndex >= boneToGroup.Length || weight <= bestWeight)
                        return;

                    int group = boneToGroup[boneIndex];
                    if (group < 0)
                        return;

                    bestGroup = group;
                    bestWeight = weight;
                }
            }

            return vertexGroups;
        }

        private static Mesh[] PartitionMesh(Mesh sourceMesh, int[] vertexGroups, int groupCount, int fallbackGroup)
        {
            if (groupCount <= 0)
                return Array.Empty<Mesh>();

            Vector3[] vertices = sourceMesh.vertices;
            Vector3[] normals = sourceMesh.normals;
            Vector4[] tangents = sourceMesh.tangents;
            Vector2[] uv = sourceMesh.uv;
            Vector2[] uv2 = sourceMesh.uv2;
            Color[] colors = sourceMesh.colors;
            BoneWeight[] boneWeights = sourceMesh.boneWeights;
            Matrix4x4[] bindposes = sourceMesh.bindposes;
            int subMeshCount = sourceMesh.subMeshCount;
            int capSubMeshIndex = subMeshCount;

            PartBuilder[] builders = new PartBuilder[groupCount];
            for (int i = 0; i < groupCount; i++)
                builders[i] = new PartBuilder(subMeshCount + 1, vertices, normals, tangents, uv, uv2, colors, boneWeights);

            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                int[] triangles = sourceMesh.GetTriangles(subMeshIndex);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int a = triangles[i];
                    int b = triangles[i + 1];
                    int c = triangles[i + 2];

                    int ga = SafeGroup(vertexGroups[a], groupCount, fallbackGroup);
                    int gb = SafeGroup(vertexGroups[b], groupCount, fallbackGroup);
                    int gc = SafeGroup(vertexGroups[c], groupCount, fallbackGroup);

                    ClipTriangleForGroup(builders[ga], subMeshIndex, a, b, c, ga, vertexGroups);
                    if (gb != ga)
                        ClipTriangleForGroup(builders[gb], subMeshIndex, a, b, c, gb, vertexGroups);
                    if (gc != ga && gc != gb)
                        ClipTriangleForGroup(builders[gc], subMeshIndex, a, b, c, gc, vertexGroups);
                }
            }

            for (int i = 0; i < groupCount; i++)
                builders[i].BuildCaps(capSubMeshIndex);

            Mesh[] result = new Mesh[groupCount];
            for (int i = 0; i < groupCount; i++)
                result[i] = builders[i].ToMesh($"Part_{i}", bindposes);

            return result;
        }

        private static void ClipTriangleForGroup(PartBuilder builder,
                                                 int subMeshIndex,
                                                 int a,
                                                 int b,
                                                 int c,
                                                 int targetGroup,
                                                 int[] vertexGroups)
        {
            SourceVertexRef[] source =
            {
                new SourceVertexRef(a),
                new SourceVertexRef(b),
                new SourceVertexRef(c)
            };

            var polygon = new List<VertexRef>(4);
            var intersections = new List<int>(2);
            SourceVertexRef previous = source[2];
            bool previousInside = vertexGroups[previous.sourceIndex] == targetGroup;

            for (int i = 0; i < 3; i++)
            {
                SourceVertexRef current = source[i];
                bool currentInside = vertexGroups[current.sourceIndex] == targetGroup;

                if (previousInside && currentInside)
                {
                    polygon.Add(VertexRef.FromSource(current.sourceIndex));
                }
                else if (previousInside && !currentInside)
                {
                    int cut = builder.GetOrCreateCutVertex(previous.sourceIndex, current.sourceIndex, targetGroup);
                    polygon.Add(VertexRef.FromLocal(cut));
                    intersections.Add(cut);
                }
                else if (!previousInside && currentInside)
                {
                    int cut = builder.GetOrCreateCutVertex(previous.sourceIndex, current.sourceIndex, targetGroup);
                    polygon.Add(VertexRef.FromLocal(cut));
                    polygon.Add(VertexRef.FromSource(current.sourceIndex));
                    intersections.Add(cut);
                }

                previous = current;
                previousInside = currentInside;
            }

            if (polygon.Count >= 3)
                builder.AddPolygon(subMeshIndex, polygon);

            if (intersections.Count == 2 && intersections[0] != intersections[1])
                builder.AddCutSegment(intersections[0], intersections[1]);
        }

        private static int SafeGroup(int value, int groupCount, int fallbackGroup)
        {
            if (value >= 0 && value < groupCount)
                return value;
            return Mathf.Clamp(fallbackGroup, 0, groupCount - 1);
        }

        private static List<string> CollectPaths(Transform root, Transform branchRoot)
        {
            var paths = new List<string>();
            AddRecursive(branchRoot);
            return paths;

            void AddRecursive(Transform current)
            {
                paths.Add(GetPath(root, current));
                foreach (Transform child in current)
                    AddRecursive(child);
            }
        }

        private static string GetPath(Transform root, Transform target)
        {
            if (target == root)
                return string.Empty;

            List<string> names = new List<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static Transform ResolvePath(Transform root, string path)
        {
            if (root == null)
                return null;

            if (string.IsNullOrWhiteSpace(path))
                return root;

            return root.Find(path);
        }

        private readonly struct SourceVertexRef
        {
            public readonly int sourceIndex;

            public SourceVertexRef(int sourceIndex)
            {
                this.sourceIndex = sourceIndex;
            }
        }

        private readonly struct VertexRef
        {
            public readonly bool isSource;
            public readonly int index;

            private VertexRef(bool isSource, int index)
            {
                this.isSource = isSource;
                this.index = index;
            }

            public static VertexRef FromSource(int sourceIndex) => new VertexRef(true, sourceIndex);
            public static VertexRef FromLocal(int localIndex) => new VertexRef(false, localIndex);
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            public readonly int a;
            public readonly int b;

            public EdgeKey(int first, int second)
            {
                if (first < second)
                {
                    a = first;
                    b = second;
                }
                else
                {
                    a = second;
                    b = first;
                }
            }

            public bool Equals(EdgeKey other) => a == other.a && b == other.b;
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode() => unchecked((a * 397) ^ b);
        }

        private readonly struct EdgeGroupKey : IEquatable<EdgeGroupKey>
        {
            public readonly int a;
            public readonly int b;
            public readonly int group;

            public EdgeGroupKey(int first, int second, int group)
            {
                if (first < second)
                {
                    a = first;
                    b = second;
                }
                else
                {
                    a = second;
                    b = first;
                }

                this.group = group;
            }

            public bool Equals(EdgeGroupKey other) => a == other.a && b == other.b && group == other.group;
            public override bool Equals(object obj) => obj is EdgeGroupKey other && Equals(other);
            public override int GetHashCode() => unchecked(((a * 397) ^ b) * 397 ^ group);
        }

        private readonly struct SegmentKey : IEquatable<SegmentKey>
        {
            public readonly int a;
            public readonly int b;

            public SegmentKey(int first, int second)
            {
                if (first < second)
                {
                    a = first;
                    b = second;
                }
                else
                {
                    a = second;
                    b = first;
                }
            }

            public bool Equals(SegmentKey other) => a == other.a && b == other.b;
            public override bool Equals(object obj) => obj is SegmentKey other && Equals(other);
            public override int GetHashCode() => unchecked((a * 397) ^ b);
        }

        private sealed class PartBuilder
        {
            private readonly Vector3[] sourceVertices;
            private readonly Vector3[] sourceNormals;
            private readonly Vector4[] sourceTangents;
            private readonly Vector2[] sourceUv;
            private readonly Vector2[] sourceUv2;
            private readonly Color[] sourceColors;
            private readonly BoneWeight[] sourceBoneWeights;

            private readonly Dictionary<int, int> sourceRemap = new();
            private readonly Dictionary<EdgeGroupKey, int> cutVertexRemap = new();
            private readonly HashSet<int> cutVertexIndices = new();
            private readonly HashSet<SegmentKey> cutSegments = new();
            private readonly List<Vector3> vertices = new();
            private readonly List<Vector3> normals = new();
            private readonly List<Vector4> tangents = new();
            private readonly List<Vector2> uv = new();
            private readonly List<Vector2> uv2 = new();
            private readonly List<Color> colors = new();
            private readonly List<BoneWeight> boneWeights = new();
            private readonly List<int>[] triangles;

            public PartBuilder(int subMeshCount,
                               Vector3[] sourceVertices,
                               Vector3[] sourceNormals,
                               Vector4[] sourceTangents,
                               Vector2[] sourceUv,
                               Vector2[] sourceUv2,
                               Color[] sourceColors,
                               BoneWeight[] sourceBoneWeights)
            {
                this.sourceVertices = sourceVertices;
                this.sourceNormals = sourceNormals;
                this.sourceTangents = sourceTangents;
                this.sourceUv = sourceUv;
                this.sourceUv2 = sourceUv2;
                this.sourceColors = sourceColors;
                this.sourceBoneWeights = sourceBoneWeights;

                triangles = new List<int>[subMeshCount];
                for (int i = 0; i < subMeshCount; i++)
                    triangles[i] = new List<int>();
            }

            public void AddPolygon(int subMeshIndex, List<VertexRef> polygon)
            {
                if (polygon == null || polygon.Count < 3)
                    return;

                int first = ResolveVertex(polygon[0]);
                for (int i = 1; i < polygon.Count - 1; i++)
                {
                    int second = ResolveVertex(polygon[i]);
                    int third = ResolveVertex(polygon[i + 1]);
                    triangles[subMeshIndex].Add(first);
                    triangles[subMeshIndex].Add(second);
                    triangles[subMeshIndex].Add(third);
                }
            }

            public int GetOrCreateCutVertex(int sourceA, int sourceB, int targetGroup)
            {
                EdgeGroupKey key = new EdgeGroupKey(sourceA, sourceB, targetGroup);
                if (cutVertexRemap.TryGetValue(key, out int existing))
                    return existing;

                int index = vertices.Count;
                cutVertexRemap[key] = index;
                cutVertexIndices.Add(index);

                float t = 0.5f;
                vertices.Add(Vector3.Lerp(sourceVertices[sourceA], sourceVertices[sourceB], t));

                Vector3 normal = SafeNormalize(Vector3.Lerp(GetNormal(sourceA), GetNormal(sourceB), t));
                if (normal.sqrMagnitude < 0.000001f)
                    normal = Vector3.up;
                normals.Add(normal);

                Vector4 tangent = Vector4.Lerp(GetTangent(sourceA), GetTangent(sourceB), t);
                if (new Vector3(tangent.x, tangent.y, tangent.z).sqrMagnitude < 0.000001f)
                    tangent = new Vector4(1f, 0f, 0f, 1f);
                tangents.Add(tangent);

                uv.Add(Vector2.Lerp(GetUv(sourceA), GetUv(sourceB), t));
                uv2.Add(Vector2.Lerp(GetUv2(sourceA), GetUv2(sourceB), t));
                colors.Add(Color.Lerp(GetColor(sourceA), GetColor(sourceB), t));
                boneWeights.Add(BlendBoneWeights(GetBoneWeight(sourceA), GetBoneWeight(sourceB), t));
                return index;
            }

            public void AddCutSegment(int a, int b)
            {
                if (a == b)
                    return;

                cutSegments.Add(new SegmentKey(a, b));
            }

            public void BuildCaps(int subMeshIndex)
            {
                List<List<int>> loops = BuildLoopsFromSegments(cutSegments);

                if (loops.Count == 0)
                    loops = BuildBoundaryLoops(subMeshIndex, true);

                if (loops.Count == 0)
                    loops = BuildBoundaryLoops(subMeshIndex, false);

                foreach (List<int> loop in loops)
                    AddCapLoop(loop, subMeshIndex);
            }

            public Mesh ToMesh(string meshName, Matrix4x4[] bindposes)
            {
                if (vertices.Count == 0)
                    return null;

                Mesh mesh = new Mesh { name = meshName };
                if (vertices.Count > 65535)
                    mesh.indexFormat = IndexFormat.UInt32;

                mesh.SetVertices(vertices);
                if (normals.Count == vertices.Count)
                    mesh.SetNormals(normals);
                if (tangents.Count == vertices.Count)
                    mesh.SetTangents(tangents);
                if (uv.Count == vertices.Count)
                    mesh.SetUVs(0, uv);
                if (uv2.Count == vertices.Count)
                    mesh.SetUVs(1, uv2);
                if (colors.Count == vertices.Count)
                    mesh.SetColors(colors);

                mesh.boneWeights = boneWeights.ToArray();
                mesh.bindposes = bindposes;
                mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < triangles.Length; i++)
                    mesh.SetTriangles(triangles[i], i);

                if (normals.Count != vertices.Count)
                    mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }

            private int ResolveVertex(VertexRef vertex)
            {
                return vertex.isSource ? GetOrCreateSource(vertex.index) : vertex.index;
            }

            private int GetOrCreateSource(int sourceIndex)
            {
                if (sourceRemap.TryGetValue(sourceIndex, out int destinationIndex))
                    return destinationIndex;

                destinationIndex = vertices.Count;
                sourceRemap[sourceIndex] = destinationIndex;

                vertices.Add(sourceVertices[sourceIndex]);
                normals.Add(GetNormal(sourceIndex));
                tangents.Add(GetTangent(sourceIndex));
                uv.Add(GetUv(sourceIndex));
                uv2.Add(GetUv2(sourceIndex));
                colors.Add(GetColor(sourceIndex));
                boneWeights.Add(GetBoneWeight(sourceIndex));
                return destinationIndex;
            }

            private List<List<int>> BuildLoopsFromSegments(IEnumerable<SegmentKey> segments)
            {
                var adjacency = new Dictionary<int, List<int>>();
                var segmentList = new List<SegmentKey>();

                foreach (SegmentKey segment in segments)
                {
                    segmentList.Add(segment);
                    AddAdjacency(segment.a, segment.b);
                    AddAdjacency(segment.b, segment.a);
                }

                var loops = new List<List<int>>();
                var visited = new HashSet<SegmentKey>();
                foreach (SegmentKey segment in segmentList)
                {
                    if (visited.Contains(segment))
                        continue;

                    List<int> loop = TraceLoop(segment.a, segment.b, adjacency, visited);
                    if (loop != null && loop.Count >= 3)
                        loops.Add(loop);
                }

                return loops;

                void AddAdjacency(int a, int b)
                {
                    if (!adjacency.TryGetValue(a, out List<int> neighbours))
                    {
                        neighbours = new List<int>(2);
                        adjacency.Add(a, neighbours);
                    }

                    if (!neighbours.Contains(b))
                        neighbours.Add(b);
                }
            }

            private List<List<int>> BuildBoundaryLoops(int capSubMeshIndex, bool cutVerticesOnly)
            {
                var edgeCounts = new Dictionary<EdgeKey, int>();
                var orientedBoundary = new Dictionary<EdgeKey, (int from, int to)>();

                int shellSubMeshCount = Mathf.Min(capSubMeshIndex, triangles.Length);
                for (int subMeshIndex = 0; subMeshIndex < shellSubMeshCount; subMeshIndex++)
                {
                    List<int> subMeshTriangles = triangles[subMeshIndex];
                    for (int i = 0; i < subMeshTriangles.Count; i += 3)
                    {
                        int a = subMeshTriangles[i];
                        int b = subMeshTriangles[i + 1];
                        int c = subMeshTriangles[i + 2];
                        CountEdge(a, b);
                        CountEdge(b, c);
                        CountEdge(c, a);
                    }
                }

                var boundarySegments = new List<SegmentKey>();
                foreach (var pair in orientedBoundary)
                {
                    if (!edgeCounts.TryGetValue(pair.Key, out int count) || count != 1)
                        continue;

                    int a = pair.Value.from;
                    int b = pair.Value.to;
                    if (cutVerticesOnly && !(cutVertexIndices.Contains(a) && cutVertexIndices.Contains(b)))
                        continue;

                    boundarySegments.Add(new SegmentKey(a, b));
                }

                return BuildLoopsFromSegments(boundarySegments);

                void CountEdge(int from, int to)
                {
                    EdgeKey key = new EdgeKey(from, to);
                    edgeCounts.TryGetValue(key, out int count);
                    edgeCounts[key] = count + 1;
                    if (count == 0)
                        orientedBoundary[key] = (from, to);
                }
            }

            private static List<int> TraceLoop(int start,
                                               int second,
                                               Dictionary<int, List<int>> adjacency,
                                               HashSet<SegmentKey> visited)
            {
                var loop = new List<int> { start };
                int previous = start;
                int current = second;
                visited.Add(new SegmentKey(start, second));

                int guard = 0;
                while (guard++ < 10000)
                {
                    loop.Add(current);
                    if (current == start)
                        break;

                    if (!adjacency.TryGetValue(current, out List<int> neighbours) || neighbours.Count == 0)
                        return null;

                    int next = -1;
                    for (int i = 0; i < neighbours.Count; i++)
                    {
                        int candidate = neighbours[i];
                        if (candidate == previous && neighbours.Count > 1)
                            continue;

                        SegmentKey key = new SegmentKey(current, candidate);
                        if (visited.Contains(key) && candidate != start)
                            continue;

                        next = candidate;
                        break;
                    }

                    if (next < 0)
                    {
                        for (int i = 0; i < neighbours.Count; i++)
                        {
                            if (neighbours[i] == start)
                            {
                                next = start;
                                break;
                            }
                        }
                    }

                    if (next < 0)
                        return null;

                    visited.Add(new SegmentKey(current, next));
                    previous = current;
                    current = next;
                }

                if (loop.Count > 1 && loop[loop.Count - 1] == start)
                    loop.RemoveAt(loop.Count - 1);

                return loop.Count >= 3 ? loop : null;
            }

            private void AddCapLoop(List<int> loop, int subMeshIndex)
            {
                if (loop == null || loop.Count < 3)
                    return;

                Vector3 loopCenter = Vector3.zero;
                for (int i = 0; i < loop.Count; i++)
                    loopCenter += vertices[loop[i]];
                loopCenter /= loop.Count;

                Vector3 partCenter = GetCurrentPartCenter();
                Vector3 outwardNormal = SafeNormalize(loopCenter - partCenter);
                if (outwardNormal.sqrMagnitude < 0.000001f)
                    outwardNormal = EstimateLoopNormal(loop);
                if (outwardNormal.sqrMagnitude < 0.000001f)
                    outwardNormal = Vector3.up;

                Vector3 geometricNormal = EstimateLoopNormal(loop);
                if (geometricNormal.sqrMagnitude > 0.000001f && Vector3.Dot(geometricNormal, outwardNormal) < 0f)
                    loop.Reverse();

                Vector3 basisU = Vector3.Cross(outwardNormal, Mathf.Abs(Vector3.Dot(outwardNormal, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up).normalized;
                if (basisU.sqrMagnitude < 0.000001f)
                    basisU = Vector3.right;
                Vector3 basisV = Vector3.Cross(outwardNormal, basisU).normalized;

                Vector2[] projected = new Vector2[loop.Count];
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                for (int i = 0; i < loop.Count; i++)
                {
                    Vector3 delta = vertices[loop[i]] - loopCenter;
                    Vector2 p = new Vector2(Vector3.Dot(delta, basisU), Vector3.Dot(delta, basisV));
                    projected[i] = p;
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }

                Vector2 size = max - min;
                if (size.x < 0.0001f) size.x = 1f;
                if (size.y < 0.0001f) size.y = 1f;

                Vector4 tangent = new Vector4(basisU.x, basisU.y, basisU.z, 1f);
                var capVertices = new List<int>(loop.Count);
                for (int i = 0; i < loop.Count; i++)
                {
                    int boundary = loop[i];
                    Vector2 capUv = new Vector2((projected[i].x - min.x) / size.x, (projected[i].y - min.y) / size.y);
                    capVertices.Add(AddCustomVertex(
                        vertices[boundary],
                        outwardNormal,
                        tangent,
                        capUv,
                        Vector2.zero,
                        colors.Count > boundary ? colors[boundary] : Color.white,
                        boneWeights.Count > boundary ? boneWeights[boundary] : new BoneWeight { boneIndex0 = 0, weight0 = 1f }));
                }

                int centerVertex = AddCustomVertex(
                    loopCenter,
                    outwardNormal,
                    tangent,
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    AverageColor(loop),
                    AverageBoneWeight(loop));

                for (int i = 0; i < capVertices.Count; i++)
                {
                    int next = (i + 1) % capVertices.Count;
                    triangles[subMeshIndex].Add(centerVertex);
                    triangles[subMeshIndex].Add(capVertices[i]);
                    triangles[subMeshIndex].Add(capVertices[next]);
                }
            }

            private int AddCustomVertex(Vector3 position, Vector3 normal, Vector4 tangent, Vector2 uv0, Vector2 uv1, Color color, BoneWeight weight)
            {
                int index = vertices.Count;
                vertices.Add(position);
                normals.Add(normal);
                tangents.Add(tangent);
                uv.Add(uv0);
                uv2.Add(uv1);
                colors.Add(color);
                boneWeights.Add(weight);
                return index;
            }

            private Vector3 GetCurrentPartCenter()
            {
                if (vertices.Count == 0)
                    return Vector3.zero;

                Vector3 sum = Vector3.zero;
                for (int i = 0; i < vertices.Count; i++)
                    sum += vertices[i];
                return sum / vertices.Count;
            }

            private Vector3 EstimateLoopNormal(List<int> loop)
            {
                Vector3 normal = Vector3.zero;
                for (int i = 0; i < loop.Count; i++)
                {
                    Vector3 current = vertices[loop[i]];
                    Vector3 next = vertices[loop[(i + 1) % loop.Count]];
                    normal.x += (current.y - next.y) * (current.z + next.z);
                    normal.y += (current.z - next.z) * (current.x + next.x);
                    normal.z += (current.x - next.x) * (current.y + next.y);
                }

                return SafeNormalize(normal);
            }

            private Vector3 GetNormal(int sourceIndex)
            {
                if (sourceNormals != null && sourceNormals.Length == sourceVertices.Length)
                    return sourceNormals[sourceIndex];
                return Vector3.up;
            }

            private Vector4 GetTangent(int sourceIndex)
            {
                if (sourceTangents != null && sourceTangents.Length == sourceVertices.Length)
                    return sourceTangents[sourceIndex];
                return new Vector4(1f, 0f, 0f, 1f);
            }

            private Vector2 GetUv(int sourceIndex)
            {
                if (sourceUv != null && sourceUv.Length == sourceVertices.Length)
                    return sourceUv[sourceIndex];
                return Vector2.zero;
            }

            private Vector2 GetUv2(int sourceIndex)
            {
                if (sourceUv2 != null && sourceUv2.Length == sourceVertices.Length)
                    return sourceUv2[sourceIndex];
                return Vector2.zero;
            }

            private Color GetColor(int sourceIndex)
            {
                if (sourceColors != null && sourceColors.Length == sourceVertices.Length)
                    return sourceColors[sourceIndex];
                return Color.white;
            }

            private BoneWeight GetBoneWeight(int sourceIndex)
            {
                if (sourceBoneWeights != null && sourceBoneWeights.Length == sourceVertices.Length)
                    return sourceBoneWeights[sourceIndex];
                return new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            }

            private static Vector3 SafeNormalize(Vector3 value)
            {
                if (value.sqrMagnitude < 0.000001f)
                    return Vector3.zero;
                return value.normalized;
            }

            private Color AverageColor(List<int> loop)
            {
                if (loop == null || loop.Count == 0)
                    return Color.white;

                Color sum = new Color(0f, 0f, 0f, 0f);
                for (int i = 0; i < loop.Count; i++)
                    sum += colors.Count > loop[i] ? colors[loop[i]] : Color.white;
                return sum / loop.Count;
            }

            private BoneWeight AverageBoneWeight(List<int> loop)
            {
                if (loop == null || loop.Count == 0)
                    return new BoneWeight { boneIndex0 = 0, weight0 = 1f };

                Dictionary<int, float> weights = new Dictionary<int, float>();
                for (int i = 0; i < loop.Count; i++)
                {
                    BoneWeight weight = boneWeights.Count > loop[i] ? boneWeights[loop[i]] : new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                    Accumulate(weight.boneIndex0, weight.weight0);
                    Accumulate(weight.boneIndex1, weight.weight1);
                    Accumulate(weight.boneIndex2, weight.weight2);
                    Accumulate(weight.boneIndex3, weight.weight3);
                }

                var sorted = new List<KeyValuePair<int, float>>(weights);
                sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
                float total = 0f;
                for (int i = 0; i < sorted.Count && i < 4; i++)
                    total += sorted[i].Value;
                if (total <= 0.000001f)
                    total = 1f;

                BoneWeight result = new BoneWeight();
                for (int i = 0; i < sorted.Count && i < 4; i++)
                {
                    float normalized = sorted[i].Value / total;
                    switch (i)
                    {
                        case 0:
                            result.boneIndex0 = sorted[i].Key;
                            result.weight0 = normalized;
                            break;
                        case 1:
                            result.boneIndex1 = sorted[i].Key;
                            result.weight1 = normalized;
                            break;
                        case 2:
                            result.boneIndex2 = sorted[i].Key;
                            result.weight2 = normalized;
                            break;
                        case 3:
                            result.boneIndex3 = sorted[i].Key;
                            result.weight3 = normalized;
                            break;
                    }
                }

                return result;

                void Accumulate(int boneIndex, float value)
                {
                    if (value <= 0f)
                        return;

                    if (weights.TryGetValue(boneIndex, out float existing))
                        weights[boneIndex] = existing + value;
                    else
                        weights.Add(boneIndex, value);
                }
            }

            private static BoneWeight BlendBoneWeights(BoneWeight a, BoneWeight b, float t)
            {
                Dictionary<int, float> weights = new Dictionary<int, float>();
                Accumulate(a.boneIndex0, a.weight0 * (1f - t));
                Accumulate(a.boneIndex1, a.weight1 * (1f - t));
                Accumulate(a.boneIndex2, a.weight2 * (1f - t));
                Accumulate(a.boneIndex3, a.weight3 * (1f - t));
                Accumulate(b.boneIndex0, b.weight0 * t);
                Accumulate(b.boneIndex1, b.weight1 * t);
                Accumulate(b.boneIndex2, b.weight2 * t);
                Accumulate(b.boneIndex3, b.weight3 * t);

                var sorted = new List<KeyValuePair<int, float>>(weights);
                sorted.Sort((x, y) => y.Value.CompareTo(x.Value));
                float total = 0f;
                for (int i = 0; i < sorted.Count && i < 4; i++)
                    total += sorted[i].Value;
                if (total <= 0.000001f)
                    total = 1f;

                BoneWeight result = new BoneWeight();
                for (int i = 0; i < sorted.Count && i < 4; i++)
                {
                    float normalized = sorted[i].Value / total;
                    switch (i)
                    {
                        case 0:
                            result.boneIndex0 = sorted[i].Key;
                            result.weight0 = normalized;
                            break;
                        case 1:
                            result.boneIndex1 = sorted[i].Key;
                            result.weight1 = normalized;
                            break;
                        case 2:
                            result.boneIndex2 = sorted[i].Key;
                            result.weight2 = normalized;
                            break;
                        case 3:
                            result.boneIndex3 = sorted[i].Key;
                            result.weight3 = normalized;
                            break;
                    }
                }

                return result;

                void Accumulate(int boneIndex, float value)
                {
                    if (value <= 0f)
                        return;

                    if (weights.TryGetValue(boneIndex, out float existing))
                        weights[boneIndex] = existing + value;
                    else
                        weights.Add(boneIndex, value);
                }
            }
        }
    }
}
