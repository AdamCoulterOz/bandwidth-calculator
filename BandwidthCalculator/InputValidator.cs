using UnitsNet;

namespace BandwidthCalculator;

internal static class InputValidator
{
    private const int CellGranularity = 8;
    private const int CvtAndReducedBlankingV1MinimumVerticalBlankLines = 20;
    private const int ReducedBlankingV2AndV3MinimumVerticalBlankLines = 15;
    private const int CvtMinimumVerticalSyncAndBackPorchMicroseconds = 550;
    private const int ReducedBlankingMinimumVerticalBlankMicroseconds = 460;

    internal static void Validate(CalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidateEnums(input);
        ValidateDimensions(input);

        if (input.ColorBitDepth is not (6 or 8 or 10 or 12 or 14 or 16))
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.ColorBitDepth),
                input.ColorBitDepth,
                "Color bit depth must be one of 6, 8, 10, 12, 14, or 16.");
        }

        if (input.ColorEncoding == ColorEncoding.Rgb && input.ChromaSubsampling != ChromaSubsampling.Cs444)
        {
            throw new ArgumentException(
                "RGB color encoding only supports 4:4:4 chroma sampling.",
                nameof(input.ChromaSubsampling));
        }

        ValidateTimingOptions(input);
        ValidateRefreshRate(input);
        ValidateCvtGeometry(input);
    }

    private static void ValidateEnums(CalculationInput input)
    {
        if (!Enum.IsDefined(input.ColorEncoding))
            throw new ArgumentOutOfRangeException(nameof(input.ColorEncoding), input.ColorEncoding, "Unknown color encoding.");

        if (!Enum.IsDefined(input.ChromaSubsampling))
            throw new ArgumentOutOfRangeException(nameof(input.ChromaSubsampling), input.ChromaSubsampling, "Unknown chroma subsampling.");

        if (!Enum.IsDefined(input.Timing))
            throw new ArgumentOutOfRangeException(nameof(input.Timing), input.Timing, "Unknown timing standard.");

        if (!Enum.IsDefined(input.ScanMode))
            throw new ArgumentOutOfRangeException(nameof(input.ScanMode), input.ScanMode, "Unknown scan mode.");

        if (!Enum.IsDefined(input.RefreshRateConvention))
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.RefreshRateConvention),
                input.RefreshRateConvention,
                "Unknown refresh-rate convention.");
        }
    }

    private static void ValidateDimensions(CalculationInput input)
    {
        if (input.VerticalPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.VerticalPixels),
                input.VerticalPixels,
                "Vertical pixels must be greater than 0.");
        }

        if (input.HorizontalPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.HorizontalPixels),
                input.HorizontalPixels,
                "Horizontal pixels must be greater than 0.");
        }

        bool usesEightPixelCells = input.Timing is
            TimingStandard.Cvt or
            TimingStandard.CvtReducedBlanking or
            TimingStandard.CvtReducedBlankingV3;
        if (usesEightPixelCells && input.HorizontalPixels < CellGranularity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.HorizontalPixels),
                input.HorizontalPixels,
                $"Horizontal pixels must be at least one {CellGranularity}-pixel timing cell.");
        }

        // Every supported timing has at least some horizontal and vertical blanking.
        // These checks reject values for which even those minimal totals cannot fit in the
        // integer timing model, without imposing a display-size policy maximum.
        if (input.HorizontalPixels > int.MaxValue - CellGranularity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.HorizontalPixels),
                input.HorizontalPixels,
                "Horizontal pixels leave no representable space for timing blanking.");
        }

        if (input.VerticalPixels == int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.VerticalPixels),
                input.VerticalPixels,
                "Vertical pixels leave no representable space for timing blanking.");
        }
    }

    private static void ValidateRefreshRate(CalculationInput input)
    {
        double refreshRateHertz = input.RefreshRate.Hertz;
        if (!double.IsFinite(refreshRateHertz) || refreshRateHertz <= Frequency.Zero.Hertz)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.RefreshRate),
                input.RefreshRate,
                "Refresh rate must be finite and greater than 0 Hz.");
        }

        int? requiredBlankMicroseconds = input.Timing switch
        {
            TimingStandard.Cvt => CvtMinimumVerticalSyncAndBackPorchMicroseconds,
            TimingStandard.CvtReducedBlanking or TimingStandard.CvtReducedBlankingV2 => ReducedBlankingMinimumVerticalBlankMicroseconds,
            TimingStandard.CvtReducedBlankingV3 =>
                (input.CvtReducedBlankingV3 ?? new CvtReducedBlankingV3Options()).MinimumVerticalBlankMicroseconds,
            TimingStandard.Cta861OptimizedVideoTiming => ReducedBlankingMinimumVerticalBlankMicroseconds,
            _ => null,
        };

        Frequency fieldRate = input.ScanMode == ScanMode.Interlaced
            ? input.RefreshRate * 2d
            : input.RefreshRate;
        if (requiredBlankMicroseconds is int blankMicroseconds &&
            fieldRate >= Duration.FromMicroseconds(blankMicroseconds).Inverse())
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.RefreshRate),
                input.RefreshRate,
                $"Refresh period must be longer than the timing standard's {blankMicroseconds} microsecond blanking interval.");
        }
    }

    private static void ValidateTimingOptions(CalculationInput input)
    {
        if (input.StandardTimingId is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.StandardTimingId),
                input.StandardTimingId,
                "Standard timing ID must be greater than 0 when supplied.");
        }

        if (input.StandardTimingId.HasValue && input.Timing is not (TimingStandard.Dmt or TimingStandard.Cta861))
        {
            throw new ArgumentException(
                "A standard timing ID can only be used with DMT or CTA-861 timing.",
                nameof(input.StandardTimingId));
        }

        if (input.CvtReducedBlankingV3 is not null && input.Timing != TimingStandard.CvtReducedBlankingV3)
        {
            throw new ArgumentException(
                "CVT Reduced Blanking V3 options can only be used with CVT Reduced Blanking V3 timing.",
                nameof(input.CvtReducedBlankingV3));
        }

        if (input.CvtReducedBlankingV3 is { } rbv3)
        {
            if (rbv3.HorizontalBlankPixels is < 80 or > 200 || rbv3.HorizontalBlankPixels % CellGranularity != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(input.CvtReducedBlankingV3),
                    rbv3,
                    "CVT Reduced Blanking V3 horizontal blanking must be from 80 through 200 pixels and a multiple of 8 pixels.");
            }

            if (rbv3.MinimumVerticalBlankMicroseconds is < 300 or > 920)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(input.CvtReducedBlankingV3),
                    rbv3,
                    "CVT Reduced Blanking V3 minimum vertical blanking must be from 300 through 920 microseconds.");
            }
        }

        if (input.RefreshRateConvention == RefreshRateConvention.VideoOptimized &&
            input.Timing is not (
                TimingStandard.CvtReducedBlankingV2 or
                TimingStandard.Dmt or
                TimingStandard.Cta861 or
                TimingStandard.Cta861OptimizedVideoTiming))
        {
            throw new ArgumentException(
                "The video-optimized refresh convention is only valid for CVT Reduced Blanking V2, DMT, CTA-861 VIC, or CTA-861 OVT timing.",
                nameof(input.RefreshRateConvention));
        }

        if (input.Timing == TimingStandard.Cta861OptimizedVideoTiming)
        {
            if (input.ScanMode != ScanMode.Progressive)
            {
                throw new ArgumentException(
                    "CTA-861 Optimized Video Timing only defines progressive timings.",
                    nameof(input.ScanMode));
            }

            if (input.HorizontalPixels % CellGranularity != 0)
            {
                throw new ArgumentException(
                    "CTA-861 Optimized Video Timing requires horizontal active pixels to be a multiple of 8.",
                    nameof(input.HorizontalPixels));
            }

            double nominalRefreshRate = input.RefreshRateConvention == RefreshRateConvention.Automatic &&
                                        TryInferVideoNominal(input.RefreshRate.Hertz, out double inferredRate)
                ? inferredRate
                : input.RefreshRate.Hertz;
            if (nominalRefreshRate != Math.Truncate(nominalRefreshRate))
            {
                throw new ArgumentException(
                    "CTA-861 Optimized Video Timing requires an integer nominal refresh rate; use Automatic for a standard 1000/1001 rate or VideoOptimized with its nominal integer rate.",
                    nameof(input.RefreshRate));
            }
        }
    }

    private static bool TryInferVideoNominal(double actualRate, out double nominalRate)
    {
        const double videoRateMultiplier = 1000d / 1001d;
        const double toleranceHz = 0.001d;

        nominalRate = Math.Round(actualRate / videoRateMultiplier);
        return nominalRate > 0d &&
               nominalRate == Math.Truncate(nominalRate) &&
               Math.Abs(actualRate - nominalRate * videoRateMultiplier) <= toleranceHz;
    }

    private static void ValidateCvtGeometry(CalculationInput input)
    {
        if (input.Timing is not (
            TimingStandard.Cvt or
            TimingStandard.CvtReducedBlanking or
            TimingStandard.CvtReducedBlankingV2 or
            TimingStandard.CvtReducedBlankingV3))
        {
            return;
        }

        int horizontalCellGranularity = input.Timing == TimingStandard.CvtReducedBlankingV2
            ? 1
            : CellGranularity;
        int roundedHorizontalActive = input.HorizontalPixels / horizontalCellGranularity * horizontalCellGranularity;
        long maximumHorizontalTotal = input.Timing switch
        {
            // For positive CVT horizontal periods the ideal duty cycle is always below
            // 30%, so blanking is strictly less than 3/7 of active width.
            TimingStandard.Cvt => roundedHorizontalActive + (long)roundedHorizontalActive * 3 / 7,
            TimingStandard.CvtReducedBlanking => (long)roundedHorizontalActive + 160,
            TimingStandard.CvtReducedBlankingV2 => (long)roundedHorizontalActive + 80,
            TimingStandard.CvtReducedBlankingV3 =>
                (long)roundedHorizontalActive + (input.CvtReducedBlankingV3 ?? new CvtReducedBlankingV3Options()).HorizontalBlankPixels,
            _ => throw new InvalidOperationException("Unexpected CVT timing standard."),
        };

        if (maximumHorizontalTotal > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.HorizontalPixels),
                input.HorizontalPixels,
                "Horizontal timing total cannot be represented by the timing model.");
        }

        int minimumVerticalBlankLinesPerField = input.Timing is
            TimingStandard.CvtReducedBlankingV2 or TimingStandard.CvtReducedBlankingV3
                ? ReducedBlankingV2AndV3MinimumVerticalBlankLines
                : CvtAndReducedBlankingV1MinimumVerticalBlankLines;
        long minimumAdditionalVerticalLines = input.ScanMode == ScanMode.Interlaced
            ? minimumVerticalBlankLinesPerField * 2L + 1L
            : minimumVerticalBlankLinesPerField;
        if ((long)input.VerticalPixels + minimumAdditionalVerticalLines > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.VerticalPixels),
                input.VerticalPixels,
                "Vertical timing total cannot be represented by the timing model.");
        }
    }
}
