# SkillTreeUI Plugin

UI-based skill tree plugin for ARPG Project.

## Features
- Condition-based unlocking:
  - Required character level.
  - Required quest IDs (string IDs you mark complete).
  - Required prerequisite skills.
- Learning a node calls `EntitySkillManager.TryLearnSkill(skill)`.
- Optional automatic skill point grants on level gain.
- No modifications to ARPG Project core scripts.

## Setup
1. Add `SkillTreeUIController` to a UI GameObject in your scene (Canvas recommended).
2. You can leave `entity` and `skillManager` empty. The controller auto-resolves the runtime player using `Level.instance.player` (and retries until the player is spawned). You may still assign explicit references if desired.
3. Assign a `pointsText` Text (optional).
4. Configure `nodes`:
   - `id`: unique node id.
   - `skill`: the `Skill` asset to learn.
   - `skillPointCost`.
   - `requiredLevel`.
   - `requiredQuestIds`.
   - `requiredSkills`.
   - `learnButton`, `learnedState`, `statusText` UI refs.
5. If your quest system emits `EventBus.QuestCompleted`, this plugin listens for it.
   - Supported payloads: `Quest`, `string` quest id, or `object[]` with first arg as one of those.
   - You can also call `MarkQuestCompleted("quest_id")` manually.

## Runtime behavior
- Nodes become interactable only when all conditions are met.
- On click, a node learns its skill and consumes points.
- Learned nodes are shown as Learned in status.

