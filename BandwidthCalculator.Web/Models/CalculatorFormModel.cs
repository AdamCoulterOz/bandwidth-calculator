using System.ComponentModel.DataAnnotations;
using UnitsNet;

namespace BandwidthCalculator.Web.Models;

public enum HdmiCapabilityMode
{
    FixedRateLink,
    Tmds,
}

public enum DisplayDiagonalUnit
{
    Inches,
    Centimeters,
}

public sealed class CalculatorFormModel
{
    public string ResolutionPresetId { get; set; } = "3840x2160";

    [Range(1, int.MaxValue, ErrorMessage = "Width must be greater than zero.")]
    public int Width { get; set; } = 3840;

    [Range(1, int.MaxValue, ErrorMessage = "Height must be greater than zero.")]
    public int Height { get; set; } = 2160;

    [Range(1, int.MaxValue, ErrorMessage = "The horizontal aspect value must be greater than zero.")]
    public int AspectRatioWidth { get; set; } = 16;

    [Range(1, int.MaxValue, ErrorMessage = "The vertical aspect value must be greater than zero.")]
    public int AspectRatioHeight { get; set; } = 9;

    public bool LockAspectRatio { get; set; } = true;

    [Range(0.1d, double.MaxValue, ErrorMessage = "Display size must be greater than zero.")]
    public double DisplayDiagonal { get; set; } = 27d;

    public DisplayDiagonalUnit DisplayDiagonalUnit { get; set; } = DisplayDiagonalUnit.Inches;

    public double PixelsPerInch
    {
        get
        {
            if (Width <= 0 || Height <= 0 || !double.IsFinite(DisplayDiagonal) || DisplayDiagonal <= 0d)
            {
                return double.NaN;
            }

            Length diagonal = DisplayDiagonalUnit switch
            {
                DisplayDiagonalUnit.Inches => Length.FromInches(DisplayDiagonal),
                DisplayDiagonalUnit.Centimeters => Length.FromCentimeters(DisplayDiagonal),
                _ => throw new InvalidOperationException($"Unsupported display diagonal unit {DisplayDiagonalUnit}."),
            };

            double width = Width;
            double height = Height;
            double pixelDiagonal = Math.Sqrt((width * width) + (height * height));
            return pixelDiagonal / diagonal.Inches;
        }
    }

    [Range(0.001d, double.MaxValue, ErrorMessage = "Refresh rate must be greater than zero.")]
    public double RefreshRateHertz { get; set; } = 120d;

    public int ColorBitDepth { get; set; } = 10;

    public ColorEncoding ColorEncoding { get; set; } = ColorEncoding.Rgb;

    public ChromaSubsampling ChromaSubsampling { get; set; } = ChromaSubsampling.Cs444;

    public TimingStandard Timing { get; set; } = TimingStandard.CvtReducedBlankingV2;

    public ScanMode ScanMode { get; set; } = ScanMode.Progressive;

    public RefreshRateConvention RefreshRateConvention { get; set; } = RefreshRateConvention.Automatic;

    [Range(1, int.MaxValue, ErrorMessage = "A standard timing ID must be greater than zero.")]
    public int? StandardTimingId { get; set; }

    public int RbV3HorizontalBlankPixels { get; set; } = 80;

    public int RbV3MinimumVerticalBlankMicroseconds { get; set; } = 460;

    public bool RbV3EarlyVsync { get; set; }

    public bool UseExactCapabilities { get; set; }

    public DisplayPortLinkRate DisplayPortMaximumRate { get; set; } = DisplayPortLinkRate.Hbr3;

    public int DisplayPortLaneCount { get; set; } = 4;

    public bool DisplayPortSupportsDsc { get; set; } = true;

    public bool DisplayPortSupports420 { get; set; } = true;

    public HdmiCapabilityMode HdmiMode { get; set; } = HdmiCapabilityMode.FixedRateLink;

    public HdmiFixedRateLinkRate HdmiMaximumFrlRate { get; set; } = HdmiFixedRateLinkRate.Frl6;

    public HdmiTmdsRate HdmiMaximumTmdsRate { get; set; } = HdmiTmdsRate.MHz600;

    public bool HdmiSupportsDsc { get; set; } = true;

    public bool HdmiSupports420 { get; set; } = true;

    public bool HdmiSupportsDeepColor { get; set; } = true;

    public DisplayPortLinkRate AltModeMaximumRate { get; set; } = DisplayPortLinkRate.Hbr3;

    public int AltModeLaneCount { get; set; } = 2;

    public bool AltModeSupportsDsc { get; set; } = true;

    public bool AltModeSupports420 { get; set; } = true;

    public CalculationInput ToCalculationInput()
    {
        bool usesStandardTimingId = Timing is TimingStandard.Dmt or TimingStandard.Cta861;
        bool usesRbV3 = Timing == TimingStandard.CvtReducedBlankingV3;

        return new CalculationInput(
            VerticalPixels: Height,
            HorizontalPixels: Width,
            RefreshRate: Frequency.FromHertz(RefreshRateHertz),
            ColorBitDepth: ColorBitDepth,
            ColorEncoding: ColorEncoding,
            ChromaSubsampling: ColorEncoding == ColorEncoding.Rgb
                ? ChromaSubsampling.Cs444
                : ChromaSubsampling,
            Timing: Timing,
            ScanMode: Timing == TimingStandard.Cta861OptimizedVideoTiming
                ? ScanMode.Progressive
                : ScanMode,
            RefreshRateConvention: RefreshRateConvention,
            StandardTimingId: usesStandardTimingId ? StandardTimingId : null,
            CvtReducedBlankingV3: usesRbV3
                ? new CvtReducedBlankingV3Options(
                    RbV3HorizontalBlankPixels,
                    RbV3MinimumVerticalBlankMicroseconds,
                    RbV3EarlyVsync)
                : null);
    }

    public InterfaceConfiguration ToInterfaceConfiguration()
    {
        InterfaceCapabilities displayPort = InterfaceConfiguration.DisplayPortProfile(
            "Custom DisplayPort target",
            DisplayPortMaximumRate,
            supportsDsc: DisplayPortSupportsDsc,
            supports420: DisplayPortSupports420,
            laneCount: DisplayPortLaneCount);

        InterfaceCapabilities hdmi = HdmiMode == HdmiCapabilityMode.FixedRateLink
            ? InterfaceConfiguration.HdmiProfile(
                "Custom HDMI target",
                HdmiMaximumFrlRate,
                supportsDsc: HdmiSupportsDsc,
                supports420: HdmiSupports420,
                deepColor: HdmiSupportsDeepColor)
            : InterfaceConfiguration.HdmiTmdsProfile(
                "Custom HDMI target",
                HdmiMaximumTmdsRate,
                supports420: HdmiSupports420,
                deepColor: HdmiSupportsDeepColor);

        InterfaceCapabilities altMode = InterfaceConfiguration.DisplayPortAltModeProfile(
            "Custom USB-C DisplayPort Alt Mode target",
            AltModeLaneCount,
            AltModeMaximumRate,
            supportsDsc: AltModeSupportsDsc,
            supports420: AltModeSupports420);

        return new InterfaceConfiguration(
            displayPort: [displayPort],
            hdmi: [hdmi],
            displayPortAltModeOnUsbC: [altMode]);
    }
}
