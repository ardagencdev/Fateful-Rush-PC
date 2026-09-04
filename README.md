# Fateful Rush PC

**Fateful Rush PC** is the Windows version of Fateful Rush, adapted for **Google Play Games on PC**.

The game is a fast-paced 2D arcade survival experience focused on movement, timing, risk management, combos, abilities and progressively more difficult missions.

This repository contains the PC-specific Unity project and platform adaptations.

---

## Platform

- Windows
- Google Play Games on PC

The Android version is maintained separately:

[Android Version Repository](https://github.com/ardagencdev/Fateful-Rush)

---

## Gameplay

Players enter increasingly hostile arenas and complete different mission objectives:

- Reach a target score
- Survive for a required amount of time
- Reach the required score before time runs out

The game features 40 missions with increasingly complex enemy combinations, hazards and boss encounters.

---

## Features

- 40 progressively challenging missions
- Fast-paced 2D arcade survival gameplay
- Dash, Clone and Slow abilities
- Combo and Near Miss systems
- Multiple enemy types
- Environmental hazards
- Boss and Mini-Boss encounters
- Unlockable skins
- Persistent player statistics
- Google Play Games achievements
- Google Play Games leaderboards
- PC-specific controls and UI behaviour

---

## PC Adaptations

The PC version includes platform-specific changes compared to the Android build:

- Mouse and keyboard focused interaction
- Mobile joystick UI removed
- PC-specific HUD positioning
- Adjusted menu and button interaction
- Mouse hover support for UI elements
- PC-oriented performance configuration
- Google Play Games on PC integration
- Platform-safe separation from the Android Unity project

The Android and PC projects share core gameplay systems while maintaining separate platform-specific settings and behaviour.

---

## Technical Highlights

- Built with Unity 6
- Written in C#
- Universal Render Pipeline
- Unity Input System
- ScriptableObject-based mission configuration
- Object and projectile pooling
- Modular enemy behaviour systems
- Persistent progression and statistics
- Audio Mixer-based sound routing
- Google Play Games Services integration
- Separate Android and PC project structure

---

## Project Structure

Core Unity project folders included in this repository:

```text
Assets/
Packages/
ProjectSettings/
```

Generated Unity folders such as `Library`, `Temp`, `Logs`, `Build` and `UserSettings` are excluded from version control.

Sensitive signing files and local development files are also excluded from the repository.

---

## Technology Stack

- Unity 6
- C#
- Universal Render Pipeline
- Unity Input System
- Google Play Games Services
- Git
- GitHub

---

## Development Structure

Fateful Rush is maintained as two separate Unity projects:

```text
Fateful Rush Android
        |
        | Shared gameplay systems
        |
Fateful Rush PC
```

The projects share the same core game design and gameplay systems while platform-specific settings, input systems, UI behaviour and optimizations are maintained independently.

This structure allows each platform to be updated without overwriting platform-specific configuration.

---

## Android Version

The Android version of Fateful Rush is available in a separate repository:

[Fateful Rush Android Repository](https://github.com/ardagencdev/Fateful-Rush)

It is publicly available on Google Play.

[View Fateful Rush on Google Play](https://play.google.com/store/apps/details?id=com.youngdevstudios.fatefulrush)

---

## Release Status

The PC version is currently being prepared and optimized for **Google Play Games on PC**.

Development focuses on maintaining gameplay parity with the Android release while adapting controls, interface behaviour, performance settings and platform-specific systems for desktop use.

---

## Repository Purpose

This repository documents the PC adaptation and development of Fateful Rush.

It includes the Unity project files required to rebuild and continue development of the Windows version while excluding generated Unity files, build outputs and sensitive local configuration.

---

## Developer

Developed and published by **YoungDev Studios**.

Fateful Rush is developed as both a commercial game project and a game development portfolio project.
