#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ANSTUDIO.EditorTools
{
    /// <summary>
    /// Build-time shader variant stripper for Unity 6000.3.x / URP projects.
    /// Place this file anywhere under an Editor folder, for example:
    /// Assets/Editor/ANSTUDIO/ShaderVariants/AN_ShaderVariantStripper.cs
    ///
    /// Use menu: Tools/ANSTUDIO/Shader Variants/Settings
    /// Then enable the stripper and choose which groups you do NOT use.
    /// </summary>
    public sealed class ANShaderVariantStripperSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/ANSTUDIO/Scripts/ShaderVariantStripper/Editor/ShaderVariants/AN_ShaderVariantStripperSettings.asset";

        [Header("Master")]
        [Tooltip("If disabled, this script only reports nothing and strips nothing.")]
        public bool enabled = true;

        [Tooltip("Recommended ON while testing. This avoids stripping variants from Development builds.")]
        public bool disableForDevelopmentBuilds = false;

        [Tooltip("Write a compact summary to the Console after the build finishes compiling shader snippets.")]
        public bool logSummary = true;

        [Header("Usually safe for non-XR desktop ARPG projects")]
        public bool stripXRAndStereo = true;
        public bool stripDebugDisplay = true;
        public bool stripEditorVisualization = true;

        [Header("Lighting / shadows - disable only if your project does not use them")]
        public bool stripAdditionalLights = false;
        public bool stripAdditionalLightShadows = false;
        public bool stripMainLightShadows = false;
        public bool stripSoftShadows = false;
        public bool stripLightCookies = false;
        public bool stripLightLayers = true;

        [Header("URP Renderer Features - disable only if removed from all Universal Renderers")]
        public bool stripScreenSpaceOcclusion = false;
        public bool stripDecals = false;
        public bool stripRenderPassEnabled = false;
        public bool stripRenderingLayers = true;

        [Header("Probes / GI / fog")]
        public bool stripReflectionProbeBlending = false;
        public bool stripReflectionProbeBoxProjection = false;
        public bool stripLightmaps = false;
        public bool stripFog = false;

        [Header("Instancing")]
        public bool stripGpuInstancing = false;
        public bool stripDotsInstancing = true;
        public bool stripProceduralInstancing = true;

        [Header("Pass stripping - powerful, test carefully")]
        [Tooltip("Strips Meta pass variants. Enable only if you do not bake lighting and do not need meta/lightmap export passes.")]
        public bool stripMetaPass = false;

        [Tooltip("Strips MotionVectors pass variants. Enable only if you do not use TAA, motion blur or effects requiring motion vectors.")]
        public bool stripMotionVectorsPass = true;

        [Tooltip("Strips ShadowCaster pass variants. Enable only if nothing using these shaders casts shadows.")]
        public bool stripShadowCasterPass = false;

        [Header("Custom keyword stripping")]
        [Tooltip("Any variant containing one of these keywords will be stripped. Use for project-specific debug/unused keywords.")]
        public List<string> customStripKeywords = new List<string>
        {
            "DEBUG_DISPLAY",
            "SCENESELECTIONPASS",
            "PICKINGPASS"
        };
    }

    public sealed class ANShaderVariantStripper : IPreprocessShaders
    {
        public int callbackOrder => -1000;

        private static int _totalBefore;
        private static int _totalAfter;
        private static int _totalStripped;
        private static readonly Dictionary<string, int> _strippedByReason = new Dictionary<string, int>();

        private static readonly string[] XRKeywords =
        {
            "STEREO_INSTANCING_ON",
            "STEREO_MULTIVIEW_ON",
            "STEREO_CUBEMAP_RENDER_ON",
            "UNITY_SINGLE_PASS_STEREO"
        };

        private static readonly string[] DebugKeywords =
        {
            "DEBUG_DISPLAY",
            "DEBUG_DISPLAY_GLOBAL"
        };

        private static readonly string[] EditorVisualizationKeywords =
        {
            "SCENESELECTIONPASS",
            "PICKINGPASS"
        };

        private static readonly string[] AdditionalLightsKeywords =
        {
            "_ADDITIONAL_LIGHTS",
            "_ADDITIONAL_LIGHTS_VERTEX"
        };

        private static readonly string[] AdditionalLightShadowKeywords =
        {
            "_ADDITIONAL_LIGHT_SHADOWS"
        };

        private static readonly string[] MainLightShadowKeywords =
        {
            "_MAIN_LIGHT_SHADOWS",
            "_MAIN_LIGHT_SHADOWS_CASCADE",
            "_MAIN_LIGHT_SHADOWS_SCREEN"
        };

        private static readonly string[] SoftShadowKeywords =
        {
            "_SHADOWS_SOFT",
            "_SHADOWS_SOFT_LOW",
            "_SHADOWS_SOFT_MEDIUM",
            "_SHADOWS_SOFT_HIGH"
        };

        private static readonly string[] LightCookieKeywords =
        {
            "_LIGHT_COOKIES"
        };

        private static readonly string[] LightLayerKeywords =
        {
            "_LIGHT_LAYERS"
        };

        private static readonly string[] ScreenSpaceOcclusionKeywords =
        {
            "_SCREEN_SPACE_OCCLUSION"
        };

        private static readonly string[] DecalKeywords =
        {
            "_DBUFFER_MRT1",
            "_DBUFFER_MRT2",
            "_DBUFFER_MRT3",
            "_DECAL_NORMAL_BLEND_LOW",
            "_DECAL_NORMAL_BLEND_MEDIUM",
            "_DECAL_NORMAL_BLEND_HIGH",
            "_DECAL_LAYERS"
        };

        private static readonly string[] RenderPassKeywords =
        {
            "_RENDER_PASS_ENABLED"
        };

        private static readonly string[] RenderingLayerKeywords =
        {
            "_WRITE_RENDERING_LAYERS"
        };

        private static readonly string[] ProbeBlendingKeywords =
        {
            "_REFLECTION_PROBE_BLENDING"
        };

        private static readonly string[] ProbeBoxProjectionKeywords =
        {
            "_REFLECTION_PROBE_BOX_PROJECTION"
        };

        private static readonly string[] LightmapKeywords =
        {
            "LIGHTMAP_ON",
            "DIRLIGHTMAP_COMBINED",
            "DYNAMICLIGHTMAP_ON",
            "LIGHTMAP_SHADOW_MIXING",
            "SHADOWS_SHADOWMASK"
        };

        private static readonly string[] FogKeywords =
        {
            "FOG_LINEAR",
            "FOG_EXP",
            "FOG_EXP2"
        };

        private static readonly string[] GpuInstancingKeywords =
        {
            "INSTANCING_ON"
        };

        private static readonly string[] DotsInstancingKeywords =
        {
            "DOTS_INSTANCING_ON"
        };

        private static readonly string[] ProceduralInstancingKeywords =
        {
            "PROCEDURAL_INSTANCING_ON"
        };

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            var settings = ANShaderVariantStripperUtility.GetOrCreateSettings(false);
            if (settings == null || !settings.enabled)
                return;

            if (settings.disableForDevelopmentBuilds && EditorUserBuildSettings.development)
                return;

            int before = data.Count;
            if (before == 0)
                return;

            _totalBefore += before;

            StripByPass(settings, snippet, data);
            StripByKeywords(shader, settings, data);

            int after = data.Count;
            int stripped = before - after;
            _totalAfter += after;
            _totalStripped += stripped;
        }

        private static void StripByPass(ANShaderVariantStripperSettings settings, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (data.Count == 0)
                return;

            string passName = snippet.passName.ToString();

            if (settings.stripMetaPass && (snippet.passType == PassType.Meta || passName.IndexOf("Meta", StringComparison.OrdinalIgnoreCase) >= 0))
                StripAll(data, "Pass/Meta");

            if (settings.stripMotionVectorsPass && (snippet.passType == PassType.MotionVectors || passName.IndexOf("Motion", StringComparison.OrdinalIgnoreCase) >= 0))
                StripAll(data, "Pass/MotionVectors");

            if (settings.stripShadowCasterPass && (snippet.passType == PassType.ShadowCaster || passName.IndexOf("ShadowCaster", StringComparison.OrdinalIgnoreCase) >= 0))
                StripAll(data, "Pass/ShadowCaster");
        }

        private static void StripByKeywords(Shader shader, ANShaderVariantStripperSettings settings, IList<ShaderCompilerData> data)
        {
            if (settings.stripXRAndStereo) StripVariantsWithAnyKeyword(shader, data, XRKeywords, "XR/Stereo");
            if (settings.stripDebugDisplay) StripVariantsWithAnyKeyword(shader, data, DebugKeywords, "Debug Display");
            if (settings.stripEditorVisualization) StripVariantsWithAnyKeyword(shader, data, EditorVisualizationKeywords, "Editor Visualization");

            if (settings.stripAdditionalLights) StripVariantsWithAnyKeyword(shader, data, AdditionalLightsKeywords, "URP Additional Lights");
            if (settings.stripAdditionalLightShadows) StripVariantsWithAnyKeyword(shader, data, AdditionalLightShadowKeywords, "URP Additional Light Shadows");
            if (settings.stripMainLightShadows) StripVariantsWithAnyKeyword(shader, data, MainLightShadowKeywords, "URP Main Light Shadows");
            if (settings.stripSoftShadows) StripVariantsWithAnyKeyword(shader, data, SoftShadowKeywords, "URP Soft Shadows");
            if (settings.stripLightCookies) StripVariantsWithAnyKeyword(shader, data, LightCookieKeywords, "URP Light Cookies");
            if (settings.stripLightLayers) StripVariantsWithAnyKeyword(shader, data, LightLayerKeywords, "URP Light Layers");

            if (settings.stripScreenSpaceOcclusion) StripVariantsWithAnyKeyword(shader, data, ScreenSpaceOcclusionKeywords, "URP SSAO");
            if (settings.stripDecals) StripVariantsWithAnyKeyword(shader, data, DecalKeywords, "URP Decals");
            if (settings.stripRenderPassEnabled) StripVariantsWithAnyKeyword(shader, data, RenderPassKeywords, "URP Native RenderPass");
            if (settings.stripRenderingLayers) StripVariantsWithAnyKeyword(shader, data, RenderingLayerKeywords, "URP Rendering Layers");

            if (settings.stripReflectionProbeBlending) StripVariantsWithAnyKeyword(shader, data, ProbeBlendingKeywords, "Probe Blending");
            if (settings.stripReflectionProbeBoxProjection) StripVariantsWithAnyKeyword(shader, data, ProbeBoxProjectionKeywords, "Probe Box Projection");
            if (settings.stripLightmaps) StripVariantsWithAnyKeyword(shader, data, LightmapKeywords, "Lightmaps/GI");
            if (settings.stripFog) StripVariantsWithAnyKeyword(shader, data, FogKeywords, "Fog");

            if (settings.stripGpuInstancing) StripVariantsWithAnyKeyword(shader, data, GpuInstancingKeywords, "GPU Instancing");
            if (settings.stripDotsInstancing) StripVariantsWithAnyKeyword(shader, data, DotsInstancingKeywords, "DOTS Instancing");
            if (settings.stripProceduralInstancing) StripVariantsWithAnyKeyword(shader, data, ProceduralInstancingKeywords, "Procedural Instancing");

            if (settings.customStripKeywords != null && settings.customStripKeywords.Count > 0)
                StripVariantsWithAnyKeyword(shader, data, settings.customStripKeywords, "Custom Keywords");
        }

        private static void StripVariantsWithAnyKeyword(Shader shader, IList<ShaderCompilerData> data, IEnumerable<string> keywordNames, string reason)
        {
            if (data.Count == 0)
                return;

            var keywords = new List<ShaderKeyword>();

            foreach (string keywordName in keywordNames)
            {
                if (string.IsNullOrWhiteSpace(keywordName))
                    continue;

                // Local keyword lookup for shader-local declarations.
                keywords.Add(new ShaderKeyword(shader, keywordName.Trim()));

                // Global keyword lookup for global declarations.
                keywords.Add(new ShaderKeyword(keywordName.Trim()));
            }

            for (int i = data.Count - 1; i >= 0; i--)
            {
                ShaderKeywordSet keywordSet = data[i].shaderKeywordSet;

                for (int k = 0; k < keywords.Count; k++)
                {
                    if (!keywordSet.IsEnabled(keywords[k]))
                        continue;

                    data.RemoveAt(i);
                    AddReason(reason);
                    break;
                }
            }
        }

        private static void StripAll(IList<ShaderCompilerData> data, string reason)
        {
            int count = data.Count;
            data.Clear();
            AddReason(reason, count);
        }

        private static void AddReason(string reason, int amount = 1)
        {
            if (amount <= 0)
                return;

            if (_strippedByReason.TryGetValue(reason, out int current))
                _strippedByReason[reason] = current + amount;
            else
                _strippedByReason.Add(reason, amount);
        }

        internal static void ResetCountersForBuild()
        {
            _totalBefore = 0;
            _totalAfter = 0;
            _totalStripped = 0;
            _strippedByReason.Clear();
        }

        internal static void LogSummaryForBuild()
        {
            var settings = ANShaderVariantStripperUtility.GetOrCreateSettings(false);
            if (settings == null || !settings.enabled || !settings.logSummary || _totalBefore <= 0)
                return;

            var lines = new System.Text.StringBuilder();
            lines.AppendLine("AN Shader Variant Stripper summary");
            lines.AppendLine($"Before:   {_totalBefore:n0}");
            lines.AppendLine($"After:    {_totalAfter:n0}");
            lines.AppendLine($"Stripped: {_totalStripped:n0}");

            foreach (var pair in _strippedByReason)
                lines.AppendLine($" - {pair.Key}: {pair.Value:n0}");

            Debug.Log(lines.ToString());
        }
    }

    public sealed class ANShaderVariantStripperBuildEvents : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => -1001;

        public void OnPreprocessBuild(BuildReport report)
        {
            ANShaderVariantStripper.ResetCountersForBuild();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            ANShaderVariantStripper.LogSummaryForBuild();
        }
    }

    public static class ANShaderVariantStripperUtility
    {
        [MenuItem("Tools/ANSTUDIO/Shader Variants/Settings")]
        public static void SelectSettings()
        {
            var settings = GetOrCreateSettings(true);
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Tools/ANSTUDIO/Shader Variants/Create/Refresh Settings Asset")]
        public static ANShaderVariantStripperSettings CreateOrRefreshSettingsAsset()
        {
            return GetOrCreateSettings(true);
        }

        public static ANShaderVariantStripperSettings GetOrCreateSettings(bool createIfMissing)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ANShaderVariantStripperSettings>(ANShaderVariantStripperSettings.AssetPath);
            if (settings != null || !createIfMissing)
                return settings;

            EnsureFolder("Assets/ANSTUDIO/Scripts/ShaderVariantStripper");
            EnsureFolder("Assets/ANSTUDIO/Scripts/ShaderVariantStripper/Editor");
            EnsureFolder("Assets/ANSTUDIO/Scripts/ShaderVariantStripper/Editor/ShaderVariants");

            settings = ScriptableObject.CreateInstance<ANShaderVariantStripperSettings>();
            AssetDatabase.CreateAsset(settings, ANShaderVariantStripperSettings.AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return settings;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = System.IO.Path.GetFileName(path);

            if (string.IsNullOrEmpty(parent))
                return;

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
#endif
