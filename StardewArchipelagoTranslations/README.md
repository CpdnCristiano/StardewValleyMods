# Stardew Archipelago Translations

A high-performance, premium, and standalone multilingual localization and patcher mod for [StardewArchipelago](https://github.com/KaitoKid/StardewArchipelago).

This mod utilizes Harmony patches to intercept, translate, and augment core StardewArchipelago behaviors in real-time, providing players with beautiful localized content, custom i18n rules, and an advanced modular mail translation system.

---

## 🚀 Key Features

### 1. Granular Modular Mail System
Tired of generic mails (like Hat Mouse or Burger King jokes) showing up on every item? This mod introduces a granular category-based mail system. 
Mails are loaded dynamically based on the item's category from `templates/mail/{locale}/{category}.json`. Supported categories include:
*   ⚔️ **`weapon`**: Swords, daggers, hammers, clubs, gauntlets, and other combat items.
*   🪓 **`tool`**: Axes, pickaxes, hoes, watering cans, fishing rods, trash cans, and specialized equipment.
*   💍 **`ring`**: Rings, bands, amulets, and special accessories.
*   🥾 **`boots`**: Boots, shoes, sneakers, and other footwear.
*   📖 **`book`**: Skill books, catalogues, guides, and manuals.
*   🐟 **`fish`**: Fishes and marine creatures.
*   🍰 **`food`**: Edibles, ingredients, crops, and cooking recipes.
*   📦 **`default`**: Fallback generic templates.

### 2. 🍲 Food Dynamic Stats (`{{energy}}` & `{{health}}`)
Food items now have **exclusive template variables**! The mod dynamically reads the Stardew Valley `Edibility` index of incoming crops or cooked foods and calculates their exact **Energy** and **Health** restoration stats in real-time:
*   Use `{{energy}}` and `{{health}}` anywhere in your custom food mail templates to render real-time values, like:
    > *"Hey @, I made this fresh {{item}} (+{{energy}} Energy / +{{health}} Health) for you!"*

### 3. 🗺️ Smart Dynamic i18n Replacements
Translate complex game terms dynamically with elegant placeholders, including:
*   **Progressive Upgrades**: `"buildings.free.name": "{{name}} (grátis)"` (Portuguese) or `Free {{name}}` (English).
*   **Skill Formats**: Custom templates for skill-level unlocks, recipes, and dynamic location rewards.

### 4. ⚡ Extreme Optimization
Built for professional performance:
*   **Dynamic Cache Mapping**: All item, location, bundle, and description lookups are heavily cached to guarantee **zero gameplay stutter**.
*   **Background Pre-Scouting**: Automatically queries the Archipelago server for location data in a non-blocking background thread as soon as the save is loaded. **This completely eliminates the menu-open lag** experienced in large shops!
*   **Ultra-light memory footprint**: Runs a custom real-time memory monitor command (`ap_pt_mem`) verifying a physical RAM impact of less than 0.01%!

---

## 📂 Directory Structure

All translations and customized mail assets are stored inside clean, structured folders:

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

## 🛠️ How to Customize Mail Templates

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

Legacy templates using `{0}`, `{1}`, `{2}`, `{3}` format indexes are also supported out-of-the-box and will automatically resolve with zero compatibility issues!

---

## 👨‍💻 Commands

Run these useful commands directly in your SMAPI console:
*   **`ap_pt_mem`**: Evaluates and displays the exact physical RAM size and dynamic cache entry count of the translation mod in real-time.

---

## 🤝 Contributing & Support

Maintained with ❤️ by **CpdnCristiano** and contributors.
Feel free to open a Pull Request to translate terms to other languages by creating a folder under `templates/mail/` and translating `i18n/` entries!
