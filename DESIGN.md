# Design System Inspired by Neo-Brutalism

## 1. Visual Theme & Atmosphere

The BPCVN website employs a neo-brutalist aesthetic, characterized by high contrast, bold outlines, flat colors, and stark geometric shapes. It strips away the soft gradients and subtle shadows of corporate web design, replacing them with thick borders, sharp edges, and unyielding dropshadows. 

This design choice resonates with the mechanical keyboard community: it feels engineered, physical, and unapologetically raw.

**Key Characteristics:**
- High-contrast 2-accent palette: Yellow (`#FFD700`) and Ink Black (`#1a1a1a`).
- Hard shadows without blur (`4px 4px 0 var(--nb-ink)`).
- Zero border radius globally (`0px`), creating a sharp, blocky appearance.
- Thick, solid borders (`3px solid var(--nb-ink)`) separating elements clearly.
- Typography that pairs a pixel-art logo with modern sans-serif fonts (Inter).
- Flat, non-transparent surfaces avoiding glassmorphism or soft blurs.

## 2. Color Palette & Roles

### 2-Accent System
- **Background (Light Mode)**: `#FFFDF0` (Cream/White)
- **Ink (Light Mode)**: `#1a1a1a` (Black)
- **Background (Dark Mode)**: `#121212` (Dark Gray)
- **Ink (Dark Mode)**: `#FFFDF0` (Cream/White)
- **Primary Accent**: `#FFD700` (Yellow) — used for backgrounds of featured cards, active states, and CTA buttons in dark mode.
- **CTA Accent**: `#1a1a1a` in light mode, `#FFD700` in dark mode.

### Contrast Rules (AA/AAA Compliant)
- **Yellow Accent**: Always paired with `#1a1a1a` text. Never use white/cream text on yellow backgrounds due to poor contrast.
- **Backgrounds**: The ink color serves as the text and border color for its respective mode to guarantee maximum contrast (AAA).

## 3. Typography Rules

### Font Family
- **Primary**: `Inter`, with fallbacks `-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif`
- **Logo/Brand**: `Determination` (Pixel font)

### Weights
- **Headings**: 800 (Extra Bold) or 900 (Black) for extreme impact.
- **Body**: 400 (Regular) to 700 (Bold) depending on context.

## 4. Component Stylings

### Buttons
- **Primary (CTA)**
  - Background: `var(--nb-cta)`
  - Text: `var(--nb-bg)` (Light mode) or `#1a1a1a` (Dark mode)
  - Border: 3px solid `var(--nb-ink)`
  - Shadow: 4px 4px 0 `var(--nb-ink)`
  - Hover: Background switches to `var(--nb-accent)`, text to `#1a1a1a`, shadow reduces to `2px 2px 0`, and the button translates down and right by 2px.

- **Ghost / Secondary**
  - Background: Transparent
  - Text: `var(--nb-ink)`
  - Border: 3px solid `var(--nb-ink)`
  - Shadow: 4px 4px 0 `var(--nb-ink)`
  - Hover: Background becomes `var(--nb-accent)`, shadow shifts.

### Cards & Containers
- Background: `var(--nb-bg)`
- Border: `3px solid var(--nb-ink)`
- Radius: `0px`
- Shadow: `4px 4px 0 var(--nb-ink)`
- Hover: Shadow reduces to `2px 2px`, translating the card slightly for tactile feedback.

### Inputs & Forms
- Background: `var(--nb-bg)`
- Text: `var(--nb-ink)`
- Border: `3px solid var(--nb-ink)`
- Radius: `0px`
- Focus: Removes default ring, switches background to `var(--nb-accent)`, keeping black text.

### Badges / Tags
- No rounded corners.
- Border: `2px solid var(--nb-ink)`
- Background: Semi-transparent ink or flat colors.

## 5. Layout Principles

### Spacing System
- Spacing relies on clear, distinct blocks. Padding inside containers is generous (e.g., 24px - 32px) to let the thick borders breathe.

### Depth & Elevation
- There are only two levels of elevation: flat on the surface, and raised with a hard shadow.
- Elevated elements (buttons, cards, dropdowns) cast a solid shadow: `4px 4px 0 var(--nb-ink)`.
- When interacted with (hover/active), the element moves physically closer to the surface, reducing the shadow and creating a satisfying mechanical click feel.

## 6. Do's and Don'ts

### Do
- Ensure all borders are `3px solid` and corners are `0px`.
- Use the 2-accent color system strictly. 
- Ensure high contrast.
- Embrace blocky, stark layouts.

### Don't
- Don't use `border-radius`.
- Don't use soft, blurred `box-shadow` or gradients.
- Don't use more than the defined accent colors.
- Don't use white text on the yellow accent.
