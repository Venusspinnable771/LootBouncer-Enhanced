
# LootBouncer (Enhanced Edition)
Author: SeesAll

## Overview
LootBouncer is a Rust (uMod/Oxide) server plugin designed to prevent partially looted containers and junkpiles from blocking spawn groups. When players leave unwanted items in containers, Rust normally keeps the container alive indefinitely, preventing the spawn system from recycling that loot location.

LootBouncer solves this problem by automatically clearing partially looted containers after a configurable delay, freeing the spawn group so the game can respawn fresh loot.

This enhanced version includes multiple improvements over the original plugin, focusing on:
- spawn‑system friendliness
- smarter junkpile detection
- roadside vehicle pile support
- configurable cleanup thresholds
- improved timer handling
- better performance safety

The goal is to keep roadside loot flowing naturally without interfering with Rust's spawn engine.

---

# Features

## Partial Loot Cleanup
If a player removes some items from a container but leaves junk behind, the plugin will automatically clear the container after:

Default:
30 seconds

This prevents spawn groups from becoming permanently blocked.

---

## Reopen-Safe Container Logic
If a player reopens a container before the cleanup timer expires:

- the cleanup timer is canceled
- the container state remains tracked
- when the container is closed again the timer restarts

This prevents players from accidentally disabling cleanup by simply reopening containers.

---

## Junkpile Detection
LootBouncer intelligently detects roadside junkpile groups including:

- barrels
- crates
- mixed roadside junkpiles

Once enough of the junkpile has been looted, the plugin will clear the remaining containers to free the spawn group.

---

## Junkpile Cleanup Threshold
Administrators can define how much of a junkpile must be looted before the rest of the pile is removed.

Default:

0.6 (60%)

Example:

| Containers | Required Looted |
|------------|----------------|
| 3 | 2 |
| 4 | 3 |
| 5 | 3 |

This keeps cleanup natural and prevents fresh junkpiles from being prematurely removed.

---

## Roadside Vehicle / Van Support
Some roadside spawn groups include vehicle wrecks or vans with loot containers.

This enhanced version adds detection for:

- roadside vans
- vehicle wreck piles
- mixed roadside groups

These now recycle properly instead of behaving like vanilla roadside spawns.

---

## Junkpile Failsafe Timer
Even if a junkpile never reaches the cleanup threshold, a secondary timer ensures abandoned piles eventually clear.

Default:
150 seconds

This guarantees spawn groups never remain blocked indefinitely.

---

## Configurable Cleanup Radius
Administrators can control how far the plugin searches for containers belonging to the same roadside group.

Default:
12 meters

This radius is used when detecting:

- junkpile containers
- roadside vehicle groups
- nearby related loot containers

---

## Optional Nearby Loot Cleanup
When a junkpile is cleared, administrators can optionally remove nearby standalone loot containers.

Default:
Disabled

This prevents aggressive cleanup from affecting unrelated roadside spawns.

---

## Item Handling Options

### Remove Items Instead of Dropping
Default:
Enabled

This removes leftover items instantly instead of spawning dropped item entities, which improves server performance.

### Slap Players Who Leave Loot
Optional fun feature to slap players who leave containers partially looted.

Default:
Disabled

---

# Performance Considerations

LootBouncer is designed to be extremely lightweight.

Key performance principles:

- Event-driven (no global scans)
- Localized entity searches
- Safe pooled entity lists
- Timers automatically cleaned
- Spawn-system friendly

The plugin does **not**:

- spawn loot manually
- force spawn groups
- scan the entire map
- create heavy repeating tasks

This keeps server impact minimal even on high population servers.

---

# Default Configuration

```
{
  "Time before loot containers are emptied (seconds)": 30.0,
  "Empty the entire junkpile when automatically empty loot": true,
  "Junkpile cleanup threshold": 0.6,
  "Maximum cleanup radius for roadside groups": 12.0,
  "Empty the nearby loot when emptying junkpile": false,
  "Time before junkpiles are emptied (seconds)": 150.0,
  "Slaps players who don't empty containers": false,
  "Remove items instead of dropping them": true
}
```

---

# Recommended Server Settings

These settings work very well with LootBouncer for modded servers.

## 3x Server

```
spawn.min_rate 0.35
spawn.max_rate 0.65
spawn.min_density 1.5
spawn.max_density 2.5
```

## 5x Server

```
spawn.min_rate 0.45
spawn.max_rate 0.85
spawn.min_density 2
spawn.max_density 3
```

## 10x Server

```
spawn.min_rate 0.5
spawn.max_rate 1.0
spawn.min_density 2
spawn.max_density 3
```

---

# Installation

1. Place `LootBouncer.cs` inside:

```
/oxide/plugins/
```

2. Start or reload the plugin:

```
oxide.reload LootBouncer
```

3. The config will generate automatically in:

```
/oxide/config/LootBouncer.json
```

---

# License

You may distribute and modify this plugin with credit to the author.

---

# Credits

Original concept: LootBouncer community plugin

Enhanced version:
SeesAll

Major improvements include:

- threshold-based junkpile cleanup
- roadside vehicle detection
- spawn-friendly cleanup logic
- improved timer safety
- configurable detection radius
- optimized entity scanning
