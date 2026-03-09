
# LootBouncer (Enhanced Edition)

**Version:** 1.3.1  
**Enhanced by:** SeesAll  

LootBouncer is a Rust (uMod/Oxide) plugin that automatically cleans up partially looted containers and junkpiles to prevent spawn-group blocking and improve roadside loot cycling.

When players take only valuable items and leave junk behind, Rust normally keeps that container alive indefinitely. This blocks the spawn group from refreshing. LootBouncer solves this by automatically clearing leftover loot after a configurable delay so the spawn system can recycle the location naturally.

This repository contains an **enhanced version** of the original plugin with additional features, performance improvements, and expanded roadside support.

---

# Credits

This plugin builds upon the original LootBouncer plugin.

**Original Authors**
- Sorrow
- Arainrr

**uMod Listing Credit**
- VisEntities

**Enhanced Version**
- SeesAll

Enhancements include new junkpile logic, improved roadside detection, better timer safety, and additional configuration options.

---

# Enhancements Over the Original Plugin

This enhanced version introduces several improvements:

- Junkpile cleanup threshold system
- Roadside vehicle / van junkpile detection
- Configurable cleanup radius
- Reopen-safe container timers
- Junkpile failsafe cleanup timer
- Improved spawn-group recycling
- Performance and stability improvements
- Cleaner configuration defaults
- Optimized entity scanning and timer handling

The goal is to keep roadside loot flowing naturally without interfering with Rust's spawn engine.

---

# Features

## Partial Loot Cleanup

If a player partially loots a container, the plugin automatically clears it after a delay.

Default:

30 seconds

This prevents containers from blocking spawn groups indefinitely.

---

## Reopen-Safe Containers

If a player reopens a container before the timer expires:

- The timer is paused
- The container remains tracked
- When the container is closed again the cleanup timer resumes

This prevents accidental disabling of the cleanup system.

---

## Junkpile Detection

The plugin detects and groups roadside junkpile components such as:

- barrels
- crates
- mixed roadside piles

Once enough of the junkpile has been looted, the remaining containers can be cleared automatically.

---

## Junkpile Cleanup Threshold

Administrators can control how much of a junkpile must be looted before the remainder is cleared.

Default:

0.6 (60%)

Example:

| Containers | Required Looted |
|------------|----------------|
| 3 | 2 |
| 4 | 3 |
| 5 | 3 |

This keeps cleanup natural while preventing blocked spawn groups.

---

## Roadside Vehicle / Van Support

Some roadside spawn groups contain vehicles such as vans or wrecked cars with loot containers.

This enhanced version detects:

- roadside vans
- vehicle wreck piles
- mixed roadside junk groups

These now recycle properly instead of behaving like vanilla roadside spawns.

---

## Junkpile Failsafe Timer

Even if the cleanup threshold is never reached, abandoned junkpiles are cleared after a secondary timer.

Default:

150 seconds

This guarantees spawn groups eventually recycle.

---

## Configurable Cleanup Radius

Administrators can control how far the plugin searches for related roadside loot.

Default:

12 meters

This radius is used when detecting:

- junkpile containers
- roadside vehicle piles
- related nearby loot

---

## Optional Nearby Loot Cleanup

When a junkpile is cleared, the plugin can optionally remove nearby standalone loot containers.

Default:

Disabled

This prevents aggressive cleanup from affecting unrelated roadside spawns.

---

# Default Configuration

```json
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

# Installation

1. Place **LootBouncer.cs** inside:

```
/oxide/plugins/
```

2. Reload the plugin:

```
oxide.reload LootBouncer
```

3. The configuration file will generate automatically in:

```
/oxide/config/LootBouncer.json
```

---

# Recommended Server Spawn Settings

These settings work well with LootBouncer for modded servers.

### 3x Servers

```
spawn.min_rate 0.35
spawn.max_rate 0.65
spawn.min_density 1.5
spawn.max_density 2.5
```

### 5x Servers

```
spawn.min_rate 0.45
spawn.max_rate 0.85
spawn.min_density 2
spawn.max_density 3
```

### 10x Servers

```
spawn.min_rate 0.5
spawn.max_rate 1.0
spawn.min_density 2
spawn.max_density 3
```

---

# Performance

LootBouncer is designed to be extremely lightweight.

Key design principles:

- Event-driven logic
- Localized entity scanning
- Safe pooled entity lists
- Automatic timer cleanup
- Spawn-system friendly behavior

The plugin does **not**:

- spawn loot manually
- force spawn groups
- scan the entire map
- run heavy repeating loops

This keeps performance impact minimal even on high population servers.

---

# License

This repository uses the MIT License.

---

# Contributing

Pull requests and improvements are welcome.
