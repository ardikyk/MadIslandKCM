# 🏝️ Mad Island — Kydra's Cheat Menu (MadIslandKCM)

[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)
[![BepInEx 5](https://img.shields.io/badge/Requires-BepInEx%205-orange.svg)](https://github.com/BepInEx/BepInEx)
[![Version](https://img.shields.io/badge/Version-1.2-green.svg)](../../releases)

[ **English Readme** ] | [ **[Русская версия (README_RU.md)](README_RU.md)** ] 

---

> **⚠️ DISCLAIMER / ПРЕДУПРЕЖДЕНИЕ**  
> Active use of mod functions may lead to in-game bugs. Backup your saves before using.  
> *Активное использование функций мода может привести к внутриигровым багам. Делайте резервную копию файлов сохранения.*

---

## 📌 Manual / Overview

**MadIslandKCM** is an in-memory cheat menu for **Mad Island** built on BepInEx 5.

It directly modifies player health states, survival parameters, and camera vectors in memory, alongside spawner catalogs, progression adjusters, and custom category mapping (`.txt`).

---

## ✨ Actual Features / Настоящие возможности

### 🎒 Item Spawner & Categories (.txt)
* **Item Spawner:** Access all game items with search, page navigation, sorting (A-Z / Z-A), and quantity buttons (1x, 10x, 100x, 999x).
* **Favorites System (⭐):** Mark items as favorites to display them in the Favorites category (saved in config).
* **Item-to-Category Assignment:** Reassign any item to a specific category with 1 click directly from the list.
* **Category Configuration (.txt):** Export/Import exact category item lists via `BepInEx/config/MadIslandKCM_Categories.txt`.

### 👤 Character & Survival
* **In-Memory HP Control:** Set Current HP, Max HP, or both simultaneously. Displays live HP state in the menu.
* **God Mode (Invincibility):** Cancels incoming damage on player components.
* **Restore Hunger & Thirst (2-in-1):** One-click 100% restoration of hunger and hydration.
* **3rd Person Orbit Camera:** Hold `T` + move Mouse to orbit camera around the character. Includes a 1-click Reset Camera button.
* **Fly / Noclip Mode:** Free flight through obstacles (`WASD` + `Space` / `LeftCtrl` / `C`, speed slider 2x - 40x).
* **Stats & Progression:** Adjust Experience (`/exp`), Status Points (`/point`), Skill Points (`/point skill`), Attack (`/atk`), Run Speed (`/run`), and Max Followers (`/followcap`).
* **Utility Actions:** Collect All (`/collectall`), Swap HP/MP & Gender (`/change`), Yona Down (`/yonadown`).

### 👥 NPC & Companions
* **Companion Controls:** Get stunned NPC by ID (`/getgen`), Max taming (`/petmax`), Random age (`/allage`), Love & Libido controls (`/love`, `/inclove`, `/libido`), Pregnancy (`/preg`, `/allpreg`), Reset position (`/resetpos`), Summon friends (`/friends`, `/friend`, `/makevill`), Teleport target NPC (`/call`), Patrol (`/pat`), Raid events (`/ass`), Kidnap/Rescue quests (`/addprisoner`, `/rescue`, `/deadtime`), Morale (`/moralall`).
* **NPC Catalog:** Spawner database for Natives (M/F), NPCs, Bosses, Friendly/Hostile Animals, Monsters, and Ruins/Lab entities.

### 🌍 World & Environment
* **Time Speed:** Smooth time scale slider (0x Pause to 50x) and hotkeys (`Shift + < / >`).
* **Time of Day & Weather:** Dawn, Noon, Sunset, Night, Sunny, Rain, Blood Rain.
* **Map & Teleportation:** Show coordinates (`/mapID`), Open map (`/mapopen`), Reset map resources (`/resetmap`), Teleport to Base (`/wp base`), Lab basement (`/stage labo2`), or Location ID (`/tp`).

---

## 💻 Requirements & System Compatibility

* **Game:** Mad Island (PC / Windows)
* **Mod Loader:** BepInEx 5.x (x64)
* **Framework:** .NET Framework 4.7.2 / Unity Mono

---

## 🚀 Quick Start & Installation

1. Install **BepInEx 5** into your `Mad Island` game folder.
2. Download the latest release from **[Releases](../../releases/latest)**.
3. Extract `MadIslandKCM.dll` into:
   `Mad Island/BepInEx/plugins/`
4. *(Optional)* Place `MadIslandKCM_Categories.txt` into:
   `Mad Island/BepInEx/config/`
5. Launch the game and press **F3** to open the menu.

---

## ⚙️ Category File (`MadIslandKCM_Categories.txt`)

Stored at `BepInEx/config/MadIslandKCM_Categories.txt`:
```text
CATEGORY|NameRU|NameEN|DefaultPrefixes|AssignedItemIDs
