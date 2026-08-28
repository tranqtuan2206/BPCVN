---
name: pop-neo-brutalist-ui
description: Playful, high-contrast, tactile web interfaces fusing vibrant pop/pastel palettes with heavy black outlines, hard offset drop shadows, pill-shaped geometry, and retro/pixel-art accents.
---

# SKILL: Pop Neo-Brutalism & Playful Tactile UI

## 1. Skill Meta
**Name:** Pop Neo-Brutalism & Playful Tactile Interface Engineering
**Description:** Advanced proficiency in architecting digital environments that merge the playful energy of Y2K retro culture, candy/pastel color dynamics, and tactile physical affordances with modern frontend precision. This discipline replaces cold military/industrial austerity with approachable, vibrant design: heavy black borders, unblurred hard drop shadows, pill-shaped and rounded container geometry, high-contrast typography, and optional retro/pixel-art decorative elements.

## 2. Visual Archetypes
Pick ONE primary mood per project to ensure aesthetic consistency throughout the interface.

### 2.1 Pop Pastel & Candy Neo-Brutalism
*   **Characteristics:** Light, optimistic canvas substrates (lavender, soft cream, pale pink). High-saturation candy accent colors (pink, lime green, bright yellow, cyan). Heavy use of rounded pill shapes (`border-radius: 9999px`) for buttons and badges. Extremely tactile 3D button press states.

### 2.2 Retro-Pixel & Toy-Box Neo-Brutalism
*   **Characteristics:** Merges Neo-Brutalism with 8-bit/16-bit retro video game aesthetics. Pixel-art mascots, 8-bit header typography, starburst badges, and polka-dot/grid canvas backgrounds alongside heavy black outlines and solid shadows.

## 3. Typographic Architecture

### 3.1 Macro-Typography (Headers & Titles)
*   **Classification:** Bold Display Sans-Serif or Retro Pixel Display.
*   **Optimal Web Fonts:** Space Grotesk, Syne, Archivo Black, Plus Jakarta Sans, Outfit, or Press Start 2P / Silkscreen (for retro/pixel headings).
*   **Implementation Parameters:**
    *   **Scale:** Massive, punchy headlines (`clamp(2.5rem, 6vw, 8rem)`).
    *   **Weight:** Extra Bold (800) or Black (900).
    *   **Casing:** Uppercase or Title Case for energetic impact.

### 3.2 Body & Micro-Typography
*   **Classification:** Friendly Geometric Sans or Crisp Monospace.
*   **Optimal Web Fonts:** Plus Jakarta Sans, Inter, Space Mono, JetBrains Mono.
*   **Implementation Parameters:**
    *   **Weight:** Medium (500) to Semi-Bold (600) for maximum readability against thick borders.
    *   **Tracking:** Standard (`0em`) to slightly generous for monospace elements.

## 4. Color System & Substrates
The color palette balances soft background canvases with high-energy pop accents. All interactive elements are anchored by thick pure black borders (`#000000`).

### 4.1 Substrate Canvas (Backgrounds)
*   **Lavender:** `#E9D5FF` / `#D8B4F8`
*   **Soft Cream / Off-White:** `#FFFDF7` / `#F7F4ED`
*   **Pale Pink:** `#FFD1DC` / `#FFE4E6`

### 4.2 Pop Accent Palette
*   **Hot / Pastel Pink:** `#FF70A6` / `#FF85A1`
*   **Lime Green:** `#C4F538` / `#A3E635`
*   **Sunny Yellow / Orange:** `#FFD166` / `#FFB703`
*   **Cyan / Sky Blue:** `#70D6FF` / `#38BDF8`
*   **Ink & Border Color:** `#000000` (Absolute Pure Black).

## 5. Geometry, Borders & Tactile Mechanics

### 5.1 Border Radius (Mandatory Shapes)
*   **Buttons & Badges:** Full Pill Shape (`border-radius: 9999px` / `rounded-full`).
*   **Cards & Containers:** Soft Rounded Corners (`16px` to `24px` / `rounded-2xl`).
*   **Input Fields:** Rounded Pill or `12px` rounded rectangles.

### 5.2 Outlines & Hard Drop Shadows
*   **Border Thickness:** `2px` to `4px` solid `#000000`.
*   **Hard Drop Shadow:** Unblurred $45^\circ$ solid offset shadow (`box-shadow: 4px 4px 0px #000000` or `6px 6px 0px #000000`).

### 5.3 Tactile Interaction States (Crucial)
*   **Default State:** `translate(0, 0)` with `box-shadow: 4px 4px 0px #000`.
*   **Hover State:** `translate(-2px, -2px)` with `box-shadow: 6px 6px 0px #000`.
*   **Active / Clicked State:** Physical push-down effect `translate(4px, 4px)` with `box-shadow: 0px 0px 0px #000` (shadow collapses to zero).

## 6. Decorative Assets & Pixel Art Integration
*   **Floating Badges:** Starburst tags (`30% off!`, `NEW`), pill status indicators, pixel-art icons.
*   **Substrate Patterns:** Polka-dot background patterns using CSS radial gradients (`radial-gradient(#000 1.5px, transparent 1.5px)` with `background-size: 20px 20px`).
*   **Pixel-Art Harmony:** Blend pixel art avatars, mascots, or UI icons cleanly inside solid-bordered cards.

## 7. Web Engineering Directives (Tailwind CSS Reference)

### 7.1 Neo-Brutalist Pill Button Component
```html
<button class="px-6 py-3 bg-[#FF70A6] text-black font-extrabold rounded-full border-3 border-black shadow-[4px_4px_0px_0px_rgba(0,0,0,1)] transition-all duration-150 hover:-translate-x-0.5 hover:-translate-y-0.5 hover:shadow-[6px_6px_0px_0px_rgba(0,0,0,1)] active:translate-x-1 active:translate-y-1 active:shadow-[0px_0px_0px_0px_rgba(0,0,0,1)]">
  Click Me
</button>
<div class="p-6 bg-[#C4F538] text-black rounded-2xl border-3 border-black shadow-[6px_6px_0px_0px_rgba(0,0,0,1)]">
  <h3 class="text-2xl font-black uppercase mb-2">Card Title</h3>
  <p class="font-medium">Card content goes here with high-contrast text.</p>
</div>