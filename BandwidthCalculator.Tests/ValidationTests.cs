using UnitsNet;

namespace BandwidthCalculator.Tests;

public sealed class ValidationTests
{
    [Fact]
    public void Calculate_rejects_null_input()
    {
        Assert.Throws<ArgumentNullException>(() => global::BandwidthCalculator.BandwidthCalculator.Calculate(null!));
    }

    [Fact]
    public void Calculate_rejects_null_interface_configuration()
    {
        Assert.Throws<ArgumentNullException>(() =>
            global::BandwidthCalculator.BandwidthCalculator.Calculate(ValidInput(), null!));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1d)]
    [InlineData(0d)]
    public void Calculate_rejects_non_finite_or_non_positive_refresh_rate(double refreshRateHertz)
    {
        CalculationInput input = ValidInput() with { RefreshRate = Frequency.FromHertz(refreshRateHertz) };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.RefreshRate), exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void Calculate_rejects_invalid_or_unrepresentable_vertical_dimensions(int verticalPixels)
    {
        CalculationInput input = ValidInput() with { VerticalPixels = verticalPixels };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.VerticalPixels), exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void Calculate_rejects_invalid_or_unrepresentable_horizontal_dimensions(int horizontalPixels)
    {
        CalculationInput input = ValidInput() with { HorizontalPixels = horizontalPixels };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.HorizontalPixels), exception.ParamName);
    }

    [Fact]
    public void Calculate_rejects_width_below_the_timing_cell_granularity()
    {
        CalculationInput input = ValidInput() with
        {
            HorizontalPixels = 7,
            Timing = TimingStandard.Cvt,
        };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.HorizontalPixels), exception.ParamName);
    }

    [Fact]
    public void Calculate_preserves_reduced_blanking_v2_one_pixel_granularity()
    {
        CalculationInput input = ValidInput() with { HorizontalPixels = 7 };

        CalculationResult result = Calculate(input);

        Assert.Equal(7, result.Timing.HorizontalActive);
    }

    [Fact]
    public void Calculate_rejects_refresh_rates_shorter_than_required_blanking()
    {
        CalculationInput input = ValidInput() with
        {
            Timing = TimingStandard.Cvt,
            RefreshRate = Duration.FromMicroseconds(550d).Inverse(),
        };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.RefreshRate), exception.ParamName);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(18)]
    public void Calculate_rejects_unsupported_color_depth(int colorBitDepth)
    {
        CalculationInput input = ValidInput() with { ColorBitDepth = colorBitDepth };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.ColorBitDepth), exception.ParamName);
    }

    [Fact]
    public void Calculate_rejects_unknown_color_encoding()
    {
        CalculationInput input = ValidInput() with { ColorEncoding = (ColorEncoding)int.MaxValue };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.ColorEncoding), exception.ParamName);
    }

    [Fact]
    public void Calculate_rejects_unknown_chroma_subsampling()
    {
        CalculationInput input = ValidInput() with { ChromaSubsampling = (ChromaSubsampling)int.MaxValue };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.ChromaSubsampling), exception.ParamName);
    }

    [Fact]
    public void Calculate_rejects_unknown_timing_standard()
    {
        CalculationInput input = ValidInput() with { Timing = (TimingStandard)int.MaxValue };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.Timing), exception.ParamName);
    }

    [Fact]
    public void Calculate_rejects_unknown_scan_mode()
    {
        CalculationInput input = ValidInput() with { ScanMode = (ScanMode)int.MaxValue };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.ScanMode), exception.ParamName);
    }

    [Fact]
    public void Calculate_rejects_unknown_refresh_rate_convention()
    {
        CalculationInput input = ValidInput() with { RefreshRateConvention = (RefreshRateConvention)int.MaxValue };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.RefreshRateConvention), exception.ParamName);
    }

    [Theory]
    [InlineData(ChromaSubsampling.Cs422)]
    [InlineData(ChromaSubsampling.Cs420)]
    public void Calculate_rejects_subsampled_rgb(ChromaSubsampling chromaSubsampling)
    {
        CalculationInput input = ValidInput() with { ChromaSubsampling = chromaSubsampling };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.ChromaSubsampling), exception.ParamName);
    }

    [Fact]
    public void Calculate_accepts_subsampled_ycbcr()
    {
        CalculationInput input = ValidInput() with
        {
            ColorEncoding = ColorEncoding.YCbCr,
            ChromaSubsampling = ChromaSubsampling.Cs420,
        };

        CalculationResult result = Calculate(input);

        Assert.True(result.Bandwidth.Maximum > BitRate.Zero);
    }

    [Theory]
    [MemberData(nameof(InvalidReducedBlankingV3Options))]
    public void Calculate_rejects_invalid_reduced_blanking_v3_options(CvtReducedBlankingV3Options options)
    {
        CalculationInput input = ValidInput() with
        {
            Timing = TimingStandard.CvtReducedBlankingV3,
            CvtReducedBlankingV3 = options,
        };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.CvtReducedBlankingV3), exception.ParamName);
    }

    public static TheoryData<CvtReducedBlankingV3Options> InvalidReducedBlankingV3Options =>
        new()
        {
            new CvtReducedBlankingV3Options(HorizontalBlankPixels: 72),
            new CvtReducedBlankingV3Options(HorizontalBlankPixels: 208),
            new CvtReducedBlankingV3Options(HorizontalBlankPixels: 82),
            new CvtReducedBlankingV3Options(MinimumVerticalBlankMicroseconds: 299),
            new CvtReducedBlankingV3Options(MinimumVerticalBlankMicroseconds: 921),
        };

    [Theory]
    [InlineData(80, 300)]
    [InlineData(200, 920)]
    public void Calculate_accepts_reduced_blanking_v3_option_boundaries(
        int horizontalBlankPixels,
        int minimumVerticalBlankMicroseconds)
    {
        CalculationInput input = ValidInput() with
        {
            Timing = TimingStandard.CvtReducedBlankingV3,
            CvtReducedBlankingV3 = new CvtReducedBlankingV3Options(
                horizontalBlankPixels,
                minimumVerticalBlankMicroseconds),
        };

        CalculationResult result = Calculate(input);

        Assert.Equal(horizontalBlankPixels, result.Timing.HorizontalBlank);
    }

    [Fact]
    public void Calculate_rejects_reduced_blanking_v3_options_for_another_timing_standard()
    {
        CalculationInput input = ValidInput() with { CvtReducedBlankingV3 = new CvtReducedBlankingV3Options() };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.CvtReducedBlankingV3), exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Calculate_rejects_non_positive_standard_timing_id(int standardTimingId)
    {
        CalculationInput input = ValidInput() with
        {
            Timing = TimingStandard.Dmt,
            StandardTimingId = standardTimingId,
        };

        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.StandardTimingId), exception.ParamName);
    }

    [Fact]
    public void Calculate_rejects_standard_timing_id_for_calculated_timing()
    {
        CalculationInput input = ValidInput() with { StandardTimingId = 1 };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.StandardTimingId), exception.ParamName);
    }

    [Theory]
    [InlineData(TimingStandard.Cvt)]
    [InlineData(TimingStandard.CvtReducedBlanking)]
    [InlineData(TimingStandard.CvtReducedBlankingV3)]
    public void Calculate_rejects_video_optimized_refresh_for_unsupported_timing(TimingStandard timing)
    {
        CalculationInput input = ValidInput() with
        {
            Timing = timing,
            RefreshRateConvention = RefreshRateConvention.VideoOptimized,
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.RefreshRateConvention), exception.ParamName);
    }

    [Fact]
    public void Calculate_accepts_video_optimized_refresh_for_reduced_blanking_v2()
    {
        CalculationInput input = ValidInput() with
        {
            RefreshRateConvention = RefreshRateConvention.VideoOptimized,
        };

        CalculationResult result = Calculate(input);

        Assert.True(result.Timing.FrameFrequency > Frequency.Zero);
    }

    [Fact]
    public void Cta_optimized_video_timing_rejects_non_integer_nominal_rate()
    {
        CalculationInput input = ValidInput() with
        {
            Timing = TimingStandard.Cta861OptimizedVideoTiming,
            RefreshRate = Frequency.FromHertz(59.5d),
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() => Calculate(input));

        Assert.Equal(nameof(CalculationInput.RefreshRate), exception.ParamName);
    }

    [Theory]
    [InlineData(ScanMode.Interlaced, 1920)]
    [InlineData(ScanMode.Progressive, 1919)]
    public void Cta_optimized_video_timing_rejects_unsupported_geometry(ScanMode scanMode, int horizontalPixels)
    {
        CalculationInput input = ValidInput() with
        {
            Timing = TimingStandard.Cta861OptimizedVideoTiming,
            ScanMode = scanMode,
            HorizontalPixels = horizontalPixels,
        };

        Assert.Throws<ArgumentException>(() => Calculate(input));
    }

    private static CalculationInput ValidInput() =>
        new(
            VerticalPixels: 1080,
            HorizontalPixels: 1920,
            RefreshRate: Frequency.FromHertz(60d),
            ColorBitDepth: 10,
            ColorEncoding: ColorEncoding.Rgb,
            ChromaSubsampling: ChromaSubsampling.Cs444,
            Timing: TimingStandard.CvtReducedBlankingV2);

    private static CalculationResult Calculate(CalculationInput input) =>
        global::BandwidthCalculator.BandwidthCalculator.Calculate(input);
}
