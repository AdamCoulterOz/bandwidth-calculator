using UnitsNet;

namespace BandwidthCalculator;

internal static class InterfaceEvaluator
{
    private const double DscBitsPerPixelIncrement = 1d / 16d;

    public static IReadOnlyList<InterfaceEvaluation> Evaluate(
        IReadOnlyList<InterfaceCapabilities> profiles,
        CalculationInput input,
        BandwidthBreakdown bandwidth,
        TimingMetrics timing,
        Information sourceBitsPerPixel)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(bandwidth);

        return [.. profiles.Select(profile => Evaluate(profile, input, bandwidth, timing, sourceBitsPerPixel))];
    }

    private static InterfaceEvaluation Evaluate(
        InterfaceCapabilities profile,
        CalculationInput input,
        BandwidthBreakdown bandwidth,
        TimingMetrics timing,
        Information sourceBitsPerPixel)
    {
        string[] methods = [.. profile.LinkModes.Select(mode => mode.Name)];
        InterfaceProfileSummary profileSummary = Summarize(profile);

        if (!profile.SupportsPixelFormat(input))
        {
            return Unsupported(
                profile,
                methods,
                profileSummary,
                InterfaceFailureKind.PixelFormat,
                $"Pixel format {FormatPixelFormat(input)} is not supported by this profile.");
        }

        LinkModeCapabilities? nativeMode = profile.LinkModes
            .OrderBy(mode => mode.PayloadBandwidth)
            .FirstOrDefault(mode => ResolveNativeBandwidth(mode, input, bandwidth, timing, sourceBitsPerPixel) <= mode.PayloadBandwidth);

        if (nativeMode is not null)
        {
            return new InterfaceEvaluation(
                profile.Profile,
                methods,
                InterfaceSupport.Native,
                nativeMode.Name,
                null,
                null)
            {
                ProfileSummary = profileSummary,
                SelectedLinkMethod = Summarize(nativeMode),
            };
        }

        if (!profile.SupportsDsc)
        {
            return Unsupported(
                profile,
                methods,
                profileSummary,
                InterfaceFailureKind.Bandwidth,
                "Not enough bandwidth, and DSC is not enabled by this profile.");
        }

        if (!profile.SupportsDscInput(input))
        {
            return Unsupported(
                profile,
                methods,
                profileSummary,
                InterfaceFailureKind.DscInputBitDepth,
                $"DSC does not support {input.ColorBitDepth}-bit input in this profile.");
        }

        double minimumDscBitsPerPixel = BandwidthCalculator.ResolveMinimumDscBitsPerPixel(input.ChromaSubsampling);
        DscCandidate[] dscCandidates = [.. profile.LinkModes
            .Select(mode => ResolveDscCandidate(mode, timing, sourceBitsPerPixel, minimumDscBitsPerPixel))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!.Value)
            .OrderByDescending(candidate => candidate.BitsPerPixel)
            .ThenBy(candidate => candidate.Mode.PayloadBandwidth)];

        if (dscCandidates.Length == 0)
        {
            return Unsupported(
                profile,
                methods,
                profileSummary,
                InterfaceFailureKind.DscQualityFloor,
                $"Not enough bandwidth at the {minimumDscBitsPerPixel:0.##} bpp DSC quality floor for {input.ChromaSubsampling}.");
        }

        DscCandidate selectedDsc = dscCandidates[0];

        return new InterfaceEvaluation(
            profile.Profile,
            methods,
            InterfaceSupport.Compressed,
            selectedDsc.Mode.Name,
            selectedDsc.BitsPerPixel,
            null)
        {
            ProfileSummary = profileSummary,
            SelectedLinkMethod = Summarize(selectedDsc.Mode),
            DscCompressionRatio = sourceBitsPerPixel.Bits / selectedDsc.BitsPerPixel,
        };
    }

    private static BitRate ResolveNativeBandwidth(
        LinkModeCapabilities mode,
        CalculationInput input,
        BandwidthBreakdown bandwidth,
        TimingMetrics timing,
        Information sourceBitsPerPixel)
    {
        Information transportBitsPerPixel = ResolveNativeTransportBitsPerPixel(mode.Transport, input, sourceBitsPerPixel);

        if (transportBitsPerPixel == sourceBitsPerPixel)
            return mode.UsesActiveVideoBandwidth ? bandwidth.Active : bandwidth.Maximum;

        return transportBitsPerPixel * ResolveTransportPixelRate(mode, timing);
    }

    private static Information ResolveNativeTransportBitsPerPixel(
        LinkTransport transport,
        CalculationInput input,
        Information sourceBitsPerPixel)
    {
        bool isHdmi = transport is LinkTransport.HdmiTmds or LinkTransport.HdmiFixedRateLink;
        return isHdmi && input.ColorEncoding == ColorEncoding.YCbCr && input.ChromaSubsampling == ChromaSubsampling.Cs422
            ? Information.FromBits(24d)
            : sourceBitsPerPixel;
    }

    private static DscCandidate? ResolveDscCandidate(
        LinkModeCapabilities mode,
        TimingMetrics timing,
        Information sourceBitsPerPixel,
        double minimumBitsPerPixel)
    {
        Frequency transportPixelRate = ResolveTransportPixelRate(mode, timing);
        double availableBitsPerPixel = (mode.PayloadBandwidth / transportPixelRate).Bits;
        double maximumUsefulBitsPerPixel = Math.Min(availableBitsPerPixel, sourceBitsPerPixel.Bits);
        double quantizedBitsPerPixel = Math.Floor(maximumUsefulBitsPerPixel / DscBitsPerPixelIncrement) * DscBitsPerPixelIncrement;

        if (quantizedBitsPerPixel < minimumBitsPerPixel)
            return null;

        BitRate requiredBandwidth = Information.FromBits(quantizedBitsPerPixel) * transportPixelRate;
        if (requiredBandwidth > mode.PayloadBandwidth)
            return null;

        return new DscCandidate(mode, quantizedBitsPerPixel);
    }

    private static Frequency ResolveTransportPixelRate(LinkModeCapabilities mode, TimingMetrics timing) =>
        mode.UsesActiveVideoBandwidth
            ? timing.FrameFrequency * timing.HorizontalActive * timing.VerticalActive
            : timing.PixelClock;

    private static InterfaceEvaluation Unsupported(
        InterfaceCapabilities profile,
        IReadOnlyList<string> methods,
        InterfaceProfileSummary profileSummary,
        InterfaceFailureKind failureKind,
        string reason) =>
        new(profile.Profile, methods, InterfaceSupport.No, null, null, reason)
        {
            ProfileSummary = profileSummary,
            FailureKind = failureKind,
        };

    private static InterfaceProfileSummary Summarize(InterfaceCapabilities profile)
    {
        LinkModeCapabilities maximumMode = profile.LinkModes.MaxBy(mode => mode.PayloadBandwidth)!;
        int[] laneCounts = [.. profile.LinkModes.Select(mode => mode.LaneCount).Distinct()];

        return new InterfaceProfileSummary(
            profile.Kind,
            maximumMode.Name,
            laneCounts.Length == 1 ? laneCounts[0] : null,
            profile.SupportsDsc,
            profile.PixelFormats.Any(format => format.ChromaSubsampling == ChromaSubsampling.Cs420),
            profile.PixelFormats.SelectMany(format => format.ColorBitDepths).Max());
    }

    private static InterfaceLinkMethodSummary Summarize(LinkModeCapabilities mode) =>
        new(mode.Name, mode.Transport, mode.PayloadBandwidth, mode.LaneCount);

    private static string FormatPixelFormat(CalculationInput input) =>
        $"{input.ColorEncoding} {input.ChromaSubsampling} at {input.ColorBitDepth} bits per component";

    private readonly record struct DscCandidate(LinkModeCapabilities Mode, double BitsPerPixel);
}
