# Clean Desktop Workspace Design System

A reusable visual specification for professional, data-focused applications. It describes appearance and interaction patterns independently of any product, organization, business domain or implementation framework.

Measurements are logical UI units: device-independent units in desktop frameworks or CSS pixels on the web. Adapt them to the platform's scaling and accessibility settings.

## 1. Design direction

Build a compact, calm workspace with a strong navigation hierarchy and a large working area. Use restrained color, readable typography, subtle borders and simple line icons. Prefer useful data and controls over decorative content.

- Keep related actions and information together.
- Use one visually dominant primary action per working area.
- Make editable, calculated, selected, invalid and saved states distinguishable.
- Support light and dark appearances with equivalent information hierarchy.
- Use explicit labels and feedback; do not communicate meaning through color alone.
- Keep layouts clean without squeezing controls or reducing text to fit.

## 2. Layout structure

```text
Application bar       Product identity                         Window controls
Navigation strip      Primary sections             Save state | Theme toggle
Context row           Current section                Context selector | Create
Command strip         Related actions                     View-specific options
Notification region   Conditional validation or action feedback
Working surface       Table, editor, form or content cards
Status footer         Current state                         Contextual metadata
```

### Desktop proportions

| Element | Baseline |
|---|---|
| Application bar | 42 units high |
| Navigation strip | 40 units high |
| Status footer | 24 units high |
| Main content outer margin | 10–12 units |
| Toolbar padding | 10 horizontal, 5 vertical |
| Preferred large window | Up to 1440 × 880, constrained by available work area |
| Dense workspace minimum | Approximately 1000 × 600 when available |
| Context selector | Approximately 230 units wide, minimum height 32 |

Allow wide data surfaces to scroll horizontally. Allow navigation overflow when necessary. On smaller screens, wrap or collapse secondary actions and stack form fields; do not hide essential actions outside the viewport.

## 3. Color tokens

Use semantic tokens rather than scattering literal colors through screens.

### Foundation

| Token | Light | Dark | Purpose |
|---|---|---|---|
| `chrome` | `#111E35` | `#0D1420` | Application/caption bar |
| `canvas` | `#F4F6FA` | `#111925` | Background behind surfaces |
| `surface` | `#FFFFFF` | `#172234` | Cards, inputs and table body |
| `surface-header` | `#F7F9FC` | `#1D293B` | Table headers |
| `surface-alternate` | `#FAFBFD` | `#1B2738` | Alternating rows |
| `text-primary` | `#18243B` | `#E5ECF6` | Main text |
| `text-secondary` | `#52627A` | `#BECBDD` | Labels and supporting text |
| `text-muted` | `#64748B` | `#A8B8CE` | Metadata and helper text |
| `border` | `#E2E8F0` | `#35465D` | Standard outlines |
| `grid-line` | `#EDF1F6` | `#2A394E` | Table separators |
| `accent-text` | `#2458D3` | `#94B8FF` | Links, active labels and focus |
| `selection` | `#E7EFFF` | `#2D456C` | Selected cells/items |
| `active-navigation` | `#E8EFFD` | `#2D456C` | Active section |
| `calculated-surface` | `#F0F5FF` | `#1D2D47` | Derived/read-only values |
| `editable-emphasis` | `#EDF4FF` | `#21334F` | Important editable table columns |

### Status and actions

| Token | Light | Dark |
|---|---|---|
| `success-text` | `#247653` | `#8CD5AE` |
| `warning-text` | `#9A6700` | `#EDC775` |
| `error-text` | `#B42318` | `#FFAAA3` |
| `error-border` | `#C2413C` | `#FFAAA3` |
| `error-surface` | `#FFF2F0` | `#3B252D` |
| `saved-surface` | `#F3F8F5` | `#1C3029` |
| `saved-text` | `#407057` | `#9AD5B7` |
| `saved-border` | `#D4E2D9` | `#395B4D` |
| `primary-fill` | `#2458D3` | `#2458D3` |
| `danger-fill` | `#B42318` | `#B42318` |
| `on-filled-action` | `#FFFFFF` | `#FFFFFF` |

Separate accent **text** from primary **fill**. Lightening accent text in dark mode must not turn a filled button into a pale background with white text.

Avoid heavy shadows and decorative gradients. Establish hierarchy with spacing, borders, typography and surface contrast.

## 4. Typography

Use a clear system sans-serif. For Windows, prefer **Segoe UI Variable** with **Segoe UI** fallback; on other platforms, use the native UI font or a comparable sans-serif.

| Role | Size | Weight |
|---|---|---|
| Large page/dialog heading | 20–24 | Semibold |
| Application identity | 18 | Semibold |
| Section heading | 17 | Semibold |
| Body | 13 | Regular |
| Navigation and toolbar | 12 | Medium/Semibold |
| Table values | 12 | Regular |
| Labels/helper text | 11–13 | Regular |
| Footer/metadata | 10–11 | Regular |
| Emphasized numeric input | 15 | Regular/Medium |

Wrap explanatory text and column headers. Use single-line truncation for constrained contextual titles, with a way to inspect the full value when needed. Right-align comparable numbers and label units explicitly. Never shrink essential text simply to avoid scrolling.

## 5. Spacing and shape

Use a restrained spacing vocabulary: **4, 6, 8, 10, 12, 14, 16, 18 and 24** units.

- Standard button: minimum height 30, padding 10 × 5, radius 6.
- Primary form input: minimum height 36, padding 10 × 6.
- Compact input: minimum height 30, padding 8 × 4.
- Card: 1-unit border, radius 8, typically 12–18 units internal padding.
- Adjacent icon/label gap: 8 units.
- Form label to input: approximately 5–6 units.
- Standard section separation: 12–18 units.

Keep corners modest and consistent. Do not make dense data cells pill-shaped. Increase target sizes when adapting to touch input.

## 6. Buttons and action hierarchy

| Variant | Appearance | Use |
|---|---|---|
| Primary | Blue fill, white label/icon, semibold | Main commit/submit action |
| Secondary | Surface fill, subtle border, accent text | Create/open/supporting action |
| Quiet | Transparent background and border | Navigation, toolbar and low-emphasis actions |
| Destructive | Red fill, white label | Explicit destructive confirmation |
| Saved status | Soft green surface, check icon and text | Successful committed state |

Interaction baseline:

- **Hover:** clearer border or slight surface/opacity change.
- **Pressed:** stronger visual response; return immediately after release.
- **Keyboard focus:** visible accent outline, approximately 2 units thick.
- **Disabled:** reduced emphasis and appropriate cursor; never remove the label.
- **Saved/read-only status:** remain readable even when the control is disabled.

Use specific labels such as Save changes, Create item, Keep item and Delete item. Avoid vague confirmation labels when the consequence matters.

## 7. Navigation and window chrome

Use horizontal, icon-and-label section navigation. Active items receive an accent label and tinted background. Inactive sections use muted text on the normal surface.

Application identity sits at the left of the top bar. Desktop minimize, maximize/restore and close controls sit at the right and must remain clearly visible and keyboard accessible. A custom caption should preserve native dragging, resizing and window-management behavior.

Keep build numbers and technical metadata out of the primary heading; place them in a subtle footer or an About surface when needed.

Place a compact **32 × 32** theme toggle after the primary save/status action. Use moon/sun line icons and action labels such as “Switch to dark theme.” Switching appearance must retain the user's current work, focus where practical and undo history. Remember the preference locally; fall back to a usable default if the preference cannot be read.

## 8. Tables and spreadsheet surfaces

- Use thin separators, low-contrast alternating rows and wrapped semibold headers.
- Dense rows: approximately 28–30 units; form-oriented tables: approximately 34 units.
- Header height: approximately 50–54 units, increased for longer labels.
- Row-number/selection gutter: approximately 42 units.
- Freeze the smallest useful set of identifying columns.
- Keep search above the table and preserve horizontal and vertical scrolling.
- Use virtualization for large datasets where the framework supports it.
- Show calculated/read-only cells on a distinct surface without making their text faint.
- Selection must override calculated shading so the selected range stays visible.
- Focus uses an accent border; validation uses a field-specific error treatment.

For continuous-entry interfaces, show available blank rows without counting untouched placeholders as saved records. Preserve row position after a clear operation when spatial continuity is important.

Make whole-record and cell-range actions distinguishable. Copy/cut/paste must protect read-only columns, avoid partial destructive changes on failure and preserve native text-editing behavior inside an active editor. Support predictable undo/redo where edits are reversible.

## 9. Forms and detail workflows

Use cards to separate a compact input group from a larger related table or detail area. Align fields by meaning rather than making every input equally wide.

For complex entry windows:

1. Fixed page heading with a clear close/back action.
2. Small local navigation strip for entry versus history/list views.
3. Scrollable input/detail region.
4. Persistent summary and action footer outside the scrolling region.

Show live totals or derived summaries near the commit action. Highlight the few editable columns instead of coloring every cell. Move lengthy explanations into contextual help or a collapsed disclosure. Switching local views should retain unsaved input unless the user explicitly discards it.

History lists can use stretching cards with a tinted surface and accent border for selection. Enable edit/cancel actions only when appropriate for the selected item. Provide an empty-state message that explains the next useful step.

## 10. Input validation and feedback

Use inline, actionable feedback. Keep unknown or invalid text available for correction rather than silently replacing it.

- Do not announce unrelated missing required fields after every keystroke.
- Validate complete values at appropriate commit boundaries.
- Attach field-specific errors to the affected input instead of outlining an entire row.
- Combine error color with clear text and an accessible description.
- Preserve the user's work after a failed save.
- Distinguish local input drafts from committed changes.

Date controls should use one consistent display format appropriate to the audience. A typing mask may insert separators and normalize valid dates. Add a calendar affordance where direct selection helps. Valid fields should not show repetitive instructional popups; error tooltips should explain how to correct invalid input.

Status examples:

| State | Presentation |
|---|---|
| Unsaved work | Amber text with explicit unsaved label |
| Commit available | Enabled primary Save changes action |
| Successfully saved | Green check and Saved label |
| Read-only content | Clear Read-only label and protected inputs |
| Invalid action | Inline error surface with a specific explanation |
| Long-running action | Progress text/state and duplicate-action prevention |

## 11. Dialogs and destructive actions

Use content-height dialogs with minimal unused space, typically 440–560 units wide for small tasks. Constrain them to the available work area and scroll the body when necessary.

A destructive confirmation should contain:

- A clear question naming the action.
- The exact item/context being affected.
- A brief, accurate consequence statement, including whether undo is available.
- A safe Keep/Cancel action and a distinct red destructive action.

Initial focus and Escape should take the safe path. Avoid confirmation dialogs for routine reversible cell edits; reserve them for meaningful whole-item consequences.

## 12. Icons, imagery and output

Use one consistent family of simple line icons: approximately **16 × 16**, **1.5-unit strokes**, rounded line caps/joins and an **8-unit label gap**. Bind icon color to the associated text state. Use accessible names/tooltips on icon-only actions.

Keep product identity assets replaceable. A small geometric mark works in the application bar and window/taskbar icon. Do not embed a particular product name, logo or industry metaphor into reusable components.

Documents and printable output should use their own print-friendly styling: white backgrounds, dark text, repeated headings, clear units, aligned values, totals and page numbers. Dark application appearance should not produce dark paper backgrounds. Native operating-system dialogs may follow system appearance rather than the application's theme.

## 13. Accessibility and implementation guidance

- Preserve visible keyboard focus and logical tab order.
- Give every interactive control an accessible name.
- Pair color with labels, icons or text state changes.
- Check contrast for normal text, muted text, selected items and filled actions in both themes.
- Keep essential commands reachable at the smallest supported window size and at increased display scaling.
- Support system font fallback and respect platform scaling.
- Use semantic design tokens and shared component styles; avoid screen-specific hard-coded colors.
- Keep appearance changes independent of application data and business rules.
- Do not assume rendered screenshots establish screen-reader or accessibility conformance.

## 14. Reuse and review checklist

To apply this design to another application:

1. Define the semantic palette and light/dark mappings.
2. Implement typography, spacing, buttons, inputs, cards and table primitives.
3. Build the application/navigation/context/command/content/footer shell.
4. Replace generic section labels and identity assets with the new application's own content.
5. Implement consistent editable, calculated, selected, invalid, pending, saved and disabled states.
6. Add keyboard navigation, accessible names and safe destructive-action handling.
7. Review normal/minimum sizes, long labels, empty/populated/error states, light/dark themes and print output.
8. Test physical keyboard/mouse input, scaling, scrolling, assistive technologies and native dialogs on the target platform.

Use this specification as the visual reference. The choice of programming language, framework, database and business workflow is independent of the design system.
