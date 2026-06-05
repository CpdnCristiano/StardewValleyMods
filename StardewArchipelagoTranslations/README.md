# Stardew Archipelago Translations

> [!WARNING]
> **Project Status**: This translation mod is still **experimental** and is **not 100% complete**. It **is not intended to be published** at this time due to its active development and ongoing testing. Use at your own risk.

A standalone multilingual localization and patcher mod for [StardewArchipelago](https://github.com/KaitoKid/StardewArchipelago).

This mod utilizes Harmony patches to translate and customize core StardewArchipelago behaviors, providing players with localized content, custom i18n rules, and a modular category-based mail system.

---

## Key Features

### 1. Granular Modular Mail System
Mails are loaded dynamically based on the item's category from `templates/mail/{locale}/{category}.json`. This allows custom templates per category, rather than using a single pool. Supported categories include:
*   ⚔️ **`weapon`**: Swords, daggers, hammers, clubs, gauntlets, and other combat items.
*   🪓 **`tool`**: Axes, pickaxes, hoes, watering cans, fishing rods, trash cans, and specialized equipment.
*   💍 **`ring`**: Rings, bands, amulets, and special accessories.
*   🥾 **`boots`**: Boots, shoes, sneakers, and other footwear.
*   📖 **`book`**: Skill books, catalogues, guides, and manuals.
*   🐟 **`fish`**: Fishes and marine creatures.
*   🍰 **`food`**: Edibles, ingredients, crops, and cooking recipes.
*   📦 **`default`**: Fallback generic templates.

### 2. Food Dynamic Stats (`{{energy}}` & `{{health}}`)
Food items have template variables. The mod reads the Stardew Valley `Edibility` index of incoming crops or cooked foods and calculates their Energy and Health restoration stats in real-time:
*   Use `{{energy}}` and `{{health}}` anywhere in your custom food mail templates to render these values, like:
    > *"Hey @, I made this fresh {{item}} (+{{energy}} Energy / +{{health}} Health) for you!"*

### 3. Dynamic i18n Replacements
Translate complex game terms dynamically with placeholders, including:
*   **Progressive Upgrades**: `"buildings.free.name": "{{name}} (grátis)"` (Portuguese) or `Free {{name}}` (English).
*   **Skill Formats**: Custom templates for skill-level unlocks, recipes, and dynamic location rewards.

### 4. Optimization Details
*   **Cache Mapping**: Item, location, bundle, and description lookups are cached to prevent gameplay delays.
*   **Background Pre-Scouting**: Queries the Archipelago server for location data in a background thread upon save load to minimize menu lag in large shops.
*   **Memory Monitor**: A console command (`ap_pt_mem`) allows you to view the cache entry count and estimated memory footprint.

---

## Directory Structure

All translations and customized mail assets are stored inside structured folders:

```text
StardewArchipelagoTranslations/
├── i18n/
│   ├── default.json             # Core English fallback terms
│   └── pt.json                  # Core Portuguese localization terms
├── templates/
│   └── mail/
│       └── pt/                  # Folder per language locale
│           ├── default.json     # Generic fallback templates
│           ├── food.json        # Edibles with dynamic {{energy}} and {{health}}
│           ├── weapon.json      # Combat & cave templates
│           ├── tool.json        # Farm & heavier equipment templates
│           └── ring.json        # Jewelry & protection templates
```

---

## How to Customize Mail Templates

To write your own translations or messages:
1. Go to `templates/mail/{your_locale}/` (create the directory matching your SMAPI locale code if it doesn't exist).
2. Create/edit `{category}.json` with the following structure:
```json
{
  "ItemMails": [
    "Template message for receiving a {{item}} from {{sender}} found at {{location}}!^Enjoy it on the {{farm}} farm."
  ],
  "GiftMails": [
    "Template message for a gift {{item}} sent by {{sender}} playing {{game}}!^Attributes: +{{energy}} Energy / +{{health}} Health."
  ]
}
```

### Supported Tokens:
*   `{{item}}`: The fully localized name of the item.
*   `{{sender}}`: The player who sent or found the item.
*   `{{location}}`: The fully localized location where the item was found.
*   `{{farm}}`: The player's active farm name.
*   `{{game}}`: The sender's game name (useful in Gift mails).
*   `{{energy}}`: Real-time energy value (available for **`food`** category only).
*   `{{health}}`: Real-time health value (available for **`food`** category only).

Legacy templates using `{0}`, `{1}`, `{2}`, `{3}` format indexes are also supported and resolve automatically.

---

## Commands

Run these commands in your SMAPI console:
*   **`ap_pt_mem`**: Displays the active cache entry count and estimated physical RAM size of the translation mod.

---

## License

```
Copyright (c) 2026 Cristiano Nascimento
GitHub: CPDNCRISTIANO

All Rights Reserved.

Permission is granted to use this mod for personal, non-commercial purposes only.

You may not:
- Redistribute this mod in any form.
- Upload or publish this mod on any website, platform, or repository without prior written permission from the author.
- Modify and redistribute this mod.
- Use any part of this mod's source code, assets, or content in other projects.
- Sell, sublicense, or otherwise commercially exploit this mod.

This mod is provided "as is", without warranty of any kind.

By using this mod, you agree to these terms.
```
