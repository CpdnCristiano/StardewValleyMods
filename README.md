# StardewValleyMods

A collection of quality-of-life, compatibility, and translation mods for Stardew Valley developed by [CpdnCristiano](https://github.com/CpdnCristiano).

This repository contains the source code and assets for the following active mods:

---

## 📂 Active Mods

### 1. [StardewArchipelagoTranslations](file:///d:/StardewValleyMods/StardewArchipelagoTranslations/)
*   **Description**: A standalone multilingual localization and patcher mod for [StardewArchipelago](https://github.com/KaitoKid/StardewArchipelago) utilizing Harmony patches.
*   **Key Features**:
    *   **Modular Mail System**: Load custom, context-specific emails from `{locale}/{category}.json` (e.g. `weapon`, `tool`, `food`, `ring`, `boots`, `book`, `fish`, `default`).
    *   **Food Dynamic Stats**: Dynamically computes food `Edibility` to supply `{{energy}}` and `{{health}}` variables to mail templates in real-time.
    *   **Advanced Cache & Pre-Scouting**: Multi-layer item/location lookup caches and background pre-scouting thread to eliminate shop menu lag.
    *   *Read more in the specific [StardewArchipelagoTranslations README](file:///d:/StardewValleyMods/StardewArchipelagoTranslations/README.md).*

### 2. [FullInventoryView](file:///d:/StardewValleyMods/FullInventoryView/)
*   **Description**: A UI quality-of-life mod that expands the active inventory display.
*   **Key Features**:
    *   Displays all unlocked inventory rows at once when opening chests or menus, eliminating the need to scroll manually.
    *   *Read more in the specific [FullInventoryView README](file:///d:/StardewValleyMods/FullInventoryView/README.md).*

### 3. [FixWarpGreenhouses](file:///d:/StardewValleyMods/FixWarpGreenhouses/)
*   **Description**: A compatibility utility for Stardew Valley greenhouse setups.
*   **Key Features**:
    *   Corrects exit and entrance teleportation coordinates when running multiple greenhouses or custom greenhouse expansion mods.
    *   *Read more in the specific [FixWarpGreenhouses README](file:///d:/StardewValleyMods/FixWarpGreenhouses/README.md).*

---

## 🛠️ Build & Development

### Requirements
*   [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
*   [Stardew Valley](https://www.stardewvalley.net/) installed on your machine
*   [SMAPI](https://smapi.io/) installed

### Compiling the Solution
All projects are organized within the single Visual Studio solution file `StardewValleyMods.sln`. To compile the projects and automatically deploy them to your Stardew Valley `Mods` folder, run the following command in the repository root:

```bash
dotnet build
```

This uses SMAPI's build configuration tasks to automatically compile the C# code, package the translations and folders, and deploy the compiled mods straight into your game's directory for instant gameplay testing!
