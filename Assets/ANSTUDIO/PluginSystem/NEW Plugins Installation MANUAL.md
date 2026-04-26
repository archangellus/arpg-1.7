Plugin System — Import & Installation Manual

This guide shows you how to import, install, and verify a plugin in the ARPG Plugin System, plus how to apply or revert core code edits that a plugin ships with.
________________________________________
1) Before you start
•	Where plugins live: Assets/ANSTUDIO/PluginSystem/plugins (this is the folder tree the inspector manages). 
•	What a plugin is: any folder under plugins/ that contains your code/assets, optionally a plugin.patches.json describing editor-side code edits. 
________________________________________
2) Import a plugin (ZIP)
1.	Open Tools ▸ Plugin System ▸ Plugin System. 
2.	Click Import Plug-in and select the .zip.
o	The tool extracts to a temp folder, safely ignores __MACOSX junk, guards against zip-slip paths, then copies the top folder into Assets/ANSTUDIO/PluginSystem/plugins/<PluginName>. 
3.	If prefab names collide with existing project prefabs, you’ll be asked whether to overwrite. Choosing Overwrite will:
o	Move the existing prefab to a BCK backup alongside it and assign that backup a new GUID,
o	Copy in the plugin’s prefab while preserving the original GUID so references don’t break. 
4.	Unity refreshes assets, the plugin tree updates, and you’ll see a confirmation dialog. The system can also show a Dependency Reminder after import (toggle it via Tools ▸ Plugin System ▸ Show Import Dependency Reminder). 



3) Manual install (from source)
If you’re not using a ZIP, simply place your plugin folder under:
Assets/ANSTUDIO/PluginSystem/plugins/<YourPlugin> and Refresh in the window. 
________________________________________
4) Apply the plugin’s core edits (if provided)
Many plugins ship a plugin.patches.json that describes safe, ID-tagged edits to your project files (C#, shaders, UXML, JSON, etc.). Supported operations:
•	insert_once (anchor-based insert)
•	replace_region (between start/end regex)
•	insert_at (line/column insert with markers & indentation) 
How to apply/revert
•	In the inspector’s Patch Builder (select the plugin root in the left tree), click:
o	Apply Core Edits (manifest) to apply every operation in plugin.patches.json.
o	Revert Core Edits to remove previously inserted regions for this plugin.
Both actions show a report (files touched, ops applied/skipped). 
•	Under the hood, edits are wrapped with markers like:
// >>> PLUGIN_PATCH:<PluginName>::<id> … user code … // <<< PLUGIN_PATCH:<PluginName>::<id>
This makes them idempotent and revertible. 
Safety: a backup of each touched file is created once (.before_plugin_patch) before any first-time write. 
________________________________________
5) Preview edits before applying (Patch Builder)
When building or reviewing edits:
•	Choose a Core files root and a Target Core File from a searchable popup. 
•	Set Line (0-based) and Row (indent hint). The preview:
o	Is read-only with background #181818,
o	Colors inserted code in #d5d184, and // comments in #608b4e,
o	Respects any already-applied edits so you see the real enclosing block. 
•	Stage multiple edits into a Pending list, then Save (Append) or Save (Replace) to write plugin.patches.json, or Replace & Apply All to write and run the patcher in one step. Pending lists are remembered per plugin root. 
•	The patch engine will indent and place the insert on its own line (exactly one newline before code; before a } line it inserts inside the block). 
________________________________________
6) Verify the plugin is loaded (Play Mode)
•	Auto-discovery: On game start (BeforeSceneLoad), the PluginManager scans all assemblies for non-abstract IPlugin types, orders them by Dependencies and LoadOrder, creates instances (requires a parameterless ctor), and calls Initialize(). On quit, Shutdown() is called in reverse order. Errors and cycles are logged. 
•	Descriptor metadata: Comes from the optional [Plugin] attribute (Id, DisplayName, Version, Dependencies, LoadOrder). If omitted, sensible defaults are used.
•	Sanity check: Enter Play Mode and watch the Console for your plugin’s logs (example below). 
Minimal plugin example (pattern)
The sample AttackLogger shows subscribing to the global EventBus.Attack on init and unsubscribing on shutdown.
7) Uninstall / reinstall
1.	In the Patch Builder for that plugin, click Revert Core Edits (removes all marked regions for this plugin). 
2.	Delete the plugin’s folder under plugins/. Unity will refresh.
3.	If you re-import later, you can Apply Core Edits again from the manifest. 
________________________________________
8) Troubleshooting
•	“Missing parameterless constructor” — add a public empty constructor to your plugin class. The manager only auto-instantiates types with a default ctor. 
•	Duplicate plugin id — if two plugins share the same Id, the later one is skipped (warning logged). Ensure unique Ids. 
•	Circular or missing dependencies — cycles fall back to LoadOrder with errors logged; missing deps are warned and that edge is ignored. 
•	Patch apply says “Anchor not found / Region not found” — check your manifest regex/line targets; the engine logs per-file messages and skips unsafe ops. 
•	Prefabs lost references after overwrite — the exporter/importer preserves target GUIDs when replacing; backups get a new GUID by design. Verify you chose Overwrite and review the summary dialog. 
________________________________________
9) Best practices when creating plugins
•	Decorate the main class with [Plugin] and set a unique Id. Use Dependencies to declare order without hardcoding timing, and LoadOrder as a lightweight tiebreaker. 
•	Subscribe to events in Initialize() and unsubscribe in Shutdown() (see EventBus/AttackLogger).
•	If distributing core edits, ship a clean plugin.patches.json (you can Normalize Manifest to remove duplicates and stabilize IDs). 
•	Provide a README in your plugin folder with any manual steps (assets, scenes, scripting defines), then rely on Apply Core Edits for code changes.

You’re all set
•	Import the ZIP,
•	Resolve any prefab conflicts,
•	Apply the core edits,
•	Enter Play Mode and check your plugin logs.

