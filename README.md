# Blood Sanctum

[한국어](README.ko.md)

A 3D action RPG built solo in **Unity 6** (URP). You fight through five independent zones, level up, craft and enhance gear, take on two pattern-driven bosses, and seal the Sanctum to finish the story.

This repository contains **only the original C# code and the game-data assets** (ScriptableObjects). Art, models, animations and VFX come from third-party asset packs that I do not have the right to redistribute, so they are not included — see [Third-party assets](#third-party-assets). The project is published for code review; it will not open as a playable project by cloning alone.

| | |
|---|---|
| Engine | Unity 6000.6.2f1, Universal Render Pipeline 17.6 |
| Language | C# — about 29,000 lines in 16 modules (`Assets/RPGGame/Scripts`) |
| Input | Unity Input System |
| Platform | Standalone (macOS build tested) |
| Role | Everything — design, programming, balancing, UI, content |

## Screenshots

| | |
|---|---|
| ![Village](docs/screenshots/01-village.jpg) | ![Skill combat](docs/screenshots/02-combat-skill.jpg) |
| Village hub with HUD, minimap and quick slots | Dash Strike hitting a pack: damage numbers, level-up banner, slash VFX |
| ![Inventory](docs/screenshots/03-inventory.jpg) | ![Boss encounter](docs/screenshots/04-boss-encounter.jpg) |
| Tabbed inventory with fixed, draggable slots | Boss encounter: name, phase and HP bar with portrait dialogue |

## Gameplay

- **Combat:** three-hit sword combo (the finisher always crits), dash with cooldown, aim assist and a short lunge toward the target, hit stop / camera shake / hit sparks for feedback.
- **Skills:** four active skills plus an ultimate, each with up to four levels, unlocked with skill points and bindable to quick slots.
- **Gear:** every equipment drop is its own instance with randomly rolled stats and its own enhancement level; enhancement has per-step success rates and material/gold costs.
- **Progression:** levels, stat points, quests (kill / collect / talk / tutorial), shops, a blacksmith, and loot drops.
- **World:** five independent zones connected through a teleport network, hunting grounds of increasing difficulty, a dark dome dungeon, and two bosses with telegraphed attack patterns.
- **Ending:** sealing the throne with the Blood Stone plays a cutscene and returns you to the village.

## Technical highlights

### Data-driven design
Balance and content live in ScriptableObjects, not in code, so a designer can change them without touching scripts.
- Skills — [`SkillData`](Assets/RPGGame/Scripts/Skill/SkillData.cs) is an abstract base with one subclass per skill (`DashStrikeSkillData`, `LaserRainSkillData`, `UltimateBlastSkillData`, …). Damage = `(flat + attack × ratio) × level multiplier`, so late-game attack growth does not scale skills linearly. Cooldowns can be set per level.
- Boss attacks — [`BossPatternData`](Assets/RPGGame/Scripts/Monsters/Boss/Data/BossPatternData.cs) subclasses (charge, combo, frost cone, line eruption, meteor rain, projectile, shockwave, summon, blink strike, area damage-over-time). A boss is a list of pattern assets plus phase rules.
- Items, enhancement tables, shop prices, quests, NPC dialogue, enemy stats and per-level player stats are all assets under [`Assets/RPGGame/Data`](Assets/RPGGame/Data) and [`Resources/Data`](Assets/RPGGame/Resources/Data).

### Combat architecture
- [`Damageable`](Assets/RPGGame/Scripts/Core/Damageable.cs) is the single entry point for damage: invulnerability window, defense via [`DamageFormula`](Assets/RPGGame/Scripts/Core/DamageFormula.cs) (a ratio-based reduction rather than flat subtraction), crit flag, knockback, and dead-target rejection.
- [`HPController`](Assets/RPGGame/Scripts/Core/HPController.cs) owns health and raises damage/heal/death events, with a one-shot death guard so repeated hits on a corpse cannot re-trigger death logic, and an interceptor hook for effects such as a once-per-run revive.
- Event-driven wiring (UnityEvents plus code subscriptions) keeps health bars, damage numbers, hit effects, quest counters and loot drops decoupled from the combat code.

### State machines
- Player — [`PlayerStateManager`](Assets/RPGGame/Scripts/Player/State/PlayerStateManager.cs) with Idle / Move / Roll / Attack / Dead states; attack timing is driven by animation events and `StateMachineBehaviour`s so hit windows match the clips.
- Monsters and bosses — Idle / Patrol / Chase / Attack / Dead for monsters; the boss adds Heal and PhaseTransition and runs its patterns through a shared [`BossPatternContext`](Assets/RPGGame/Scripts/Monsters/Boss/Data/BossPatternContext.cs).

### Systems
- **Inventory** — fixed, draggable slots per tab; items keep their slot when others are removed, and equipment swaps in place. See [`UIInventoryWindow`](Assets/RPGGame/Scripts/UI/Windows/Inventory/UIInventoryWindow.cs).
- **Equipment instances** — [`EquipmentInstance`](Assets/RPGGame/Scripts/Item/EquipmentInstance.cs) separates the shared item definition from per-drop rolled stats and enhancement.
- **Spawning** — [`MonsterSpawner`](Assets/RPGGame/Scripts/Monsters/MonsterSpawner.cs) with population caps and batch respawns; [`HuntingGroundSpawner`](Assets/RPGGame/Scripts/Monsters/HuntingGroundSpawner.cs) keeps a fixed population per zone and can lower it while a given quest is active.
- **Save / load** — JSON save ([`SaveManager`](Assets/RPGGame/Scripts/Save/SaveManager.cs), [`GameSaveLoader`](Assets/RPGGame/Scripts/Save/GameSaveLoader.cs)) covering stats, inventory, equipment instances, quests and skills, including conversion of older save formats.
- **UI** — about 70 scripts: window stack closed by Esc, item and skill tooltips, quest tracker and minimap, a three-line first-in-first-out damage text, system messages, dialogue with portraits.
- **Dev tooling** — [`GameTestTools`](Assets/RPGGame/Scripts/Dev) adds debug hotkeys for granting items, completing quests, teleporting and killing bosses so any part of the game can be tested quickly.

## Repository layout

```
Assets/RPGGame/
├── Scripts/
│   ├── Audio/ Core/ Dev/ Effects/ Interaction/
│   ├── Item/ Monsters/ NPC/ Player/ Quest/
│   ├── Save/ Settings/ Shop/ Skill/ UI/ World/
├── Data/            ScriptableObject data (skills, items, boss patterns, shop, enhancement)
└── Resources/Data/  Runtime-loaded data (player levels, enemies, quests, NPCs, item database)
Packages/manifest.json
ProjectSettings/ProjectVersion.txt
```

## Third-party assets

Not included in this repository. The game uses, among others: Hovl Studio and Eric VFX Studio effects, the EEJANAI free sword animations, MS WeaponVol01 weapon models, Mixamo character animation, ParticlePro FX, Gabriel Aguiar Productions VFX, Bandits Valley environment pack, ForestVision, Rocks and Boulders 2, Footsteps – Essentials, game input icon packs, and the Nanum Gothic font. Each remains the property of its creator under its own license.

## Running it

The scripts target Unity **6000.6.2f1**. To explore them, open the folders in any editor; to run the game you would need to import the third-party packs above into a matching project.
