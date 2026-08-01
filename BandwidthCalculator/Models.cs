using UnitsNet;

namespace BandwidthCalculator;

public enum ColorEncoding
{
    Rgb,
    YCbCr,
}

public enum ChromaSubsampling
{
    Cs444,
    Cs422,
    Cs420,
}

public enum TimingStandard
{
    Cvt,
    CvtReducedBlanking,
    CvtReducedBlankingV2,
    CvtReducedBlankingV3,
    Dmt,
    Cta861,
    Cta861OptimizedVideoTiming,
}

public enum ScanMode
{
    Progressive,
    Interlaced,
}

public enum RefreshRateConvention
{
    /// <summary>
    /// Uses the supplied rate exactly unless it is recognisable as a standard
    /// 1000/1001 video rate supported by the selected timing standard.
    /// </summary>
    Automatic,

    /// <summary>
    /// Uses the supplied rate exactly and performs no 1000/1001 inference.
    /// </summary>
    Exact,

    /// <summary>
    /// Treats the supplied rate as the nominal integer video rate and applies 1000/1001.
    /// </summary>
    VideoOptimized,
}

public enum InterfaceSupport
{
    No,
    Native,
    Compressed,
}

public enum InterfaceFailureKind
{
    None,
    PixelFormat,
    Bandwidth,
    DscInputBitDepth,
    DscQualityFloor,
}

public sealed record CvtReducedBlankingV3Options(
    int HorizontalBlankPixels = 80,
    int MinimumVerticalBlankMicroseconds = 460,
    bool EarlyVsync = false);

public sealed record CalculationInput(
    int VerticalPixels,
    int HorizontalPixels,
    Frequency RefreshRate,
    int ColorBitDepth,
    ColorEncoding ColorEncoding,
    ChromaSubsampling ChromaSubsampling,
    TimingStandard Timing,
    ScanMode ScanMode = ScanMode.Progressive,
    RefreshRateConvention RefreshRateConvention = RefreshRateConvention.Automatic,
    int? StandardTimingId = null,
    CvtReducedBlankingV3Options? CvtReducedBlankingV3 = null);

public sealed record DisplayTiming(
    TimingStandard Standard,
    int? StandardTimingId,
    ScanMode ScanMode,
    int HorizontalActive,
    int HorizontalBlank,
    int HorizontalTotal,
    int VerticalActive,
    double VerticalBlankLines,
    int VerticalTotal,
    Frequency PixelClock,
    Frequency HorizontalFrequency,
    Frequency FrameFrequency,
    Frequency FieldFrequency);

public sealed record BandwidthBreakdown(
    BitRate Maximum,
    BitRate Line,
    BitRate Active,
    BitRate MinimumDsc);

public sealed record InterfaceProfileSummary(
    InterfaceKind Kind,
    string MaximumLinkMethod,
    int? LaneCount,
    bool SupportsDsc,
    bool Supports420,
    int MaximumBitsPerComponent);

public sealed record InterfaceLinkMethodSummary(
    string Name,
    LinkTransport Transport,
    BitRate PayloadBandwidth,
    int LaneCount);

public sealed record InterfaceEvaluation(
    string Profile,
    IReadOnlyList<string> Methods,
    InterfaceSupport Support,
    string? SelectedMethod,
    double? DscBitsPerPixel,
    string? FailureReason)
{
    public InterfaceProfileSummary? ProfileSummary { get; init; }
    public InterfaceLinkMethodSummary? SelectedLinkMethod { get; init; }
    public InterfaceFailureKind FailureKind { get; init; }
    public double? DscCompressionRatio { get; init; }
}

public sealed record CalculationResult(
    DisplayTiming Timing,
    BandwidthBreakdown Bandwidth,
    IReadOnlyList<InterfaceEvaluation> DisplayPort,
    IReadOnlyList<InterfaceEvaluation> Hdmi,
    IReadOnlyList<InterfaceEvaluation> DisplayPortAltModeOnUsbC);

internal readonly record struct TimingMetrics(
    TimingStandard Standard,
    int? StandardTimingId,
    ScanMode ScanMode,
    int HorizontalActive,
    int HorizontalBlank,
    int HorizontalTotal,
    int VerticalActive,
    double VerticalBlankLines,
    int VerticalTotal,
    Frequency PixelClock,
    Frequency HorizontalFrequency,
    Frequency FrameFrequency,
    Frequency FieldFrequency)
{
    public DisplayTiming ToPublic() =>
        new(
            Standard,
            StandardTimingId,
            ScanMode,
            HorizontalActive,
            HorizontalBlank,
            HorizontalTotal,
            VerticalActive,
            VerticalBlankLines,
            VerticalTotal,
            PixelClock,
            HorizontalFrequency,
            FrameFrequency,
            FieldFrequency);
}

internal readonly record struct DmtTiming(
    int HorizontalActive,
    int VerticalActive,
    Frequency VerticalFrequency,
    bool Interlaced,
    DmtTimingKind Kind,
    Frequency PixelClock,
    int HorizontalBlank,
    int HorizontalTotal,
    double VerticalBlank,
    int VerticalTotal)
{
    public DmtTiming(
        int horizontalActive,
        int verticalActive,
        double verticalFrequencyHz,
        bool interlaced,
        DmtTimingKind kind,
        double pixelClockMhz,
        int horizontalBlank,
        int horizontalTotal,
        double verticalBlank,
        int verticalTotal)
        : this(
            horizontalActive,
            verticalActive,
            Frequency.FromHertz(verticalFrequencyHz),
            interlaced,
            kind,
            Frequency.FromMegahertz(pixelClockMhz),
            horizontalBlank,
            horizontalTotal,
            verticalBlank,
            verticalTotal)
    {
    }
}

internal enum DmtTimingKind
{
    Direct,
    Cvt,
    CvtReducedBlanking,
    CvtReducedBlankingV2,
    Cta861,
}

internal readonly record struct CeaTiming
{
    public CeaTiming(
        int horizontalActive,
        int verticalActive,
        double verticalFrequencyHz,
        bool interlaced,
        int horizontalBlank,
        int horizontalTotal,
        double verticalBlank,
        int verticalTotal,
        double pixelClockMhz)
        : this(
            horizontalActive,
            verticalActive,
            Frequency.FromHertz(verticalFrequencyHz),
            interlaced,
            horizontalBlank,
            horizontalTotal,
            verticalBlank,
            verticalTotal,
            Frequency.FromMegahertz(pixelClockMhz))
    {
    }

    public CeaTiming(
        int horizontalActive,
        int verticalActive,
        Frequency verticalFrequency,
        bool interlaced,
        int horizontalBlank,
        int horizontalTotal,
        double verticalBlank,
        int verticalTotal,
        Frequency pixelClock)
    {
        HorizontalActive = horizontalActive;
        VerticalActive = verticalActive;
        VerticalFrequency = verticalFrequency;
        Interlaced = interlaced;
        HorizontalBlank = horizontalBlank;
        HorizontalTotal = horizontalTotal;
        VerticalBlank = verticalBlank;
        VerticalTotal = verticalTotal;
        PixelClock = pixelClock;
    }

    public int HorizontalActive { get; }
    public int VerticalActive { get; }
    public Frequency VerticalFrequency { get; }
    public bool Interlaced { get; }
    public int HorizontalBlank { get; }
    public int HorizontalTotal { get; }
    public double VerticalBlank { get; }
    public int VerticalTotal { get; }
    public Frequency PixelClock { get; }
}
