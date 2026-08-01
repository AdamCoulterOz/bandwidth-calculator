# Repository Context

## Purpose and current state

BandwidthCalculator is a public .NET 10 repository containing a reusable calculator library, its verification suite, and a standalone Blazor WebAssembly UI for display timing, uncompressed/DSC video bandwidth, and reference DisplayPort, HDMI, and DisplayPort Alt Mode capability evaluation. The UI is published at `https://adamcoulteroz.github.io/bandwidth-calculator/`.

The default API remains deliberately simple: `BandwidthCalculator.Calculate(input)` resolves the requested timing and evaluates documented reference profiles. An overload accepts `InterfaceConfiguration` when the caller knows the negotiated link rate, lane allocation, DSC support, or pixel-format constraints.

The UI mirrors those two paths. Its default form calls the simple library overload; the collapsed exact-capabilities section constructs an explicit `InterfaceConfiguration` without duplicating calculator policy in the web project.

The implementation targets UnitsNet `6.0.0-pre021`. Information, bitrate, frequency, and duration formulas remain quantity-aware; scalar extraction is limited to standard-defined numeric boundaries such as timing identifiers, fractional-rate recognition, and DSC bit-per-pixel quantization.

## Architecture

- `BandwidthCalculator/Models.cs` owns public calculation, timing, and result contracts plus internal timing records.
- `BandwidthCalculator/BandwidthCalculator.cs` orchestrates validation, timing, bandwidth, and interface evaluation.
- `BandwidthCalculator/InputValidator.cs` rejects invalid semantic combinations, non-finite values, and unrepresentable timing geometry.
- `BandwidthCalculator/DisplayTimingCalculator.cs` implements CVT, DMT, CTA VIC, and CTA Optimized Video Timing policy.
- `BandwidthCalculator/BandwidthTimingDatabase.g.cs` contains the DMT 1.13 timing set and CTA timing rows/VIC mapping.
- `BandwidthCalculator/InterfaceCapabilities.cs` owns reference presets and explicit capability configuration.
- `BandwidthCalculator/InterfaceEvaluator.cs` owns transport-specific native and DSC selection.
- `BandwidthCalculator.Web` is a standalone .NET 10 Blazor WebAssembly project that references the library directly.
- `BandwidthCalculator.Web/Models/CalculatorFormModel.cs` translates UI state into public library contracts.
- `BandwidthCalculator.Web/Models/DisplayResolutionPreset.cs` owns the UI's named resolution catalogue, including standard monitor modes and documented Apple panel/virtual-display rasters; presets populate pixel geometry but do not add calculator policy.
- `BandwidthCalculator.Web/Components` contains the form, metric, timing, and compatibility presentation components.
- `BandwidthCalculator.Web/Pages/Home.razor` owns the synchronous UI calculation workflow and error state.
- The test project separates saved scenarios, independent timing vectors, interface boundaries, and validation/overflow cases.

## Current invariants

- RGB is always 4:4:4; chroma subsampling is valid only for YCbCr.
- Inputs and all derived periods, totals, clocks, and rates must be positive, finite, and representable.
- CVT uses 8-pixel cells, RBv1 uses fixed 160-pixel blanking, RBv2 preserves one-pixel active-width precision, and RBv3 uses validated configurable blanking.
- `RefreshRateConvention.Automatic` recognizes standard 1000/1001 video rates. Explicit `VideoOptimized` input is the nominal integer rate to which 1000/1001 is applied.
- DMT and CTA lookup uses exact standard timing IDs when supplied. A geometrically ambiguous request without an ID fails rather than selecting by table order.
- CTA-861 OVT is progressive-only, uses the standard default parameters, preserves the requested active geometry, and therefore requires horizontal active pixels in 8-pixel increments and an integer nominal refresh rate.
- Interlaced CTA names use field rate; results expose both frame and field frequency.
- DSC bit-per-pixel is quantized downward in 1/16-bpp steps and must fit the selected payload. Recommended quality floors are 8 bpp for 4:4:4, 7 bpp for native 4:2:2, and 6 bpp for native 4:2:0.
- Reference interface profiles are theoretical named capability bundles, not claims that every product carrying a specification version implements those features.
- Custom interface profiles expose negotiated capabilities explicitly and do not infer them from a version label.
- Transport type owns blanking behavior, and profiles cannot mix DisplayPort and HDMI link modes.
- The WebAssembly project contains no copied timing, bandwidth, DSC, or transport formulas; it consumes the library assembly as the semantic authority.
- The simple UI path never requires a lane count, link rate, or DSC flag. Exact capability controls are optional and collapsed by default.
- Invalid UI input clears stale results, and calculation failures are shown without substituting a fallback result.
- The UI follows the operating-system light/dark preference through `prefers-color-scheme`; it does not own or persist a separate theme choice. Dark-mode large-area backgrounds use neutral greys, reserving blue for brand and interaction accents.
- Field, selector, option, and disclosure-summary hover/focus fills use one semantic interaction token mixed from 16% theme blue and the connector/configuration group surface. This preserves the neutral resting surfaces while making interaction substantially more distinct from the shared group background in both system themes.
- The configuration column is a full-section surface that uses the same semantic group fill as the connector-capability groups in both system colour schemes. On wide layouts its background extends through the app-shell's left gutter; in the responsive single-column layout the section expands through both shell gutters to span the full viewport width without moving its content inset. The stacked projection explicitly widens the grid item by the active shell gutters instead of relying on negative margins alone, so it cannot leave an unpainted right strip or introduce horizontal scrolling.
- The full-width header is deliberately compact. Its subtitle remains visible above 1100px and is hidden at narrower widths, while the transparent monitor brand mark remains visible at every viewport width and scales down with the header. Reset is a neutral-blue filled pill without a resting outline, and the configuration panel uses a shallow top inset so its leading diagram sits close below the header. Configuration and results remain side by side through compact-desktop widths and stack below 960px. Within the stacked workspace, widths from 760px through 960px use an internal two-column configuration projection: diagram left, primary fields right, and the two disclosure panels paired below; narrower widths return to the linear single-column flow.
- The browser favicon and page-title logo use the same transparent calculator display-and-data-flow SVG mark rather than framework branding; its dark fill is confined to the monitor screen and does not create an outer tile.
- Width, height, and aspect ratio are linked presentation inputs. The ratio is locked by default: editing a pixel dimension derives the other dimension. Unlocked, it preserves the other pixel dimension and derives the exact reduced ratio. Editing a ratio part always preserves the opposite pixel dimension and derives the matching dimension. The two ratio parts share one compact compound-control surface with an internal colon, leaving the dimension fields most of the geometry-row width. A compact borderless outline-lock control sits beside the concise `Ratio` label; its hover/focus tooltip explains the full aspect-ratio meaning and current locked or unlocked behavior instead of reserving a permanent help row.
- The full-width `Preset resolutions` selector sets both active dimensions and an exact ratio. Its accessible custom listbox aligns each open option into leading typical-marker, pixel-dimensions, and name columns while the closed selected value omits the marker and uses only the resolution and name. The control provides controlled whitespace, rounded menu and option corners, no active outline, and a subtle selected fill instead of the native high-contrast highlight. Ratios are reduced unless a preset has a mathematically equivalent conventional notation, such as Apple Vision Pro Wide at 21:9. Identified physical hardware presets also set their published diagonal in the currently selected unit; generic and virtual modes preserve the existing diagonal. Subsequent geometry edits retain a preset label only while the dimensions still match it exactly.
- `Display size` is presentation state with an inches/centimetres unit choice. `Density` is the editable PPI derived from active pixel diagonal and physical diagonal and is displayed and stepped as a whole number: a PPI edit preserves physical diagonal and the current aspect ratio, then rounds both active dimensions to their nearest whole pixels. Size and density do not cross the library boundary; density affects bandwidth only through the resulting dimensions. Their implications are contained in their label tooltips rather than a persistent explanatory row.
- The screen diagram is the first item in the configuration flow and compares active pixel dimensions with a 4K UHD reference. The complete figure is centred in the configuration column; its outer figure is transparent and borderless, and its wrapping legend is centred beneath the diagram. Exact dimensions appear only on the exterior measurement annotations, and a recognised mode may show its concise common name inside the selected image. The dashed reference is painted above the selected-image fill so it remains visible for larger modes. It represents pixel-grid proportions and relative pixel area, not physical panel size.
- Open basic-selector menus mark typical choices with a neutral `•` in a reserved leading option column; closed selected values omit the marker, and selectors without typical choices do not reserve marker space. The associated label guidance explains the symbol. Every editable configuration field exposes accessible contextual guidance directly from its label, and the UI has no separate information-icon triggers. Tooltip-bearing terms have no persistent underline; the help cursor plus a transient hover/focus colour change provides the visual affordance without adding resting-state noise.
- Form controls use filled, borderless resting, hover, and focus states. Active fields use a fill change rather than an outline; invalid controls retain their error border. Button, disclosure, link, and tooltip-trigger focus affordances remain independent.
- Numeric inputs suppress native browser spinner controls, including Safari/WebKit chevrons, while retaining keyboard entry and semantic number input behavior.
- Static unit suffixes are rendered inside their control shells and aligned to the right (`px`, `ppi`, `Hz`, `bits`, and advanced timing units). Display size presents its numeric value and interactive `in`/`cm` selector within one compound-control surface; the selected unit is content-sized and right-aligned with the same muted suffix styling and chevron spacing as other unit-bearing selectors. Every dropdown uses the same accessible custom listbox treatment as Preset resolutions: inset outlined chevron, controlled whitespace, rounded menu and option corners, outline-free focus fill, and a subtle selected state. The basic controls fill two equal, aligned three-column rows at every supported width: Display size / Density / Refresh rate, followed by Color depth / Encoding / Sampling. At the 320px floor, the colour triggers use compact padding so `10` and `YCbCr` remain fully visible; only the open Encoding menu widens beyond its field to fit both option labels. The row remains an accessible `Colour` group without adding a visible heading. Field labels use a smaller regular weight, a softer colour than primary content in both light and dark schemes, and a tight three-pixel gap above their controls.
- Frame and field frequency remain full-precision typed quantities in the calculation result, while the timing-detail UI renders them with at most three decimal places.
- Timing and exact-capability disclosure panels use filled, borderless closed and expanded states with no internal summary/body rule. The configuration and results regions meet without a vertical or stacked horizontal separator.
- The UI recalculates after every valid input edit and has no Calculate button. Invalid or incomplete edits clear stale results until the current form is valid again.
- The visible Configuration, Bandwidth summary, Link compatibility, and Timing details headings are omitted; their regions retain accessible labels. The results flow directly from bandwidth metric cards to compatibility groups to timing rows, separated by spacing rather than section rules. Each built-in interface family is a borderless neutral-grey group containing a wrapping line of borderless compact profile chips below its heading. The visible `USB-C` heading is concise, while its tooltip title uses the unambiguous `USB-C DisplayPort Alt Mode`. The version itself exposes typed profile capabilities without a separate information icon. Native and DSC support use white ticks inside green and blue filled circles; unsupported uses a prohibition symbol. Capability and limitation labels remain neutral and regular-weight, and no profile receives a separate lowest-compatible highlight. Unsupported profiles are replaced initially by a muted summary chip and revealed inline on activation, with a minimal trailing collapse control. Versions, selected methods, DSC ratios, and short limitation categories expose contextual hover/focus explanations; DSC includes exact source/selected bpp and 1/16-bpp precision, while full failure reasons remain available to callers and assistive text. Every contextual tooltip is deterministically clamped to a 16px viewport inset on every hover or focus.
- Contextual tooltips share a structured presentation: a compact bold title, an optional lead sentence, and bullet points for remaining sentences or semicolon-delimited capability summaries. Inactive tooltips are removed from layout so their positioned boxes cannot create horizontal overflow; hover or focus displays them before the positioning script clamps them to the viewport. Profile-version tooltip titles combine the interface-family tooltip title and version; USB-C profiles therefore use titles such as `USB-C DisplayPort Alt Mode 1.0`, while visible chips remain version-only. Field guidance, ratio-lock state, compatibility detail, and the unsupported-profile collapse control use the same keyboard-accessible treatment.

## Supported standards surface

- VESA CVT standard timing, Reduced Blanking v1, Reduced Blanking v2 including video-optimized rates, and Reduced Blanking v3.
- VESA DMT 1.0 Revision 13 modes with standard IDs.
- CTA-861 timing rows represented by VICs 1-127 and 193-219, including progressive/interlaced and integer/1000-over-1001 variants.
- CTA-861-I Optimized Video Timing, including grouped frame rates, 1000/1001 rates, audio-packet blanking allowance, 4:2:0 accommodation, and processing-chunk granularity.
- DisplayPort reference profiles through 2.1/UHBR20 and one-, two-, or four-lane custom profiles.
- DisplayPort Alt Mode reference profiles for two DP lanes with simultaneous USB and four DP lanes.
- HDMI TMDS, HDMI 2.1 FRL rates, and HDMI 2.2 64/80/96 Gbps reference rates.

## Operational constraints

- The public GitHub repository is `AdamCoulterOz/bandwidth-calculator`; `main` is the default and Pages deployment branch.
- `BandwidthTimingDatabase.g.cs` is generated-style data but the source generator is not present. Changes require checking identifiers and values against the named standards.
- Interface evaluation is video-payload-only. It does not budget audio, metadata, cable loss, device-specific DSC slice/buffer limits, or other negotiated protocol features.
- UnitsNet `6.0.0-pre021` is prerelease; dependency updates must retain the information/rate/frequency/duration relations used by the formulas.
- Run the UI locally with `dotnet run --project BandwidthCalculator.Web/BandwidthCalculator.Web.csproj`; the checked-in HTTP launch profile uses port 5187.
- The WebAssembly UI is static client output and performs calculations locally. `.github/workflows/deploy-pages.yml` tests and publishes it to GitHub Pages after every `main` push, rewrites the published base path from `/` to the project-site path, and creates a static `404.html` route fallback.
- The named resolution catalogue is intentionally a concise UI convenience list, not a standards database; exact timing identity still comes from DMT/CTA IDs and calculator validation.

## Outstanding actions and technical debt

- Restore a reproducible, source-attributed generator for `BandwidthTimingDatabase.g.cs`.
- Add device-negotiation adapters only in a separate lifecycle; the calculator itself remains deterministic and in-memory.
