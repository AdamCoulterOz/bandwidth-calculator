# Repository History

## 2026-08-01: Publish the repository and static calculator

- Established `AdamCoulterOz/bandwidth-calculator` as the public source repository with `main` as its default branch.
- Added a single-job GitHub Pages workflow that restores, tests, publishes, rewrites the project-site base path, uploads, and deploys the Blazor WebAssembly output.
- Added a static `404.html` fallback for client-side routes and documented the public calculator at `https://adamcoulteroz.github.io/bandwidth-calculator/`.

## 2026-08-01: Adopt UnitsNet information-rate relations

- Upgraded UnitsNet from `5.75.0` to `6.0.0-pre021`.
- Replaced manual `.BitsPerSecond`, `.Hertz`, and `.Seconds` arithmetic and quantity reconstruction with generated relations for information, bitrate, frequency, and duration.
- Kept primitive extraction only where the external contract is scalar, including nominal refresh matching and DSC quarter-bit rounding.
- Established `CONTEXT.md`, `HISTORY.md`, and `INTERFACE.md`. Earlier repository history is unavailable because this checkout has no Git metadata.

## 2026-08-01: Correct timing and introduce capability-based interface evaluation

- Replaced the monolithic calculator with separate validation, timing, bandwidth orchestration, capability, and transport-evaluation components.
- Corrected ordinary CVT, RBv2 width granularity/video-rate handling, DMT identifiers/data, CTA fractional/interlaced selection, and derived-value validation.
- Added CVT Reduced Blanking v3 and exposed resolved timing, scan mode, frame/field frequency, and optional DMT/CTA timing identity.
- Replaced interface-version assumptions with explicitly labelled reference profiles plus a caller-supplied `InterfaceConfiguration` path for negotiated capabilities.
- Added HDMI 2.2, explicit two/four-lane DisplayPort Alt Mode, official DisplayPort payload figures, transport-specific HDMI handling, and downward 1/16-bpp DSC selection with format-specific quality floors.
- Renamed the colour model to the standard `YCbCr` terminology and made the DSC breakdown's minimum-floor meaning explicit.
- Expanded regression coverage from eight saved RBv2 examples to independent timing, interface-boundary, validation, fractional-rate, ambiguity, overflow, and saved-scenario tests.
- Added CTA-861-I Optimized Video Timing with the published default parameter set, exact/fractional rate semantics, and independent reference-algorithm vectors.
- Made blanking treatment a transport-owned invariant and rejected cross-family or internally contradictory custom capability graphs.

## 2026-08-01: Add standalone Blazor WebAssembly calculator

- Added `BandwidthCalculator.Web` as a .NET 10 sibling project in the solution with a direct reference to the calculator library.
- Implemented a responsive, accessible Razor UI for all calculation inputs, timing options, bandwidth outputs, resolved timing details, and interface evaluations.
- Preserved the simple reference-profile workflow and added a collapsed, concise exact-capability editor for DisplayPort, HDMI, and USB-C DisplayPort Alt Mode.
- Kept all semantic calculation in the library assembly; the web project translates form state to public contracts and formats results only.
- Added explicit invalid-input and calculation-error states so stale results are not presented as current.

## 2026-08-01: Add adaptive light and dark themes

- Converted the WebAssembly UI palette to semantic colour tokens with complete light and dark definitions.
- Made the UI and native form controls follow the operating-system `prefers-color-scheme` setting without an application-owned override or persistence lifecycle.
- Replaced the stock framework tab icon with a calculator-specific display and bidirectional data-flow mark.

## 2026-08-01: Add linked display geometry and contextual configuration guidance

- Added a concise named-resolution catalogue spanning 720p through 8K, with typical current choices identified in basic selectors.
- Extended the catalogue with documented Apple MacBook, iMac, Studio Display, Pro Display XDR, and Vision Pro Mac Virtual Display rasters, preserving Apple's distinct Wide and Ultrawide naming.
- Linked width, height, and exact aspect-ratio inputs with an explicit lock: locked dimension edits preserve the ratio, while unlocked edits preserve the other dimension and derive a new ratio.
- Added unit-switchable physical display diagonal and derived PPI as presentation-only viewing geometry; identified hardware presets populate their published diagonal while generic and virtual modes preserve the current value.
- Added an annotated proportional screen diagram comparing pixel dimensions and area with an always-visible 4K UHD reference overlay; exact dimensions stay on the measurement annotations and recognised modes use only a concise common name inside the image.
- Added accessible hover/focus guidance for every configuration field, including typical ranges and adoption or legacy context.
- Constrained form and control widths by content so stacked and wide layouts do not stretch short-value inputs unnecessarily.

## 2026-08-01: Make the UI continuously reactive and clarify compatibility results

- Made PPI editable as a bidirectional geometry control that preserves physical diagonal and aspect ratio while rounding both derived dimensions to whole pixels.
- Removed the Calculate action and recalculated synchronously after every valid input edit; invalid intermediate form state now clears stale results until corrected.
- Removed repeated `reference profile` text from built-in profile labels and moved the theoretical-bundle explanation above Link compatibility.
- Reduced built-in labels to version numbers, moved each bundle's available capabilities behind an information control, and compacted each interface family into a wrapping line of profile chips below its heading.
- Removed repeated visible `Native` and `Unsupported` labels because the keyed colour and shape already communicate state; chips now show only the selected capability or a short title-cased limitation.
- Added typed profile summaries, selected-link summaries, failure categories, and DSC compression ratios to interface evaluations while preserving exact DSC bpp and detailed failure reasons for programmatic and accessible use.
- Presented DSC as an effective compression ratio beside the status symbol, with exact source/selected bpp and the 1/16-bpp precision rule on hover or keyboard focus.
- Made every displayed link method, compression ratio, and limitation a hover/focus explanation target with transport-specific context.
- Added viewport-aware horizontal positioning for profile-capability and status-detail tooltips so wrapped edge chips remain readable.
- Defined green compatibility fill as the lowest listed bundle that can carry the current signal, while retaining separate Native, DSC, and Unsupported status labels.
- Reused the canonical favicon SVG as the page-title brand mark.
- Bounded each link family visually and collapsed unsupported profile chips by default behind an accessible per-family disclosure control.
- Removed the redundant bottom status key after the profile chips became self-explanatory through colour, shape, capability text, and assistive labels.
- Made viewport clamping repeat-stable by deriving each tooltip shift from its unshifted anchor instead of compounding or resetting an earlier shift.
- Moved each profile-capability tooltip from a separate information icon onto the dotted-underlined version label itself.
- Replaced coloured capability text with neutral regular-weight labels and confined status colour to filled-circle native/DSC markers or the unsupported prohibition symbol.
- Replaced the header-level unsupported disclosure with an inline muted summary chip and a minimal trailing collapse control.
- Removed the lowest-compatible green chip fill and changed interface-family containers to borderless neutral-grey panels distinct from the profile chips.

## 2026-08-01: Consolidate result hierarchy and contextual guidance

- Moved Link compatibility directly below the bandwidth summary and placed resolved Timing details after it.
- Replaced the permanent compatibility introduction with a hover/focus explanation on the dotted-underlined Link compatibility title.
- Removed standalone information-icon triggers and moved configuration guidance directly onto dotted-underlined field labels, retaining keyboard access and viewport-clamped tooltips.

## 2026-08-01: Remove redundant section chrome

- Removed the visible Configuration, Bandwidth summary, Link compatibility, and Timing details headings while retaining accessible region labels.
- Removed the result-section divider rules and the Link compatibility title tooltip, relying on metric cards, compatibility-group surfaces, timing-row rules, and whitespace for hierarchy.

## 2026-08-01: Compact typical-choice markers

- Replaced repeated `Typical` option suffixes with a single `★` marker and documented its meaning in the associated field-label guidance.
- Replaced the star with a neutral `•` after visual review so the marker indicates commonality without implying recommendation or preference.

## 2026-08-01: Separate the configuration surface

- Shifted dark-mode large-area theme surfaces from blue-tinted colours to neutral greys while retaining blue as the brand and interaction accent.
- Gave the complete configuration column a dedicated system-adaptive surface darker than the page header, preserving the surface across wide and stacked responsive layouts.
- Extended that surface through the centered app-shell gutter to the viewport's left edge and replaced resting field borders with fill contrast while retaining invalid and keyboard-focus affordances.
- Made the stacked responsive configuration section consume both shell gutters so its surface reaches both viewport edges while its content alignment remains unchanged.
- Removed the timing/exact-options panel outlines and internal disclosure rules, plus the vertical and stacked horizontal separators between configuration and results.
- Removed the screen diagram's outer background and border while retaining the annotated SVG, active-image fill, and reference legend.
- Centred the screen diagram's wrapping legend beneath the SVG.
- Removed persistent dotted underlines from tooltip-bearing terms, retaining discoverability through the help cursor, transient hover/focus colour, keyboard focus outline, and unchanged tooltip behavior.
- Moved the ratio-lock control beside the Aspect ratio label as a compact borderless outline icon, moved its persistent behavior text into a state-aware tooltip, and suppressed native number-input spinner controls across Safari/WebKit and Firefox-compatible engines.
- Removed profile-chip outer strokes, strengthened disabled-control fill contrast, moved physical-geometry helper text into its field tooltips, and shortened the DisplayPort Alt Mode family heading to `USB-C` with the full meaning available contextually.
- Renamed the resolution selector to `Preset resolutions` and moved the centred live screen diagram to the top of the configuration flow.
- Replaced flat tooltip prose with a shared titled lead-and-bullets presentation across field, lock, compatibility, and collapse guidance; moved static unit suffixes inside their control shells and shortened pixel labels to `px`.
- Renamed the physical-geometry labels to `Display size` and `Density`, and limited displayed frame/field frequencies to three decimal places without reducing calculation precision.
- Compacted the page header, hid its subtitle at the stacked breakpoint, restyled Reset as a filled rounded pill, and reduced the gap before the leading screen diagram.
- Delayed the configuration/results stack until 960px so compact-desktop layouts retain two useful columns instead of stretching the configuration form across the page.
- Removed inactive positioned tooltips from layout to prevent their invisible boxes from introducing horizontal scrolling at narrow widths.
- Added a dedicated 760–960px configuration projection with the diagram and fields side by side and paired disclosure panels, instead of merely stretching the desktop form before the phone layout.
- Rebalanced the geometry row toward the pixel dimensions, merged both ratio inputs into one compact compound field with an internal colon, aligned the physical and basic field tracks, and inset select chevrons consistently.
- Regrouped the six primary format controls into two aligned three-column rows and combined display size with its interactive unit selector in one compound field.
- Removed active outlines from editable fields in favour of focus-fill feedback, expanded the preset selector to the form width, replaced its native popup with a padded rounded listbox and subtle selection treatment while preserving descriptive labels, and grouped colour Depth / Encoding / Sampling under one shared label.
- Kept both three-control configuration groups on single full-width rows across supported viewports, removed the visible Colour heading row while retaining its accessible group name, and softened the scale and contrast of field labels in both colour schemes.
- Clarified the first colour-control label as `Color depth`, leaving the adjacent controls as the concise `Encoding` and `Sampling`.
- Replaced the remaining native select elements with one reusable accessible custom-listbox control, extending the Preset resolutions menu treatment to display units, colour configuration, timing, and exact link capabilities.
- Tightened the Color depth control so it cannot crowd Encoding and changed the visible Density value and step to whole-number PPI while retaining nearest-pixel geometry updates.
- Moved typical markers to reserved leading option columns and split preset-resolution entries into aligned marker, dimensions, and name columns.
- Unified the configuration surface with the connector-capability group colour token and corrected its stacked full-bleed sizing so both viewport edges are painted without horizontal scrolling.
- Restored equal Color depth / Encoding / Sampling field tracks, removed common markers from closed selected values, and right-aligned the selected display-size unit beside its chevron.
- Reduced configuration field labels to a regular, smaller treatment and tightened their vertical spacing to the associated controls.
- Compacted the smallest colour-control triggers so two-digit depth and `YCbCr` remain visible, while allowing only the Encoding option popup to widen beyond its closed field.
- Increased field, option, and disclosure-summary interaction contrast by deriving their shared hover/focus fill from the theme blue and connector/configuration group surface.
- Expanded USB-C family and profile tooltip titles to the unambiguous `USB-C DisplayPort Alt Mode`, appending the profile version only to per-version tooltips while retaining the concise visible `USB-C` heading.
