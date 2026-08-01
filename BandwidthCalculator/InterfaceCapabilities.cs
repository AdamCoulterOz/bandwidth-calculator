using UnitsNet;

namespace BandwidthCalculator;

public enum InterfaceKind
{
    DisplayPort,
    Hdmi,
    DisplayPortAltModeOnUsbC,
}

public enum LinkTransport
{
    DisplayPortMainLink,
    HdmiTmds,
    HdmiFixedRateLink,
}

public enum DisplayPortLinkRate
{
    Rbr,
    Hbr,
    Hbr2,
    Hbr3,
    Uhbr10,
    Uhbr13Point5,
    Uhbr20,
}

public enum HdmiTmdsRate
{
    MHz165,
    MHz340,
    MHz600,
}

public enum HdmiFixedRateLinkRate
{
    Frl1,
    Frl2,
    Frl3,
    Frl4,
    Frl5,
    Frl6,
    Gbps64,
    Gbps80,
    Gbps96,
}

public sealed record LinkModeCapabilities
{
    public LinkModeCapabilities(
        string name,
        LinkTransport transport,
        BitRate payloadBandwidth,
        int laneCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!Enum.IsDefined(transport))
            throw new ArgumentOutOfRangeException(nameof(transport));

        if (!double.IsFinite(payloadBandwidth.BitsPerSecond) || payloadBandwidth <= BitRate.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(payloadBandwidth),
                "Payload bandwidth must be finite and greater than zero.");
        }

        if (laneCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(laneCount), "Lane count must be greater than zero.");

        Name = name;
        Transport = transport;
        PayloadBandwidth = payloadBandwidth;
        LaneCount = laneCount;
    }

    public string Name { get; }
    public LinkTransport Transport { get; }
    public BitRate PayloadBandwidth { get; }
    public int LaneCount { get; }

    /// <summary>
    /// Whether unused blanking can be removed when evaluating the transported video payload.
    /// </summary>
    public bool UsesActiveVideoBandwidth => Transport == LinkTransport.HdmiFixedRateLink;

    public static LinkModeCapabilities DisplayPort(DisplayPortLinkRate rate, int laneCount)
    {
        if (!Enum.IsDefined(rate))
            throw new ArgumentOutOfRangeException(nameof(rate));

        if (laneCount is not (1 or 2 or 4))
            throw new ArgumentOutOfRangeException(nameof(laneCount), "DisplayPort supports one, two, or four main-link lanes.");

        (string name, double fourLanePayloadGbps) = rate switch
        {
            DisplayPortLinkRate.Rbr => ("RBR", 5.184d),
            DisplayPortLinkRate.Hbr => ("HBR", 8.64d),
            DisplayPortLinkRate.Hbr2 => ("HBR2", 17.28d),
            DisplayPortLinkRate.Hbr3 => ("HBR3", 25.92d),
            DisplayPortLinkRate.Uhbr10 => ("UHBR10", 38.69d),
            DisplayPortLinkRate.Uhbr13Point5 => ("UHBR13.5", 52.22d),
            DisplayPortLinkRate.Uhbr20 => ("UHBR20", 77.37d),
            _ => throw new ArgumentOutOfRangeException(nameof(rate)),
        };

        return new LinkModeCapabilities(
            name,
            LinkTransport.DisplayPortMainLink,
            BitRate.FromGigabitsPerSecond(fourLanePayloadGbps * laneCount / 4d),
            laneCount);
    }

    public static LinkModeCapabilities HdmiTmds(HdmiTmdsRate rate)
    {
        if (!Enum.IsDefined(rate))
            throw new ArgumentOutOfRangeException(nameof(rate));

        (string name, double payloadGbps) = rate switch
        {
            HdmiTmdsRate.MHz165 => ("165MHz TMDS", 3.96d),
            HdmiTmdsRate.MHz340 => ("340MHz TMDS", 8.16d),
            HdmiTmdsRate.MHz600 => ("600MHz TMDS", 14.4d),
            _ => throw new ArgumentOutOfRangeException(nameof(rate)),
        };

        return new LinkModeCapabilities(
            name,
            LinkTransport.HdmiTmds,
            BitRate.FromGigabitsPerSecond(payloadGbps),
            laneCount: 3);
    }

    public static LinkModeCapabilities HdmiFixedRateLink(HdmiFixedRateLinkRate rate)
    {
        if (!Enum.IsDefined(rate))
            throw new ArgumentOutOfRangeException(nameof(rate));

        (string name, double rawBandwidthGbps, int lanes) = rate switch
        {
            HdmiFixedRateLinkRate.Frl1 => ("FRL1", 9d, 3),
            HdmiFixedRateLinkRate.Frl2 => ("FRL2", 18d, 3),
            HdmiFixedRateLinkRate.Frl3 => ("FRL3", 24d, 4),
            HdmiFixedRateLinkRate.Frl4 => ("FRL4", 32d, 4),
            HdmiFixedRateLinkRate.Frl5 => ("FRL5", 40d, 4),
            HdmiFixedRateLinkRate.Frl6 => ("FRL6", 48d, 4),
            HdmiFixedRateLinkRate.Gbps64 => ("64 Gbps FRL", 64d, 4),
            HdmiFixedRateLinkRate.Gbps80 => ("80 Gbps FRL", 80d, 4),
            HdmiFixedRateLinkRate.Gbps96 => ("96 Gbps FRL", 96d, 4),
            _ => throw new ArgumentOutOfRangeException(nameof(rate)),
        };

        return new LinkModeCapabilities(
            name,
            LinkTransport.HdmiFixedRateLink,
            BitRate.FromGigabitsPerSecond(rawBandwidthGbps * 16d / 18d),
            lanes);
    }
}

public sealed class PixelFormatCapabilities
{
    private readonly HashSet<int> _colorBitDepths;

    public PixelFormatCapabilities(
        ColorEncoding colorEncoding,
        ChromaSubsampling chromaSubsampling,
        IEnumerable<int> colorBitDepths)
    {
        if (!Enum.IsDefined(colorEncoding))
            throw new ArgumentOutOfRangeException(nameof(colorEncoding));

        if (!Enum.IsDefined(chromaSubsampling))
            throw new ArgumentOutOfRangeException(nameof(chromaSubsampling));

        if (colorEncoding == ColorEncoding.Rgb && chromaSubsampling != ChromaSubsampling.Cs444)
        {
            throw new ArgumentException(
                "RGB pixel-format capabilities must use 4:4:4 chroma sampling.",
                nameof(chromaSubsampling));
        }

        ArgumentNullException.ThrowIfNull(colorBitDepths);

        int[] depths = [.. colorBitDepths.Distinct().Order()];
        if (depths.Length == 0 || depths.Any(depth => depth <= 0))
            throw new ArgumentException("At least one positive color bit depth is required.", nameof(colorBitDepths));

        ColorEncoding = colorEncoding;
        ChromaSubsampling = chromaSubsampling;
        ColorBitDepths = Array.AsReadOnly(depths);
        _colorBitDepths = [.. depths];
    }

    public ColorEncoding ColorEncoding { get; }
    public ChromaSubsampling ChromaSubsampling { get; }
    public IReadOnlyList<int> ColorBitDepths { get; }

    public bool Supports(CalculationInput input) =>
        ColorEncoding == input.ColorEncoding &&
        ChromaSubsampling == input.ChromaSubsampling &&
        _colorBitDepths.Contains(input.ColorBitDepth);
}

public sealed class InterfaceCapabilities
{
    private readonly HashSet<int> _dscInputBitDepths;

    public InterfaceCapabilities(
        InterfaceKind kind,
        string profile,
        IEnumerable<LinkModeCapabilities> linkModes,
        IEnumerable<PixelFormatCapabilities> pixelFormats,
        bool supportsDsc = false,
        IEnumerable<int>? dscInputBitDepths = null)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        ArgumentException.ThrowIfNullOrWhiteSpace(profile);
        ArgumentNullException.ThrowIfNull(linkModes);
        ArgumentNullException.ThrowIfNull(pixelFormats);

        LinkModeCapabilities[] modes = [.. linkModes];
        PixelFormatCapabilities[] formats = [.. pixelFormats];
        int[] dscDepths = supportsDsc
            ? [.. (dscInputBitDepths ?? [8, 10, 12, 14, 16]).Distinct().Order()]
            : [];

        if (modes.Length == 0)
            throw new ArgumentException("At least one link mode is required.", nameof(linkModes));

        if (modes.Any(mode => mode is null))
            throw new ArgumentException("Link modes cannot contain null entries.", nameof(linkModes));

        bool containsWrongTransport = kind switch
        {
            InterfaceKind.DisplayPort or InterfaceKind.DisplayPortAltModeOnUsbC =>
                modes.Any(mode => mode.Transport != LinkTransport.DisplayPortMainLink),
            InterfaceKind.Hdmi => modes.Any(mode => mode.Transport is not (LinkTransport.HdmiTmds or LinkTransport.HdmiFixedRateLink)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        if (containsWrongTransport)
            throw new ArgumentException($"Link modes do not match interface kind {kind}.", nameof(linkModes));

        if (modes.Select(mode => mode.Name).Distinct(StringComparer.Ordinal).Count() != modes.Length)
            throw new ArgumentException("Link mode names must be unique within a profile.", nameof(linkModes));

        if (formats.Length == 0)
            throw new ArgumentException("At least one pixel format is required.", nameof(pixelFormats));

        if (formats.Any(format => format is null))
            throw new ArgumentException("Pixel formats cannot contain null entries.", nameof(pixelFormats));

        if (!supportsDsc && dscInputBitDepths is not null)
            throw new ArgumentException("DSC input depths cannot be supplied when DSC is disabled.", nameof(dscInputBitDepths));

        if (supportsDsc && (dscDepths.Length == 0 || dscDepths.Any(depth => depth <= 0)))
            throw new ArgumentException("DSC requires at least one positive input bit depth.", nameof(dscInputBitDepths));

        Kind = kind;
        Profile = profile;
        LinkModes = Array.AsReadOnly(modes);
        PixelFormats = Array.AsReadOnly(formats);
        SupportsDsc = supportsDsc;
        DscInputBitDepths = Array.AsReadOnly(dscDepths);
        _dscInputBitDepths = [.. dscDepths];
    }

    public InterfaceKind Kind { get; }
    public string Profile { get; }
    public IReadOnlyList<LinkModeCapabilities> LinkModes { get; }
    public IReadOnlyList<PixelFormatCapabilities> PixelFormats { get; }
    public bool SupportsDsc { get; }
    public IReadOnlyList<int> DscInputBitDepths { get; }

    public bool SupportsPixelFormat(CalculationInput input) =>
        PixelFormats.Any(format => format.Supports(input));

    public bool SupportsDscInput(CalculationInput input) =>
        SupportsDsc && _dscInputBitDepths.Contains(input.ColorBitDepth);
}

public sealed class InterfaceConfiguration
{
    private static readonly int[] DisplayPortDepths = [6, 8, 10, 12, 14, 16];
    private static readonly int[] HdmiDeepColorDepths = [8, 10, 12, 16];
    private static readonly int[] HdmiSubsampledDepths = [8, 10, 12];

    public InterfaceConfiguration(
        IEnumerable<InterfaceCapabilities>? displayPort = null,
        IEnumerable<InterfaceCapabilities>? hdmi = null,
        IEnumerable<InterfaceCapabilities>? displayPortAltModeOnUsbC = null)
    {
        DisplayPort = CopyAndValidate(displayPort, InterfaceKind.DisplayPort, nameof(displayPort));
        Hdmi = CopyAndValidate(hdmi, InterfaceKind.Hdmi, nameof(hdmi));
        DisplayPortAltModeOnUsbC = CopyAndValidate(
            displayPortAltModeOnUsbC,
            InterfaceKind.DisplayPortAltModeOnUsbC,
            nameof(displayPortAltModeOnUsbC));
    }

    public IReadOnlyList<InterfaceCapabilities> DisplayPort { get; }
    public IReadOnlyList<InterfaceCapabilities> Hdmi { get; }
    public IReadOnlyList<InterfaceCapabilities> DisplayPortAltModeOnUsbC { get; }

    public static InterfaceConfiguration ReferencePresets { get; } = CreateReferencePresets();

    private static InterfaceConfiguration CreateReferencePresets() =>
        new(
            displayPort:
            [
                DisplayPortProfile("2.1", DisplayPortLinkRate.Uhbr20, supportsDsc: true, supports420: true),
                DisplayPortProfile("1.4", DisplayPortLinkRate.Hbr3, supportsDsc: true, supports420: true),
                DisplayPortProfile("1.3", DisplayPortLinkRate.Hbr3, supportsDsc: false, supports420: true),
                DisplayPortProfile("1.2", DisplayPortLinkRate.Hbr2, supportsDsc: false, supports420: false),
                DisplayPortProfile("1.0-1.1a", DisplayPortLinkRate.Hbr, supportsDsc: false, supports420: false),
            ],
            hdmi:
            [
                HdmiProfile("2.2", HdmiFixedRateLinkRate.Gbps96, supportsDsc: true, supports420: true, deepColor: true),
                HdmiProfile("2.1", HdmiFixedRateLinkRate.Frl6, supportsDsc: true, supports420: true, deepColor: true),
                HdmiTmdsProfile("2.0", HdmiTmdsRate.MHz600, supports420: true, deepColor: true),
                HdmiTmdsProfile("1.4", HdmiTmdsRate.MHz340, supports420: false, deepColor: true),
                HdmiTmdsProfile("1.3", HdmiTmdsRate.MHz340, supports420: false, deepColor: true),
                HdmiTmdsProfile("1.0-1.2", HdmiTmdsRate.MHz165, supports420: false, deepColor: false),
            ],
            displayPortAltModeOnUsbC:
            [
                DisplayPortAltModeProfile("2.0", 4, DisplayPortLinkRate.Uhbr20),
                DisplayPortAltModeProfile("2.0", 2, DisplayPortLinkRate.Uhbr20),
                DisplayPortAltModeProfile("1.0", 4, DisplayPortLinkRate.Hbr3),
                DisplayPortAltModeProfile("1.0", 2, DisplayPortLinkRate.Hbr3),
            ]);

    /// <summary>
    /// Creates a DisplayPort profile containing every standard link rate up to the specified maximum.
    /// </summary>
    public static InterfaceCapabilities DisplayPortProfile(
        string profile,
        DisplayPortLinkRate maximumRate,
        bool supportsDsc = false,
        bool supports420 = true,
        int laneCount = 4) =>
        new(
            InterfaceKind.DisplayPort,
            profile,
            DisplayPortModes(maximumRate, laneCount),
            DisplayPortFormats(supports420),
            supportsDsc);

    /// <summary>
    /// Creates a DisplayPort Alt Mode profile containing every standard link rate up to the specified maximum.
    /// </summary>
    public static InterfaceCapabilities DisplayPortAltModeProfile(
        string profile,
        int laneCount,
        DisplayPortLinkRate maximumRate,
        bool supportsDsc = true,
        bool supports420 = true) =>
        new(
            InterfaceKind.DisplayPortAltModeOnUsbC,
            profile,
            DisplayPortModes(maximumRate, laneCount),
            DisplayPortFormats(supports420),
            supportsDsc);

    /// <summary>
    /// Creates an HDMI FRL profile containing every standard fixed-rate-link mode up to the specified maximum,
    /// plus the HDMI 2.0 TMDS modes.
    /// </summary>
    public static InterfaceCapabilities HdmiProfile(
        string profile,
        HdmiFixedRateLinkRate maximumFrlRate,
        bool supportsDsc = false,
        bool supports420 = true,
        bool deepColor = true) =>
        new(
            InterfaceKind.Hdmi,
            profile,
            [.. HdmiFrlModes(maximumFrlRate), .. HdmiTmdsModes(HdmiTmdsRate.MHz600)],
            HdmiFormats(supports420, deepColor),
            supportsDsc,
            dscInputBitDepths: [8, 10, 12, 16]);

    /// <summary>
    /// Creates a TMDS-only HDMI profile containing every standard TMDS mode up to the specified maximum.
    /// </summary>
    public static InterfaceCapabilities HdmiTmdsProfile(
        string profile,
        HdmiTmdsRate maximumTmdsRate,
        bool supports420 = true,
        bool deepColor = true) =>
        new(
            InterfaceKind.Hdmi,
            profile,
            HdmiTmdsModes(maximumTmdsRate),
            HdmiFormats(supports420, deepColor));

    private static IReadOnlyList<LinkModeCapabilities> DisplayPortModes(
        DisplayPortLinkRate maximumRate,
        int laneCount)
    {
        DisplayPortLinkRate[] descendingRates =
        [
            DisplayPortLinkRate.Uhbr20,
            DisplayPortLinkRate.Uhbr13Point5,
            DisplayPortLinkRate.Uhbr10,
            DisplayPortLinkRate.Hbr3,
            DisplayPortLinkRate.Hbr2,
            DisplayPortLinkRate.Hbr,
            DisplayPortLinkRate.Rbr,
        ];

        int maximumIndex = Array.IndexOf(descendingRates, maximumRate);
        if (maximumIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumRate));

        return [.. descendingRates[maximumIndex..].Select(rate => LinkModeCapabilities.DisplayPort(rate, laneCount))];
    }

    private static IReadOnlyList<LinkModeCapabilities> HdmiFrlModes(HdmiFixedRateLinkRate maximumRate)
    {
        HdmiFixedRateLinkRate[] descendingRates =
        [
            HdmiFixedRateLinkRate.Gbps96,
            HdmiFixedRateLinkRate.Gbps80,
            HdmiFixedRateLinkRate.Gbps64,
            HdmiFixedRateLinkRate.Frl6,
            HdmiFixedRateLinkRate.Frl5,
            HdmiFixedRateLinkRate.Frl4,
            HdmiFixedRateLinkRate.Frl3,
            HdmiFixedRateLinkRate.Frl2,
            HdmiFixedRateLinkRate.Frl1,
        ];

        int maximumIndex = Array.IndexOf(descendingRates, maximumRate);
        if (maximumIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumRate));

        return [.. descendingRates[maximumIndex..].Select(LinkModeCapabilities.HdmiFixedRateLink)];
    }

    private static IReadOnlyList<LinkModeCapabilities> HdmiTmdsModes(HdmiTmdsRate maximumRate)
    {
        HdmiTmdsRate[] descendingRates = [HdmiTmdsRate.MHz600, HdmiTmdsRate.MHz340, HdmiTmdsRate.MHz165];
        int maximumIndex = Array.IndexOf(descendingRates, maximumRate);
        if (maximumIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumRate));

        return [.. descendingRates[maximumIndex..].Select(LinkModeCapabilities.HdmiTmds)];
    }

    private static IReadOnlyList<PixelFormatCapabilities> DisplayPortFormats(bool supports420)
    {
        List<PixelFormatCapabilities> formats =
        [
            new(ColorEncoding.Rgb, ChromaSubsampling.Cs444, DisplayPortDepths),
            new(ColorEncoding.YCbCr, ChromaSubsampling.Cs444, DisplayPortDepths),
            new(ColorEncoding.YCbCr, ChromaSubsampling.Cs422, DisplayPortDepths),
        ];

        if (supports420)
            formats.Add(new PixelFormatCapabilities(ColorEncoding.YCbCr, ChromaSubsampling.Cs420, DisplayPortDepths));

        return formats;
    }

    private static IReadOnlyList<PixelFormatCapabilities> HdmiFormats(bool supports420, bool deepColor)
    {
        int[] fullChromaDepths = deepColor ? HdmiDeepColorDepths : [8];
        int[] subsampledDepths = deepColor ? HdmiSubsampledDepths : [8];
        List<PixelFormatCapabilities> formats =
        [
            new(ColorEncoding.Rgb, ChromaSubsampling.Cs444, fullChromaDepths),
            new(ColorEncoding.YCbCr, ChromaSubsampling.Cs444, fullChromaDepths),
            new(ColorEncoding.YCbCr, ChromaSubsampling.Cs422, subsampledDepths),
        ];

        if (supports420)
            formats.Add(new PixelFormatCapabilities(ColorEncoding.YCbCr, ChromaSubsampling.Cs420, fullChromaDepths));

        return formats;
    }

    private static IReadOnlyList<InterfaceCapabilities> CopyAndValidate(
        IEnumerable<InterfaceCapabilities>? capabilities,
        InterfaceKind expectedKind,
        string parameterName)
    {
        InterfaceCapabilities[] copy = [.. capabilities ?? []];
        if (copy.Any(capability => capability is null))
            throw new ArgumentException("Interface profiles cannot contain null entries.", parameterName);

        if (copy.Any(capability => capability.Kind != expectedKind))
            throw new ArgumentException($"All profiles must have interface kind {expectedKind}.", parameterName);

        return Array.AsReadOnly(copy);
    }
}
