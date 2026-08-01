using UnitsNet;

namespace BandwidthCalculator;

public static class BandwidthCalculator
{
    public static CalculationResult Calculate(CalculationInput input) =>
        Calculate(input, InterfaceConfiguration.ReferencePresets);

    public static CalculationResult Calculate(
        CalculationInput input,
        InterfaceConfiguration interfaces)
    {
        InputValidator.Validate(input);
        ArgumentNullException.ThrowIfNull(interfaces);

        TimingMetrics timing = DisplayTimingCalculator.Resolve(input);
        Information bitsPerPixel = Information.FromBits(ResolveBitsPerPixel(input));
        Information minimumDscBitsPerPixel = Information.FromBits(ResolveMinimumDscBitsPerPixel(input.ChromaSubsampling));

        BitRate maximum = bitsPerPixel * timing.PixelClock;
        BitRate line = maximum * timing.HorizontalActive / timing.HorizontalTotal;
        BitRate active = bitsPerPixel * timing.VerticalActive * timing.HorizontalActive * timing.FrameFrequency;
        BitRate dsc = minimumDscBitsPerPixel * timing.PixelClock;
        BandwidthBreakdown bandwidth = new(maximum, line, active, dsc);

        return new CalculationResult(
            timing.ToPublic(),
            bandwidth,
            InterfaceEvaluator.Evaluate(interfaces.DisplayPort, input, bandwidth, timing, bitsPerPixel),
            InterfaceEvaluator.Evaluate(interfaces.Hdmi, input, bandwidth, timing, bitsPerPixel),
            InterfaceEvaluator.Evaluate(interfaces.DisplayPortAltModeOnUsbC, input, bandwidth, timing, bitsPerPixel));
    }

    internal static double ResolveBitsPerPixel(CalculationInput input) =>
        input.ColorBitDepth * ResolveChromaFactor(input.ChromaSubsampling);

    internal static double ResolveMinimumDscBitsPerPixel(ChromaSubsampling subsampling) =>
        subsampling switch
        {
            ChromaSubsampling.Cs444 => 8d,
            ChromaSubsampling.Cs422 => 7d,
            ChromaSubsampling.Cs420 => 6d,
            _ => throw new ArgumentOutOfRangeException(nameof(subsampling)),
        };

    private static double ResolveChromaFactor(ChromaSubsampling subsampling) =>
        subsampling switch
        {
            ChromaSubsampling.Cs444 => 3d,
            ChromaSubsampling.Cs422 => 2d,
            ChromaSubsampling.Cs420 => 1.5d,
            _ => throw new ArgumentOutOfRangeException(nameof(subsampling)),
        };
}
