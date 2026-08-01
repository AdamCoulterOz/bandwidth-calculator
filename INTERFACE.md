# Repository Interface

## 1. Purpose

BandwidthCalculator owns semantic calculation of display timing, video bandwidth, and compatibility with declared display-link capabilities. It exposes both a reusable .NET assembly and a standalone browser UI over the same contracts.

## 2. Responsibilities

Current responsibilities:

- Validate display geometry, rate, colour, timing, and option combinations.
- Resolve CVT/RB, DMT, CTA-861 VIC, and CTA-861 Optimized Video Timing metrics.
- Calculate maximum, line, active-image, and recommended-minimum DSC video rates.
- Evaluate native or DSC support against built-in reference profiles or caller-supplied negotiated capabilities.
- Present the complete calculation workflow through a client-side Blazor WebAssembly application.
- Publish the static WebAssembly application through the repository-owned GitHub Pages workflow.

Potential future ownership:

- Additional display timing revisions and transport presets when authoritative data and deterministic semantics are available.

The repository does not own device discovery, link negotiation, cable certification, audio budgeting, operating-system display configuration, a dynamic web service, or general-purpose hosting infrastructure. It owns only its static GitHub Pages deployment. The web project owns presentation and must not reimplement calculator formulas.

## 3. Domain model

- `CalculationInput` describes active geometry, UnitsNet refresh frequency, colour format, timing standard, scan mode, refresh convention, and optional standard timing/RBv3 details.
- `DisplayTiming` exposes resolved totals, pixel clock, horizontal frequency, and frame/field frequencies.
- `BandwidthBreakdown` contains UnitsNet video bitrates for the maximum raster, horizontal active portion, active image, and format-specific recommended `MinimumDsc` floor.
- `InterfaceCapabilities` describes one reference or negotiated profile and its link modes, formats, depths, and DSC capability.
- `InterfaceConfiguration` groups DisplayPort, HDMI, and DisplayPort Alt Mode profiles.
- `InterfaceEvaluation` reports native/compressed/unsupported status, the selected link method plus its typed transport/payload summary, optional DSC bpp and compression ratio, a detailed failure reason, a typed short `FailureKind`, and an `InterfaceProfileSummary` of the evaluated capability bundle.
- `CalculationResult` groups timing, bandwidth, and interface evaluations.

## 4. Public interfaces

The simple entry point uses built-in reference profiles:

```csharp
CalculationResult result = BandwidthCalculator.Calculate(
    new CalculationInput(
        VerticalPixels: 2160,
        HorizontalPixels: 3840,
        RefreshRate: Frequency.FromHertz(120),
        ColorBitDepth: 10,
        ColorEncoding: ColorEncoding.Rgb,
        ChromaSubsampling: ChromaSubsampling.Cs444,
        Timing: TimingStandard.CvtReducedBlankingV2));
```

No link rate, lane count, or DSC flags are required on this path. These results use `InterfaceConfiguration.ReferencePresets`; the UI explains their theoretical reference status once above the compatibility list rather than repeating it in every profile label.

The exact-capability overload accepts a concise configuration:

```csharp
InterfaceCapabilities dock = InterfaceConfiguration.DisplayPortProfile(
    profile: "Dock",
    maximumRate: DisplayPortLinkRate.Hbr3,
    supportsDsc: true,
    laneCount: 2);

CalculationResult result = BandwidthCalculator.Calculate(
    input,
    new InterfaceConfiguration(displayPort: [dock]));
```

Equivalent factories exist for DisplayPort Alt Mode, HDMI FRL, and HDMI TMDS. The low-level constructors remain available for capabilities not represented by a named standard rate.
Transport selection owns its protocol behavior: DisplayPort and HDMI TMDS include raster blanking, while HDMI FRL evaluates packetized active-video payload. Capability profiles reject link modes from another interface family.

Optional timing precision is expressed through `ScanMode`, `RefreshRateConvention`, `StandardTimingId`, and `CvtReducedBlankingV3Options`. `StandardTimingId` is a DMT ID for `TimingStandard.Dmt` and a VIC for `TimingStandard.Cta861`. `TimingStandard.Cta861OptimizedVideoTiming` is calculated without a VIC; it is progressive-only and requires an 8-pixel horizontal increment plus an integer nominal refresh rate. `Automatic` recognizes standard 1000/1001 input rates, while `VideoOptimized` applies 1000/1001 to the supplied nominal integer.

`Calculate` throws:

- `ArgumentNullException` for a null input or interface configuration.
- `ArgumentOutOfRangeException` for unknown enums, non-positive/non-finite rates, unsupported colour depth, invalid RBv3 ranges, non-positive IDs, or unrepresentable geometry.
- `ArgumentException` for contradictory options, invalid RGB subsampling, invalid OVT geometry/rate semantics, missing standard modes, and ambiguous DMT/CTA requests without an ID.
- `InvalidOperationException` when embedded timing data or a derived timing violates an internal consistency invariant.

### Blazor WebAssembly UI

`BandwidthCalculator.Web` is a sibling project in `BandwidthCalculator.slnx` and has a project reference to `BandwidthCalculator/BandwidthCalculator.csproj`.

The public application is hosted at `https://adamcoulteroz.github.io/bandwidth-calculator/`. The path is part of the deployment contract: published `index.html` and its `404.html` fallback use `/bandwidth-calculator/` as their base path, while source and local development retain `/`.

The default UI collects resolution, refresh rate, colour depth/encoding, chroma sampling, and timing standard, then calls `Calculate(input)` with built-in reference profiles. It recalculates after every valid input change; an invalid or incomplete edit clears stale results until the form is valid again. Optional timing controls expose scan mode, refresh convention, DMT/CTA identity, and RBv3 parameters only where relevant.

The collapsed exact-capabilities section accepts maximum standard rate, lane count, pixel-format support, and DSC support for DisplayPort, HDMI, and USB-C DisplayPort Alt Mode. When enabled it constructs one explicit profile per transport through the public factory methods and calls the exact-capability overload.

The UI displays every `BandwidthBreakdown` value, resolved timing metric, and returned interface evaluation. The visible Configuration and result-section headings are omitted while their regions retain accessible labels. Results flow from bandwidth metric cards to compatibility groups to timing rows, separated by spacing rather than section rules. The configuration and results regions meet without a vertical or stacked horizontal divider. Timing and exact-capability disclosures use filled, borderless closed and expanded states without an internal summary/body rule. Each interface family is a borderless neutral-grey group with a wrapping line of borderless compact profile chips below its heading. The concise visible `USB-C` heading uses `USB-C DisplayPort Alt Mode` as its full tooltip title. Unsupported profiles are collapsed by default into a muted summary chip; activating it replaces that summary with the unsupported profile chips and a minimal trailing collapse control without changing the calculation result. The version itself exposes the profile's capabilities on hover or focus, so no adjacent information icon is required; its tooltip title combines the full interface-family tooltip title and version, such as `USB-C DisplayPort Alt Mode 1.0`, while the visible chip remains version-only. Native and DSC support use white ticks inside green and blue filled circles, while unsupported uses a prohibition symbol. Capability and limitation labels are neutral, regular-weight text, and no profile receives a separate lowest-compatible highlight. The visible detail is the selected method, `DSC (ratio) · method`, or a concise limitation such as `Bandwidth` or `Format`. Hovering or focusing any version, method, compression, or limitation explains it in the current transport context; tooltip terms have no persistent underline and instead use a help cursor plus a transient colour change. Tooltips use a compact bold title, an optional lead, and bullets for remaining sentences or capability clauses. Inactive tooltips are absent from layout and therefore cannot enlarge the page's scroll width; active tooltips calculate their horizontal shift from the unshifted anchor and retain a 16px viewport inset across repeated hovers or focus. The DSC explanation includes exact source and selected bpp plus the 1/16-bpp quantization rule. Detailed failure prose remains available to callers and assistive text. It calculates synchronously in the browser and does not call a server API.

The UI supports light and dark colour schemes by following `prefers-color-scheme`; it does not expose or persist an application-specific theme selection. Dark-mode large-area backgrounds are neutral grey, with blue reserved for brand and interaction accents. Field labels use a smaller regular weight, sit close to their controls, and use a softer light-mode foreground or darker dark-mode foreground than primary content. The compact full-width header retains its subtitle above 1100px and hides it at narrower widths; its transparent monitor mark remains visible and scales down at narrow widths. Reset is a filled pill control with no resting outline. Configuration and results retain a two-column projection through compact-desktop widths and stack below 960px. In the stacked workspace, widths from 760px through 960px project the configuration form into two columns with the diagram on the left, primary fields on the right, and the disclosure panels paired below; narrower widths return to a single-column flow. The configuration column uses the same semantic group fill as the connector-capability groups in both schemes. It extends through the app-shell's left gutter while side by side and through both gutters when the workspace collapses to one column, while preserving the form's content inset, keeping its leading diagram close beneath the header, and avoiding horizontal page overflow. Form controls are borderless at rest, hover, and focus, using a shared interaction fill mixed from 16% theme blue and the group surface so fields, options, and disclosure summaries remain distinct from the configuration background in either scheme; invalid-state borders remain visible.

Width, height, and exact aspect-ratio parts are linked. The ratio is locked by default: changing width derives height and changing height derives width while preserving the current ratio. When unlocked, changing width or height preserves the other pixel dimension and reduces the resulting ratio. Changing the horizontal ratio part always preserves height and derives width, while changing the vertical ratio part always preserves width and derives height. The ratio parts appear as two narrow numeric segments within one compound-control surface, separated by an internal colon, so width and height retain most of the row. A compact borderless outline-lock control beside the concise `Ratio` label exposes the full aspect-ratio meaning and current behavior in a hover/focus tooltip; no permanent ratio-help row is rendered. Numeric inputs suppress native spinner controls, including Safari/WebKit chevrons, without changing their number-input semantics. Named resolution presets populate both dimensions and use a conventional ratio notation only when it is mathematically exact; direct geometry edits return the selector to `Custom resolution` unless the result exactly matches a preset. Reset restores the locked state.

Static unit suffixes appear inside the right edge of their control shell (`px`, `ppi`, `Hz`, `bits`, and advanced timing units) without becoming part of the editable value. Display size combines its numeric input and independently interactive `in`/`cm` selector in one compound-control surface; the selected unit is content-sized and right-aligned with the same muted suffix styling and chevron gap as other unit-bearing selectors. Every dropdown uses the same accessible custom-listbox interaction and visual treatment as Preset resolutions, including the inset outlined chevron, controlled whitespace, rounded menu and option corners, outline-free focus fill, subtle selected state, disabled behavior, Escape close, and focus restoration. The basic controls fill two equal, aligned three-column rows at every supported width: Display size / Density / Refresh rate, followed by Color depth / Encoding / Sampling. At the 320px floor, compact colour-trigger padding keeps `10` and `YCbCr` visible while the open Encoding menu alone widens enough to show both option labels. The latter is exposed as the accessible `Colour` group without a visible heading row.

`Display size` is editable in inches or centimetres. Selecting a preset that identifies one physical hardware panel sets its published diagonal in the active unit; generic resolutions and virtual displays preserve the current value. `Density` is the editable PPI and is displayed and stepped as a whole number: editing it preserves the current physical diagonal and aspect-ratio parts, then rounds the resulting width and height independently to the nearest whole pixels. Physical size remains presentation-only, while a density edit affects calculation indirectly through its resized pixel raster; neither size nor PPI is added to `CalculationInput`. Virtual displays do not imply a fixed physical diagonal. This viewing-geometry and bandwidth context is exposed by the two field-label tooltips rather than persistent helper text.

The configuration begins with a proportional SVG preview in a transparent, borderless figure, followed by a full-width `Preset resolutions` selector and editable geometry. The preset selector uses an accessible custom listbox whose open options align a leading typical marker, pixel dimensions, and name into separate columns while its closed selected value omits the marker. It retains controlled whitespace, rounded menu and option corners, outline-free focus fill, and a subtle selected state instead of the native high-contrast highlight. The complete figure is centred in its column, its wrapping legend is centred beneath the diagram, and exact pixel dimensions remain on the exterior measurement annotations. Recognised presets show only a concise common mode name inside the selected image; custom resolutions leave the interior unlabelled instead of duplicating dimensions or secondary statistics. Its dashed 4K UHD reference is layered above the selected-image fill so it remains visible for both smaller and larger modes. It communicates relative pixel dimensions and area only; no physical screen size is inferred. Open basic-selector options use `•` in a reserved leading column to mark typical choices, while closed selected values omit it and no marker column is reserved for selectors without typical choices; the symbol is explained by the associated label guidance. Field labels directly expose contextual guidance about semantics, common ranges, and maturity on hover or focus without persistent underlines or separate information icons.

## 5. Invariants

- UnitsNet quantities cross the public boundary for frequency and bitrate.
- A calculation never silently substitutes a distinct DMT/CTA timing.
- CTA OVT never silently rounds the requested active geometry or nominal refresh rate.
- `DisplayTiming` clocks and frequencies are derived consistently from its totals.
- A selected method is present in the evaluation's method list.
- Library-produced supported evaluations include the selected method's transport, lane count, and payload bandwidth summary; unsupported evaluations do not select a link method.
- Native and unsupported results have no DSC bpp or compression ratio.
- Compressed results have a downward-quantized DSC bpp at or above the format quality floor, a source-to-selected-bpp compression ratio, and a resulting stream that does not exceed the selected link payload.
- Library-produced evaluations always include an `InterfaceProfileSummary`; supported results use `InterfaceFailureKind.None`, while unsupported results identify the failed semantic capability without replacing their detailed reason.
- Built-in version profiles are reference ceilings; caller-supplied profiles are the authority for real equipment.
- UI results are always produced by the referenced library assembly; the UI owns formatting and interaction state only.
- Frame and field frequencies retain their full typed values across the library boundary; the timing-detail UI displays at most three decimal places.
- An invalid form edit or a library exception cannot leave an apparently current stale result visible.
- Every valid UI input edit immediately recalculates from current form state; no explicit Calculate action exists.
- UI convenience presets and aspect-ratio linking never bypass library validation or imply a standard timing identity.
- Aspect-ratio lock state is transient form state and does not cross the calculator-library boundary.
- Physical diagonal, its selected display unit, and PPI remain transient UI state. A diagonal-only edit does not affect calculator results; a PPI edit resizes width and height, which does.

## 6. Side effects

Calculation is synchronous, deterministic, in-memory, and free of filesystem, network, persistence, logging, or service side effects. The standalone UI loads its static WebAssembly assets from its host, then performs calculations locally without application network calls. A push to `main` invokes GitHub Actions, which restores and tests the solution, publishes the static WebAssembly assets, and deploys them to GitHub Pages.

## 7. Dependency boundaries

- UnitsNet owns physical quantities and quantity arithmetic.
- VESA/CTA timing semantics and declared link capabilities are inputs to this repository's deterministic domain logic.
- Timing tables and reference presets are internal data; consumers depend on public semantic types, not table order.
- Device discovery and negotiation belong in caller-owned adapters and feed `InterfaceConfiguration`.
- `BandwidthCalculator.Web` depends on the public calculator contract through a project reference; the library has no dependency on the UI.
- GitHub Actions and GitHub Pages own build execution and static asset hosting; neither dependency crosses into calculator runtime semantics.

## 8. Lifecycle and execution model

The library has no startup lifecycle. Static standard data and reference profiles are initialized once and exposed through immutable/read-only collections. Calls are stateless and safe for concurrent reads.

The standalone UI follows the Blazor WebAssembly startup lifecycle, creates an in-memory default form, calculates the initial reference-profile result, and recalculates synchronously after each valid form edit. Reset restores the documented default scenario and recalculates it.

The deployment lifecycle is independent of calculator execution. A `main` push runs one Pages job that tests, publishes, adjusts the project-site base path, uploads the static artifact, and deploys it to the `github-pages` environment.

## 9. Anti-goals

- Treating an HDMI or DisplayPort version label as proof of a device feature.
- Returning storage-shaped timing rows as the public API.
- Replacing quantity types with unitless rates.
- Silently approximating or selecting an ambiguous standard mode.
- Performing runtime standards or device lookups.
- Introducing a server-side calculator API or dynamic hosting dependency for the static UI.
- Moving timing, bandwidth, or link-selection policy into Razor components.
- Requiring exact link configuration for the normal UI workflow.

## 10. Agent guidance

- Preserve the zero-configuration reference-profile path and the explicit negotiated-capability path together.
- Keep timing, bandwidth, and transport policy in their current lifecycle-separated components.
- Verify formula changes against independent standards-derived vectors, not saved calculator output alone.
- Update this contract with any public model, selection, rounding, exception, or standards-scope change.
- Keep the WebAssembly project presentation-only and verify both the simple and exact-capability browser workflows after UI changes.
- Preserve the source `/` base path for local development and apply `/bandwidth-calculator/` only to published Pages output.
- Preserve deterministic geometry linking, accessible field guidance, system-owned theming, and the distinction between pixel-area comparison and physical display size.
