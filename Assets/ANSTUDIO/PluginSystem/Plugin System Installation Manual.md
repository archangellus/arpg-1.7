Plugin System Installation & First-Run Manual

This guide shows how to install the Plugin System into a Unity project from a distributable package (the .unityengine file you received is a standard Unity import package) and how to import a plug-in, apply its core edits, and verify the runtime loader.
________________________________________
1) What’s included
•	Editor Window: “Plugin System” window to add, import, preview, patch, revert, and export plug-ins. It lives under Tools/Plugin System/Plugin System. 
•	Runtime Loader: PluginManager discovers every class that implements IPlugin, orders them by declared dependencies and load order, and calls Initialize() before the first scene loads, then Shutdown() on quit. 
•	Patcher: PluginCorePatcher applies code/UI patches described by each plug-in’s plugin.patches.json using insert_once, replace_region, and insert_at, with clearly marked regions that can be reverted safely. 
Default plug-in root inside your project:
Assets/ANSTUDIO/PluginSystem/plugins (the window reads and writes here). 
________________________________________
2) Install the Plugin System (.unityengine file)
1.	Open your Unity project.
2.	Import the package: double-click the .unityengine file you received or drag it into the Unity Editor window. Unity’s import dialog will add the Plugin System under Assets/ANSTUDIO/PluginSystem.
3.	After import, open the window: Tools → Plugin System → Plugin System. 
Optional: toggle the “Show Dependency Reminder After Import” menu if you want a reminder dialog whenever you import a plug-in. 



3) Import a plug-in (.zip) and handle conflicts
A. Import the plug-in archive
1.	Open Tools → Plugin System → Plugin System.
2.	In the bottom toolbar click Import Plug-in and select your plug-in .zip. The window safely extracts the contents into Assets/ANSTUDIO/PluginSystem/plugins/<PluginName>. 
3.	After import, a Manifest Reminder dialog can appear. Use Open Manifest to jump to the plug-in’s plugin.patches.json and verify dependencies, or Reveal In Finder to open the folder. You can also disable this reminder later. 
B. Prefab conflicts (same prefab name already exists)
If the importer finds existing prefabs with the same name outside the plug-in folder, it offers to overwrite them. If you accept, it will back up the original prefab as ...BCK.prefab, assign a new GUID to the backup, then copy the plug-in prefab and set its GUID to the original so scene references stay valid. You’ll get a readable summary after the operation. 
________________________________________
4) Apply a plug-in’s core edits (patches)
Many plug-ins ship a plugin.patches.json file that makes small, clearly marked edits to your core files.
•	Open the plug-in folder in the left tree, then select the plug-in root to see the Patch Builder. From here you can preview, stage, save, and apply edits. 
•	Click Apply Core Edits (manifest) to apply all edits from plugin.patches.json. Every inserted region is wrapped like:
You can Revert Core Edits at any time and the patcher keeps a .before_plugin_patch backup of every touched source file. 
Patch operations supported in the manifest:
insert_once (by regex anchor), replace_region (between start/end anchors), and insert_at (exact line/column with indent rules). 
The window’s preview is read-only, uses a dark background, and colors inserted code and comments differently so you can audit changes safely before applying. 
________________________________________
5) Verify the runtime loader
At play start the manager will:
1.	Discover all non-abstract classes that implement IPlugin.
2.	Build descriptors from optional [Plugin] attributes (id, display name, version, dependencies, load order). If the attribute is missing sensible defaults are used.
3.	Sort by dependencies and LoadOrder, instantiate, then call Initialize(). On Application.quitting it calls Shutdown() in reverse order. 
You’ll see warnings if a dependency id is missing or if there’s a circular dependency. 
________________________________________
6) Create your first plug-in (editor-assisted)
1.	In Plugin System window click Add Plug-in, give it a name, and the tool creates a folder and starter class implementing IPlugin. 
2.	Optionally decorate your class with the [Plugin] attribute to set a stable id, a nicer display name, a version, dependencies, and load order:
The attribute fields map directly to what PluginManager reads when ordering and loading. 
Example: listen to game events
A sample AttackLogger plug-in shows how to subscribe to a project-wide EventBus.Attack event during Initialize() and unsubscribe on Shutdown(). 
________________________________________
7) Export a plug-in (ship to others)
Select any file or folder inside a plug-in, then click Export Plug-in in the bottom toolbar. The tool zips the entire plug-in folder (including plugin.patches.json if present) into a single archive you can distribute. The export keeps a single top-level <PluginName>/ folder in the zip. 
________________________________________
8) Typical workflow (quick checklist)
1.	Install the Plugin System from the .unityengine file.
2.	Open the window and Import Plug-in (.zip). Resolve any prefab conflicts through the prompt. 
3.	Apply Core Edits (manifest) if the plug-in includes a plugin.patches.json. Preview first, then apply. 
4.	Press Play. PluginManager will auto-initialize plug-ins before the first scene loads. Watch the Console for plugin logs or dependency warnings. 
5.	Revert Core Edits or Normalize Manifest any time if you need to back out or clean up duplicates. 
________________________________________
9) Troubleshooting
•	“Manifest Not Found” when clicking Open Manifest: your plug-in may not ship edits. That’s fine, runtime plug-ins work without patches. 
•	Missing dependency warning after Play: a [Plugin(…, Dependencies = …)] id was not found. Install the required plug-in or remove the dependency. 
•	Circular dependency warning: two or more plug-ins depend on each other. The manager will fall back to LoadOrder. Fix your dependency graph. 
•	Can’t instantiate plug-in: ensure a parameterless constructor exists on your IPlugin class or register an instance manually via code. 
•	Undoing edits: use Revert Core Edits in the window or RevertById logic, and remember each edited file is backed up once as *.before_plugin_patch. 
________________________________________
Appendix — Where things go
•	Plug-ins folder: Assets/ANSTUDIO/PluginSystem/plugins/<YourPlugin> (files appear in the tree here). 
•	Per-plug-in manifest: plugin.patches.json in the plug-in root controls patch operations.

