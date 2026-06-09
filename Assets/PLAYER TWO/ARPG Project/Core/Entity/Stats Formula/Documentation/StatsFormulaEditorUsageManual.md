# Stats Formula Editor Usage Manual

This manual explains how to create, edit, assign, and test visual stat formulas for `EntityStatsManager`.

## What the Stats Formula Editor Does

The Stats Formula Editor lets you build node-based formulas that can override selected dynamic stat calculations on an entity. If a formula is disabled, missing, or not connected to its `Result` node, the entity automatically falls back to the original built-in formula.

Supported formula targets include:

- `MinDamage`
- `MaxDamage`
- `MinMagicDamage`
- `MaxMagicDamage`
- `NextLevelExperience`
- `MaxHealth`
- `MaxMana`
- `AttackSpeed`
- `CriticalChance`
- `Defense`
- `ChanceToBlock`
- `BlockSpeed`
- `StunChance`
- `StunSpeed`
- `Accuracy`
- `Evasion`

## Step 1: Open the Formula Editor

1. Open the Unity project.
2. In the Unity top menu, choose:
   - `Tools > PLAYER TWO > ARPG Project > Stats Formula Editor`
3. The `Stats Formula Editor` window opens.

## Step 2: Create a Formula Graph Asset

1. In the Stats Formula Editor window, click `New Graph Asset`.
2. Choose a project folder where the graph asset should be saved.
3. Enter a clear name, for example:
   - `PlayerStatsFormulaGraph`
   - `EnemyStatsFormulaGraph`
   - `BossStatsFormulaGraph`
4. Click `Save`.
5. The created asset is assigned to the editor window automatically.

You can also create a graph from the Project window:

1. Right-click in the Project window.
2. Choose `Create > PLAYER TWO > ARPG Project > Entity > Stats Formula Graph`.
3. Select the new asset.
4. Open the Stats Formula Editor. The selected graph asset is picked up by the editor window.

## Step 3: Choose Which Stat Formula to Edit

1. Use the `Stat` dropdown in the editor toolbar.
2. Pick the dynamic stat you want to override, for example `MinDamage`.
3. Each stat target has its own formula inside the same graph asset.
4. Make sure `Enabled` is checked if you want this formula to override the built-in calculation.

If `Enabled` is unchecked, the formula remains saved but is ignored at runtime.

## Step 4: Add Built-In Example Formulas

If you want a starting point instead of building formulas from an empty graph, use the built-in examples.

1. Create or assign a graph asset.
2. Use the `Entity` dropdown to choose an entity prefab already present in the project. The list is built from prefabs that contain an `EntityStatsManager`.
3. If you recently added or changed prefabs, click `Refresh Entities` to rebuild the dropdown.
4. Use the `Example` dropdown to choose the stat example by name, such as `MinDamage`, `MaxHealth`, or `Accuracy`.
5. Click `Load Example` to replace only that stat target with a disabled example formula imported from the selected entity.
6. The editor automatically switches the `Stat` dropdown to the same target, so you can immediately see where that example belongs.
7. If you want templates for every supported stat target from the selected entity, click `Load All Examples` instead.
8. Confirm the replacement dialog.
9. Select any stat from the `Stat` dropdown to inspect that formula.
10. Enable only the formulas you want to use at runtime.
11. Edit the example nodes to make your balance changes.
12. Click `Save`.

The generated examples are based on the existing `EntityStatsManager` formulas and seeded with the selected entity's serialized base stats, so player, enemy, boss, and other prefab-specific values can be imported directly from project entities. They are disabled by default to avoid changing gameplay immediately after creation. `Load Example` affects only the selected example target, while `Load All Examples` replaces all formulas in the graph using the selected entity.

## Step 5: Understand the Node Types

Create formula nodes by right-clicking empty space in the graph and choosing `Create > Get Stat`, `Create > Constant`, or `Create > Operator`. You can also drag a connector line from an existing port and release it on empty space to open the same node creation menu; compatible nodes are connected automatically when possible.

The editor provides four formula node concepts:

### Get Stat Node

Use `Get Stat` nodes to read values from the entity, its equipped items, item attributes, or global game settings.

Examples:

- `Strength`
- `Dexterity`
- `Level`
- `WeaponDamageMin`
- `WeaponDamageMax`
- `AdditionalDamage`
- `HealthMultiplier`
- `MaxAttackSpeed`

### Constant Node

Use `Constant` nodes for fixed numeric values.

Examples:

- `2`
- `8`
- `100`
- `0.5`
- `1.25`

### Operator Node

Use `Operator` nodes to combine two input values.

Available operators:

- `Add`
- `Subtract`
- `Multiply`
- `Divide`
- `Min`
- `Max`

Operator nodes have two input ports:

- `A`
- `B`

The node output is the result of applying the selected operation to `A` and `B`.

### Result Node

Every formula has one `Result` node. The value connected to the `Result` node is the final formula value used by `EntityStatsManager`.

A formula is considered incomplete if nothing is connected to the `Result` node. In that case, the built-in stat formula is used instead.

## Step 6: Build a Simple Formula

This example builds a minimum damage formula similar to:

```text
(Strength / 8) + WeaponDamageMin + AdditionalDamage
```

1. Set `Stat` to `MinDamage`.
2. Right-click the graph and choose `Create > Get Stat`.
3. In the node dropdown, choose `Strength`.
4. Right-click the graph and choose `Create > Constant`.
5. Set the constant value to `8`.
6. Right-click the graph and choose `Create > Operator`.
7. Set the operator to `Divide`.
8. Connect `Strength` to the `A` input of the `Divide` node.
9. Connect the `8` constant to the `B` input of the `Divide` node.
10. Right-click the graph and choose `Create > Get Stat`.
11. Set this node to `WeaponDamageMin`.
12. Right-click the graph and choose `Create > Operator`.
13. Set the operator to `Add`.
14. Connect the output of the `Divide` node to the `A` input of the `Add` node.
15. Connect `WeaponDamageMin` to the `B` input of the `Add` node.
16. Right-click the graph and choose `Create > Get Stat`.
17. Set this node to `AdditionalDamage`.
18. Right-click the graph and choose `Create > Operator`.
19. Set the operator to `Add`.
20. Connect the previous `Add` output to the new `Add` node's `A` input.
21. Connect `AdditionalDamage` to the new `Add` node's `B` input.
22. Connect the final `Add` node output to the `Result` node.
23. Click `Save`.

## Step 7: Build the Example Formula from the Screenshot

The screenshot-style graph can be represented as:

```text
(((MinDamage + MaxDamage) / 2) * ((Strength + 1) / 100)) * 1.5
```

To build it:

1. Set `Stat` to the stat you want this graph to control, such as `MaxDamage` or another supported target.
2. Add two `Get Stat` nodes.
3. Set them to `MinDamage`-style and `MaxDamage`-style inputs if available in your project setup, or use `WeaponDamageMin` and `WeaponDamageMax` for weapon-range calculations.
4. Add an `Add` operator and connect both damage inputs to it.
5. Add a `Constant` node with value `2`.
6. Add a `Divide` operator.
7. Connect the damage `Add` output to `A` and the `2` constant to `B`.
8. Add a `Get Stat` node set to `Strength`.
9. Add a `Constant` node with value `1`.
10. Add an `Add` operator and connect `Strength` and `1` to it.
11. Add a `Constant` node with value `100`.
12. Add a `Divide` operator.
13. Connect the `Strength + 1` output to `A` and `100` to `B`.
14. Add a `Multiply` operator and connect the averaged damage branch and strength branch to it.
15. Add a `Constant` node with value `1.5`.
16. Add a final `Multiply` operator.
17. Connect the previous multiply result to `A` and `1.5` to `B`.
18. Connect the final output to `Result`.
19. Click `Save`.

## Step 8: Assign the Graph to an Entity

1. Select the GameObject that has an `EntityStatsManager` component.
2. In the Inspector, find `Formula Settings`.
3. Drag your `Entity Stats Formula Graph` asset into the `Formula Graph` field.
4. Enter Play Mode or force a stat recalculation through the existing entity/item workflows.
5. The enabled and connected formulas in the graph now override the corresponding dynamic stats.

## Step 9: Preview Formula Output in the Editor

The toolbar shows a `Preview` value when the selected formula is connected and can be evaluated.

Preview values use sample data, not the currently selected entity. They are useful for checking whether the graph is connected and mathematically valid, but final runtime values depend on the actual entity stats, equipped items, item attributes, and game settings.

## Step 10: Disable a Formula Without Deleting It

1. Select the graph asset in the editor window.
2. Choose the target stat from the `Stat` dropdown.
3. Uncheck `Enabled`.
4. Click `Save`.

The formula remains stored in the asset, but `EntityStatsManager` ignores it and uses the original built-in calculation.

## Step 11: Troubleshooting

### The stat did not change in play mode

Check the following:

1. The graph asset is assigned to the entity's `Formula Graph` field.
2. The correct target stat is selected in the formula graph asset.
3. The formula is `Enabled`.
4. The final node output is connected to `Result`.
5. The entity has recalculated stats after the graph was assigned or changed.

### The preview says `not connected`

This usually means the selected formula does not have a complete connection path into `Result`.

Fix it by connecting the final calculation node output to the `Result` node.

### Division returns zero

Division by zero is protected. If the `B` input of a `Divide` node is zero or approximately zero, the divide node returns `0` instead of throwing an error.

### Chance values look wrong

Some stats use normalized values internally. For example, `CriticalChance`, `ChanceToBlock`, and `StunChance` are typically represented as `0.25` for 25%, not `25`.

### A formula should use the original built-in value as part of its graph

The current graph inputs expose raw entity, item, attribute, and game-setting values. If you need the exact built-in result as an input node, add that as a new formula input in code or duplicate the built-in calculation with nodes.

## Recommended Workflow

1. Create one graph asset per stat profile, such as player, enemy, or boss formulas.
2. Edit one stat target at a time.
3. Keep formulas disabled until they are connected and previewing correctly.
4. Assign the graph to a test entity.
5. Enter Play Mode and compare before/after stat values.
6. Use `Save` after node edits, especially before entering Play Mode.
7. Duplicate working graph assets before experimenting with major balance changes.

## Safety Notes

- Missing formulas fall back to built-in calculations.
- Disabled formulas fall back to built-in calculations.
- Incomplete formulas fall back to built-in calculations.
- The editor-only window is not included in runtime builds.
- Runtime formula assets can be reused by multiple entities.
