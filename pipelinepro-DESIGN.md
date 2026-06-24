# PipelinePro

Confident, structured, pipeline-obsessed.

## Overview

PipelinePro is a design system engineered for CRM and sales pipeline management tools where deals flow through stages and every interaction counts. The design language is bold and structured — strong indigo anchors convey authority, while cyan and orange accents highlight motion and urgency across kanban boards, tables, and deal cards. Standard density with a compact 4px base keeps layouts tight enough for data-rich views yet readable enough for extended use. Built for sales teams who think in funnels, stages, and close rates.

## Colors

- **Primary** (#4F46E5): Primary actions, active pipeline stages, key CTAs
- **Secondary** (#06B6D4): Hyperlinks, secondary highlights, deal value accents
- **Tertiary** (#F97316): Urgency markers, due-soon indicators, hot leads
- **Background** (#FAFAFA): App-level canvas
- **Surface** (#FFFFFF): Cards, modals, deal panels
- **Success** (#22C55E)
- **Warning** (#F59E0B)
- **Error** (#EF4444)
- **Info** (#4F46E5)

## Typography

- **Headline Font**: Outfit
- **Body Font**: Inter
- **Mono Font**: Source Code Pro

- **Display**: Outfit 52px bold, 1.1 line height, 0.02em tracking. Revenue hero numbers.
- **Headline**: Outfit 38px bold, 1.2 line height, 0.015em tracking. Page headings, pipeline titles.
- **Subhead**: Outfit 26px semibold, 1.3 line height, 0.01em tracking. Stage headers, section titles.
- **Body Large**: Inter 18px regular, 1.6 line height. Lead paragraphs, deal summaries.
- **Body**: Inter 15px regular, 1.6 line height. Default body text.
- **Body Small**: Inter 14px regular, 1.5 line height. Table cells, card metadata.
- **Caption**: Inter 12px medium, 1.4 line height, 0.01em tracking. Timestamps, stage counts, labels.
- **Overline**: Inter 11px bold, 1.2 line height, 0.09em tracking. Pipeline stage names, deal tags (uppercase).
- **Code**: Source Code Pro 14px regular, 1.5 line height. API keys, integration IDs, formulas.

## Spacing

- **Base unit:** 4px
- **Scale:** 0, 4, 8, 12, 16, 20, 24, 32, 40, 48, 64, 80
- **Component padding:** 8px (small), 12px (medium), 16px (large)
- **Section spacing:** 24px (mobile), 40px (tablet), 56px (desktop)

## Border Radius

- **None:** 0px — Table cells, inline data badges
- **Small:** 4px — Small tags, status indicators
- **Medium:** 8px — Cards, buttons, inputs, modals
- **Large:** 12px — Feature panels, onboarding dialogs
- **XL:** 20px — Promotional cards, empty-state illustrations
- **Full:** 9999px — Avatars, pill badges, stage dots

## Elevation

PipelinePro uses Material-style layered shadows to create a clear visual hierarchy between pipeline columns, deal cards, and overlay surfaces.
- **Subtle:** 1px offset, 2px blur, #18181B at 5%
- **Medium:** 4px offset, 6px blur, -1px spread, #18181B at 7%; 2px offset, 4px blur, -2px spread, #18181B at 5%
- **Large:** 10px offset, 15px blur, -3px spread, #18181B at 8%; 4px offset, 6px blur, -4px spread, #18181B at 4%
- **Overlay:** 20px offset, 25px blur, -5px spread, #18181B at 12%; 8px offset, 10px blur, -6px spread, #18181B at 6%
- **Drag:** 12px offset, 24px blur, -4px spread, #4F46E5 at 15% — used on deal cards while being dragged

## Components

### Buttons
- **Primary (Filled)**: #4F46E5 fill, #FFFFFF text, 8px corners. Inter 14px 600. 8px/18px padding. Hover: background shifts to #4338CA. Active: background shifts to #3730A3, scale 0.98.
- **Secondary (Outline)**: transparent, #4F46E5 text, 1px #4F46E5 border, 8px corners. 8px/18px padding. Hover: background fills #EEF2FF.
- **Ghost**: transparent, #71717A text. Hover: background fills #F4F4F5, text shifts to #18181B.
- **Destructive**: #EF4444 fill, white text. Hover: background shifts to #DC2626.
- **Sizes**: Small (32px), Medium (38px), Large (46px)
- **Disabled**: 40% opacity, disabled cursor

### Cards
- **Default**: #FFFFFF fill, 1px #E4E4E7 border, 8px corners. 16px padding. Hover: border color shifts to #D4D4D8.
- **Elevated**: Medium shadow. Hover: shadow transitions to Large, translateY(-2px).

### Inputs
- **Text Input**: #FFFFFF fill, 1px #E4E4E7 border, #18181B text, 8px corners. Inter 14px. #A1A1AA placeholder, 8px/12px padding, 38px tall. Focus: border #4F46E5, ring 3px ring #4F46E5 at 12%. Error: border #EF4444, message #EF4444. Disabled: background #FAFAFA, text 50% opacity.
- **Label**: Above input, Inter, 13px, 500, #3F3F46
- **Helper text**: 12px, #71717A

### Chips
- **Filter Chip**: 8px corners, 1px #E4E4E7 border. 13px 500. 30px tall, 10px/horizontal padding. Selected: background #4F46E5, text #FFFFFF, border transparent. Hover: background #F4F4F5.
- **Status Chip**: background #F0FDF4, text #16A34A, border #BBF7D0 won, background #FFF7ED, text #EA580C, border #FED7AA at risk, background #FEF2F2, text #DC2626, border #FECACA lost.

### Lists
- **Default List Item**: Inter 14px. 44px tall, 10px/12px padding, 1px #F4F4F5 divider, 18px icon, 10px spacing from text with icon. Hover: background #FAFAFA. Selected: background #EEF2FF, text #4F46E5, left border 2px #4F46E5.

### Checkboxes
16px, 1.5px #D4D4D8 border, 4px corners. Checked: background #4F46E5, white checkmark. Indeterminate: background #4F46E5, white dash. Disabled: 40% opacity. Labels in Inter 14px 8px spacing from box.

### Radio Buttons
16px outer circle, 1.5px #D4D4D8 border. Selected: border #4F46E5, inner dot 8px #4F46E5. Disabled: 40% opacity. Labels in Inter 14px 8px spacing from circle.

### Tooltips
#18181B fill, #FAFAFA text, 6px corners. 12px 500. 6px/10px padding, 240px max width, 6px arrow, 300ms delay, top (default) position.

## Do's and Don'ts
- Do use pipeline stage colors consistently across kanban boards, tables, and reports
- Do visually distinguish deal card states — use left border color to encode stage at a glance
- Do highlight overdue tasks and stale deals with the tertiary orange, never with red
- Don't use more than four pipeline stages visible simultaneously without horizontal scrolling
- Don't display monetary values without proper currency formatting and locale awareness
- Don't animate deal card transitions longer than 200ms — speed conveys confidence
- Do show deal count and total value in every pipeline stage header
- Don't mix kanban and list views on the same page — let users toggle, not split