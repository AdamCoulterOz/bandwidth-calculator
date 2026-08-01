# Bandwidth Calculator

A unit-aware .NET calculator for display timing, video bandwidth, and DisplayPort, HDMI, and USB-C DisplayPort Alt Mode compatibility.

Use the hosted Blazor WebAssembly calculator at [adamcoulteroz.github.io/bandwidth-calculator](https://adamcoulteroz.github.io/bandwidth-calculator/).

## What it calculates

- CVT, Reduced Blanking, DMT, CTA-861, and CTA-861 Optimized Video Timing
- Full-raster, horizontal-active, and active-image video bandwidth
- Native and Display Stream Compression compatibility against reference link profiles
- Exact capability checks when lane count, link rate, pixel-format, and DSC support are known
- Linked display width, height, aspect ratio, physical size, and pixel density

## Projects

- `BandwidthCalculator` — reusable calculator library
- `BandwidthCalculator.Tests` — correctness and regression tests
- `BandwidthCalculator.Web` — standalone Blazor WebAssembly UI

## Build and test

Requires the .NET 10 SDK.

```sh
dotnet build BandwidthCalculator.slnx -c Release
dotnet test BandwidthCalculator.slnx -c Release
```

Run the UI locally:

```sh
dotnet run --project BandwidthCalculator.Web/BandwidthCalculator.Web.csproj
```

The checked-in launch profile serves the app at `http://localhost:5187`.

## Deployment

Every push to `main` runs the GitHub Pages workflow. It tests the solution, publishes the WebAssembly app, applies the repository base path, and deploys the static output.

The application performs calculations entirely in the browser and does not call a calculator service.
