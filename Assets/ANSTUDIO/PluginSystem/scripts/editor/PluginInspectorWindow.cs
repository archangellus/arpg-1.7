using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    /// <summary>
    /// Editor window for managing plugins.
    /// </summary>
    public partial class PluginInspectorWindow : EditorWindow
    {
        private const string PluginsPath = "Assets/ANSTUDIO/PluginSystem/plugins";
        private const string PluginStatesFile = "plugin_states.json";

        [SerializeField] private Vector2 m_workScroll = Vector2.zero;
        private string m_highlightedCode;
        private string m_patchHighlightedCode = string.Empty;
        private string m_patchSearchHighlightedCode = string.Empty;
        private GUIStyle m_highlightStyle;
        private GUIStyle m_editStyle;
        private GUIStyle m_lineNumberStyle;
        // Default colors for syntax highlighting
        private Color m_keywordColor = new Color32(0, 0, 255, 255);
        private Color m_commentColor = new Color32(0, 128, 0, 255);
        private Color m_stringColor = new Color32(163, 21, 21, 255);
        private Color m_methodColor = new Color32(128, 0, 128, 255); // default purple
        private Color m_typeColor = new Color32(86, 156, 214, 255); // cyan-ish
        private Color m_numberColor = new Color32(181, 206, 168, 255); // light green
        private Color m_interfaceColor = new Color32(86, 156, 214, 255); // cyan-ish
        private Color m_memberColor = new Color32(220, 220, 170, 255); // VS “identifier” yellow
        private Color m_delegateColor = new Color32(197, 134, 192, 255); // purple

        private const string PREF_INTERFACE = "PluginInspector.InterfaceColor";
        private const string PREF_MEMBER = "PluginInspector.MemberColor";
        private const string PREF_DELEGATE = "PluginInspector.DelegateColor";

        private const string PREF_KEYWORD = "PluginInspector.KeywordColor";
        private const string PREF_COMMENT = "PluginInspector.CommentColor";
        private const string PREF_STRING = "PluginInspector.StringColor";
        private const string PREF_METHOD = "PluginInspector.MethodColor";
        private const string PREF_TYPE = "PluginInspector.TypeColor";
        private const string PREF_NUMBER = "PluginInspector.NumberColor";

        private readonly Dictionary<string, PluginState> m_pluginStates = new(StringComparer.OrdinalIgnoreCase);
        private Texture2D m_toggleGreenCircle;
        private Texture2D m_toggleRedCircle;
        private const float INLINE_TOGGLE_WIDTH = 35f;
        private const float INLINE_TOGGLE_HEIGHT = 17f;

        private string m_selectedPath = null;      // full path of node currently picked
        private string m_code;
        private Vector2 m_codeScroll;
        private bool m_stylesReady;
        // Put at the top of the class so we can tune it easily
        private const float COLOR_CELL_WIDTH = 140f;   // minimum width of one “label + swatch”
        private const float COLOR_SWATCH_W = 60f;    // width of the actual colour box
        private const float COLOR_SWATCH_H = 16f;    // height of the colour box
        private const string PROFILE_EXT = "picprofile";
        private const string PREF_LAST_PROFILE = "PluginInspector.LastProfilePath";
        // ───── navigation tree ─────────────────────────────────────────────
        // Use generic TreeView types when available (Unity 6.2+). Older Unity 6
        // builds ship the non-generic API, so we fall back to those types to
        // avoid compiler errors when running on versions below 6.2.
#if UNITY_6000_2_OR_NEWER
        private TreeViewState<int> m_treeState;
        private PluginTreeView m_treeView;
#else
        private TreeViewState m_treeState;
        private PluginTreeView m_treeView;
#endif

        //private const float PALETTE_H = 42f;
        //private const float BOTBAR_H = 22f; 
        private static bool s_isExporting;

        // ── Patch Builder UI state
        private const int DEFAULT_SEARCH_LINE_RANGE = 10;

        private int m_patchLine = 0;            // 0-based line to append at (end-of-line)
        private int m_patchRow = 0;            // 0-based "column" used as indent hint (optional)
        private string m_patchCode = "";
        private string m_patchSearchCode = "";
        private bool m_replaceLine = false;
        [SerializeField] private Vector2 m_patchCodeScroll = Vector2.zero;
        [SerializeField] private Vector2 m_patchSearchScroll = Vector2.zero;
        [SerializeField] private bool m_useSearch = true;
        [SerializeField] private int m_patchSearchLineRange = DEFAULT_SEARCH_LINE_RANGE;
        [SerializeField] private bool m_showManifestMaintenance = false;
        [SerializeField] private float m_patchPreviewSplit = 0.5f;
        private bool m_patchPreviewSplitDragging = false;
        private float m_patchPreviewSplitStartX = 0f;
        private float m_patchPreviewSplitStartValue = 0.5f;
        private const float PATCH_EDITOR_MIN_HEIGHT = 140f;
        [SerializeField] private float m_patchEditorMaxHeight = 260f;
        private bool m_patchEditorHeightDragging = false;
        private float m_patchEditorHeightStartY = 0f;
        private float m_patchEditorHeightStartValue = 0f;
        private string m_activePluginRoot = null;   // already added in your last step
        private string m_pendingSavedAt = "";       // NEW: last autosave timestamp (local time)
        private const string PREF_LINE_OFFSET_INDEX = "PluginInspector.LineOffsetIndex";
        private static readonly int[] LINE_OFFSET_STEPS = { -2, -1, 0, 1, 2 };
        private const int DEFAULT_LINE_OFFSET_INDEX = 2; // → offset 0
        [SerializeField] private int m_lineOffsetIndex = DEFAULT_LINE_OFFSET_INDEX;
        [SerializeField] private SearchField m_search;    // survives domain reloads
        [SerializeField] private Vector2 m_previewScroll = Vector2.zero; // scroll in the preview list
        [SerializeField] private string m_lastPreviewSnapshot = string.Empty;
        [SerializeField] private int m_lastPreviewLine = -1;
        [SerializeField] private string m_lastPreviewTargetPath = string.Empty;
        [SerializeField] private Vector2 m_pluginListScroll = Vector2.zero;
        [SerializeField] private bool m_pluginListExpanded = true;
        private readonly Dictionary<string, Texture2D> m_pluginIconCache = new(StringComparer.OrdinalIgnoreCase);


        // ── Target selection via dropdown
        private string m_coreFolderRoot = "Assets/PLAYER TWO/";  // default root
        private List<string> m_coreFiles;                        // files under root
        private int m_coreIndex = 0;
        private string m_patchTargetAssetPath = "";              // currently selected target

        private static readonly string[] CORE_EXTS = {
            ".cs", ".shader", ".compute", ".cginc", ".hlsl", ".uxml", ".uss", ".json", ".txt"
        };

        // ── Preview styles (bg + label)
        private static Texture2D s_PreviewBgTex;
        private static GUIStyle s_PreviewBoxStyle;
        private static GUIStyle s_PreviewLabelStyle;

        // ── Metadata
        private const string PluginMetadataFolderName = "pluginDetails";
        private const string PluginMetadataFileName = "plugin.meta.json";
        private const int DescriptionCharacterLimit = 1548;
        [SerializeField] private string m_pluginDescription = string.Empty;
        [SerializeField] private string m_previewImageRelativePath = string.Empty;
        [SerializeField] private Texture2D m_previewTexture;
        [SerializeField] private string m_iconRelativePath = string.Empty;
        [SerializeField] private Texture2D m_iconTexture;
        [SerializeField] private string m_metadataRoot = string.Empty;
        private GUIStyle m_descriptionStyle;
        private static GUIContent TT(string text, string tip) => new GUIContent(text, tip);

        // ── Patch Builder scrolls
        [SerializeField] private Vector2 m_patchBuilderScroll;
        [SerializeField] private Vector2 m_manifestScroll;
        [SerializeField] private Vector2 m_pendingScroll;

        // In-memory manifest we’re editing
        [SerializeField] private PatchRecipe m_manifest = new PatchRecipe();
        [SerializeField] private string m_manifestLastPath = "";
        [SerializeField] private long m_manifestLastWriteTicks = -1;

        // Selection for existing ops
        [SerializeField] private int m_selTargetIdx = -1;
        [SerializeField] private int m_selOpIdx = -1;
        [SerializeField] private string m_manifestSearch = "";
        [SerializeField] private ReorderableList m_manifestList;
        [SerializeField] private readonly List<ManifestListItem> m_manifestItems = new();
        [SerializeField] private bool m_manifestDragging;

        // ── Pending staged edits (multi-apply)
        [SerializeField] private List<PendingEdit> m_pending = new();
    }
}
