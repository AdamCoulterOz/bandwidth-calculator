using UnitsNet;

namespace BandwidthCalculator.Tests;

public sealed class TimingCorrectnessTests
{
    // Reference vectors are independently derived from the equations and constants
    // in VESA CVT 1.2, not snapshots of this calculator's previous output.
    [Fact]
    public void Cvt_1920x1080_60_matches_Vesa_reference_vector()
    {
        DisplayTiming timing = Calculate(1920, 1080, 60d, TimingStandard.Cvt).Timing;

        Assert.Equal(2576, timing.HorizontalTotal);
        Assert.Equal(1120, timing.VerticalTotal);
        Assert.Equal(173d, timing.PixelClock.Megahertz, 3);
        Assert.Equal(59.9628d, timing.FrameFrequency.Hertz, 4);
    }

    [Fact]
    public void Cvt_aspect_ratio_matching_does_not_accept_integer_division_truncation()
    {
        // 1848:1040 is close to 16:9 but is not exactly 16:9. The custom-aspect
        // 10-line VSync therefore applies; truncated integer division would
        // incorrectly select the 5-line 16:9 VSync and produce 1055 total lines.
        DisplayTiming timing = Calculate(1848, 1040, 10d, TimingStandard.Cvt).Timing;

        Assert.Equal(1060, timing.VerticalTotal);
    }

    [Fact]
    public void Cvt_reduced_blanking_v1_matches_Vesa_reference_vector()
    {
        DisplayTiming timing = Calculate(1920, 1080, 60d, TimingStandard.CvtReducedBlanking).Timing;

        Assert.Equal(2080, timing.HorizontalTotal);
        Assert.Equal(1111, timing.VerticalTotal);
        Assert.Equal(138.5d, timing.PixelClock.Megahertz, 3);
        Assert.Equal(59.9339d, timing.FrameFrequency.Hertz, 4);
    }

    [Fact]
    public void Cvt_reduced_blanking_v2_preserves_one_pixel_granularity()
    {
        DisplayTiming timing = Calculate(1366, 768, 60d, TimingStandard.CvtReducedBlankingV2).Timing;

        Assert.Equal(1366, timing.HorizontalActive);
        Assert.Equal(1446, timing.HorizontalTotal);
        Assert.Equal(790, timing.VerticalTotal);
        Assert.Equal(68.540d, timing.PixelClock.Megahertz, 3);
    }

    [Fact]
    public void Cvt_reduced_blanking_v2_video_optimized_uses_nominal_blanking_and_1000_over_1001_clock()
    {
        DisplayTiming timing = Calculate(
            4096,
            2160,
            60d,
            TimingStandard.CvtReducedBlankingV2,
            convention: RefreshRateConvention.VideoOptimized).Timing;

        Assert.Equal(4176, timing.HorizontalTotal);
        Assert.Equal(2222, timing.VerticalTotal);
        Assert.Equal(556.188d, timing.PixelClock.Megahertz, 3);
        Assert.InRange((double)timing.FrameFrequency.Hertz, 59.9399d, 59.9401d);
    }

    [Fact]
    public void Cvt_reduced_blanking_v3_defaults_and_custom_blanking_are_applied()
    {
        DisplayTiming defaults = Calculate(1920, 1080, 60d, TimingStandard.CvtReducedBlankingV3).Timing;
        DisplayTiming custom = Calculate(
            1920,
            1080,
            60d,
            TimingStandard.CvtReducedBlankingV3,
            rbV3: new CvtReducedBlankingV3Options(
                HorizontalBlankPixels: 160,
                MinimumVerticalBlankMicroseconds: 300,
                EarlyVsync: true)).Timing;

        Assert.Equal(2000, defaults.HorizontalTotal);
        Assert.Equal(1111, defaults.VerticalTotal);
        Assert.Equal(133.320d, defaults.PixelClock.Megahertz, 3);
        Assert.Equal(2080, custom.HorizontalTotal);
        Assert.Equal(1100, custom.VerticalTotal);
        Assert.Equal(137.280d, custom.PixelClock.Megahertz, 3);
    }

    [Fact]
    public void Dmt_uses_corrected_rb_modes_and_standard_ids()
    {
        DisplayTiming highRefresh = Calculate(
            1792,
            1344,
            120d,
            TimingStandard.Dmt,
            standardTimingId: 0x40).Timing;
        DisplayTiming rbV2 = Calculate(
            4096,
            2160,
            60d,
            TimingStandard.Dmt,
            standardTimingId: 0x57).Timing;
        DisplayTiming rbV2Video = Calculate(
            4096,
            2160,
            59.94d,
            TimingStandard.Dmt,
            standardTimingId: 0x58).Timing;

        Assert.Equal(0x40, highRefresh.StandardTimingId);
        Assert.Equal(333.25d, highRefresh.PixelClock.Megahertz, 3);
        Assert.Equal(0x57, rbV2.StandardTimingId);
        Assert.Equal(556.744d, rbV2.PixelClock.Megahertz, 3);
        Assert.Equal(0x58, rbV2Video.StandardTimingId);
        Assert.Equal(556.188d, rbV2Video.PixelClock.Megahertz, 3);
    }

    [Fact]
    public void Dmt_requires_an_id_when_multiple_standard_modes_have_the_same_label()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            Calculate(1366, 768, 60d, TimingStandard.Dmt));

        Assert.Contains("ambiguous", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Standard_mode_matching_does_not_round_to_a_whole_hertz()
    {
        Assert.Throws<ArgumentException>(() =>
            Calculate(1920, 1080, 59.5d, TimingStandard.Cta861, standardTimingId: 16));
    }

    [Fact]
    public void Cta_interlaced_mode_reports_distinct_frame_and_field_rates()
    {
        DisplayTiming timing = Calculate(
            1920,
            1080,
            60d,
            TimingStandard.Cta861,
            scanMode: ScanMode.Interlaced,
            standardTimingId: 5).Timing;

        Assert.Equal(ScanMode.Interlaced, timing.ScanMode);
        Assert.Equal(2200, timing.HorizontalTotal);
        Assert.Equal(1125, timing.VerticalTotal);
        Assert.Equal(74.25d, timing.PixelClock.Megahertz, 3);
        Assert.Equal(30d, timing.FrameFrequency.Hertz, 6);
        Assert.Equal(60d, timing.FieldFrequency.Hertz, 6);
    }

    [Fact]
    public void Cta_automatic_matches_the_fractional_variant_without_conflating_it_with_60_hz()
    {
        DisplayTiming exact = Calculate(
            1920,
            1080,
            60d,
            TimingStandard.Cta861,
            standardTimingId: 16).Timing;
        DisplayTiming fractional = Calculate(
            1920,
            1080,
            59.94d,
            TimingStandard.Cta861,
            standardTimingId: 16).Timing;

        Assert.Equal(148.5d, exact.PixelClock.Megahertz, 6);
        Assert.Equal(148.351648d, fractional.PixelClock.Megahertz, 6);
        Assert.InRange((double)fractional.FrameFrequency.Hertz, 59.9399d, 59.9401d);
    }

    [Fact]
    public void Cta_extended_vic_range_preserves_193_to_219_identifiers()
    {
        DisplayTiming timing = Calculate(
            5120,
            2160,
            120d,
            TimingStandard.Cta861,
            standardTimingId: 193).Timing;

        Assert.Equal(193, timing.StandardTimingId);
        Assert.Equal(1485d, timing.PixelClock.Megahertz, 3);
        Assert.Equal(5500, timing.HorizontalTotal);
        Assert.Equal(2250, timing.VerticalTotal);
    }

    [Theory]
    [InlineData(1920, 1080, 60d, RefreshRateConvention.Exact, 2080, 1120, 139.776d, 60d)]
    [InlineData(3840, 2160, 120d, RefreshRateConvention.Exact, 3968, 2300, 1095.168d, 120d)]
    [InlineData(3840, 2160, 59.94d, RefreshRateConvention.Automatic, 3960, 2240, 531.692307692d, 59.94005994d)]
    [InlineData(3840, 2160, 60d, RefreshRateConvention.VideoOptimized, 3960, 2240, 531.692307692d, 59.94005994d)]
    public void Cta_optimized_video_timing_matches_Cta861I_reference_algorithm(
        int horizontalPixels,
        int verticalPixels,
        double refreshRate,
        RefreshRateConvention convention,
        int horizontalTotal,
        int verticalTotal,
        double pixelClockMhz,
        double frameRate)
    {
        DisplayTiming timing = Calculate(
            horizontalPixels,
            verticalPixels,
            refreshRate,
            TimingStandard.Cta861OptimizedVideoTiming,
            convention: convention).Timing;

        Assert.Equal(horizontalTotal, timing.HorizontalTotal);
        Assert.Equal(verticalTotal, timing.VerticalTotal);
        Assert.Equal(pixelClockMhz, timing.PixelClock.Megahertz, 6);
        Assert.Equal(frameRate, timing.FrameFrequency.Hertz, 6);
    }

    [Fact]
    public void Every_resolved_frequency_is_derived_consistently_from_clock_and_totals()
    {
        DisplayTiming timing = Calculate(2560, 1440, 144d, TimingStandard.CvtReducedBlankingV2).Timing;
        double expectedHorizontalHz = timing.PixelClock.Hertz / timing.HorizontalTotal;
        double expectedFrameHz = expectedHorizontalHz / timing.VerticalTotal;

        Assert.Equal(expectedHorizontalHz, timing.HorizontalFrequency.Hertz, 6);
        Assert.Equal(expectedFrameHz, timing.FrameFrequency.Hertz, 9);
        Assert.Equal(expectedFrameHz, timing.FieldFrequency.Hertz, 9);
    }

    private static CalculationResult Calculate(
        int horizontalPixels,
        int verticalPixels,
        double refreshRateHz,
        TimingStandard standard,
        ScanMode scanMode = ScanMode.Progressive,
        RefreshRateConvention convention = RefreshRateConvention.Automatic,
        int? standardTimingId = null,
        CvtReducedBlankingV3Options? rbV3 = null) =>
        BandwidthCalculator.Calculate(
            new CalculationInput(
                VerticalPixels: verticalPixels,
                HorizontalPixels: horizontalPixels,
                RefreshRate: Frequency.FromHertz(refreshRateHz),
                ColorBitDepth: 8,
                ColorEncoding: ColorEncoding.Rgb,
                ChromaSubsampling: ChromaSubsampling.Cs444,
                Timing: standard,
                ScanMode: scanMode,
                RefreshRateConvention: convention,
                StandardTimingId: standardTimingId,
                CvtReducedBlankingV3: rbV3));
}
