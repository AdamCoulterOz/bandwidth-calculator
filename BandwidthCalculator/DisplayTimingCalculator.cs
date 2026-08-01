using UnitsNet;

namespace BandwidthCalculator;

/// <summary>
/// Resolves raster timing independently from the bandwidth and interface layers.
/// CVT calculations follow VESA CVT 1.2; database timing identifiers are VESA
/// DMT IDs, CTA-861 VICs, and CTA-861-I Optimized Video Timing respectively.
/// </summary>
internal static class DisplayTimingCalculator
{
    private const double VideoRateMultiplier = 1000d / 1001d;
    private const double RateMatchToleranceHz = 0.001d;

    public static TimingMetrics Resolve(CalculationInput input) =>
        input.Timing switch
        {
            TimingStandard.Cvt => CalculateCvt(input, CvtRevision.Standard),
            TimingStandard.CvtReducedBlanking => CalculateCvt(input, CvtRevision.ReducedBlankingV1),
            TimingStandard.CvtReducedBlankingV2 => CalculateCvt(input, CvtRevision.ReducedBlankingV2),
            TimingStandard.CvtReducedBlankingV3 => CalculateCvt(input, CvtRevision.ReducedBlankingV3),
            TimingStandard.Dmt => ResolveDmt(input),
            TimingStandard.Cta861 => ResolveCta(input),
            TimingStandard.Cta861OptimizedVideoTiming => ResolveCtaOptimizedVideoTiming(input),
            _ => throw new ArgumentOutOfRangeException(nameof(input.Timing)),
        };

    private static TimingMetrics CalculateCvt(CalculationInput input, CvtRevision revision)
    {
        ValidateBaseInput(input);
        if (input.StandardTimingId is not null)
            throw new ArgumentException("StandardTimingId is only valid for DMT and CTA-861 timings.", nameof(input));
        if (revision != CvtRevision.ReducedBlankingV3 && input.CvtReducedBlankingV3 is not null)
            throw new ArgumentException("CVT reduced blanking v3 options require the CVT-RBv3 timing standard.", nameof(input));

        bool interlaced = input.ScanMode == ScanMode.Interlaced;
        int cellGranularity = revision == CvtRevision.ReducedBlankingV2 ? 1 : 8;
        int horizontalActive = input.HorizontalPixels / cellGranularity * cellGranularity;
        int verticalActivePerField = interlaced ? input.VerticalPixels / 2 : input.VerticalPixels;
        double interlace = interlaced ? 0.5d : 0d;
        double requestedFrameRateHz = input.RefreshRate.Hertz;
        (double blankingFrameRateHz, double refreshMultiplier) =
            ResolveCvtRefreshRate(input.RefreshRateConvention, requestedFrameRateHz, revision);
        double fieldRateHz = blankingFrameRateHz * (interlaced ? 2d : 1d);

        int horizontalBlank;
        double verticalBlankPerField;
        double pixelClockMhz;

        if (revision == CvtRevision.Standard)
        {
            const double minimumVsyncBackPorchMicroseconds = 550d;
            const int minimumVerticalFrontPorchLines = 3;
            const int minimumVerticalBackPorchLines = 7;
            const double cPrime = 30d;
            const double mPrime = 300d;

            double horizontalPeriodMicroseconds =
                ((1d / fieldRateHz) - (minimumVsyncBackPorchMicroseconds / 1_000_000d)) /
                (verticalActivePerField + minimumVerticalFrontPorchLines + interlace) * 1_000_000d;
            EnsurePositiveFinite(horizontalPeriodMicroseconds, "CVT horizontal period");

            int verticalSyncLines = ResolveCvtVerticalSyncLines(horizontalActive, input.VerticalPixels);
            double verticalSyncBackPorchLines = Math.Floor(minimumVsyncBackPorchMicroseconds / horizontalPeriodMicroseconds) + 1d;
            verticalSyncBackPorchLines = Math.Max(
                verticalSyncBackPorchLines,
                verticalSyncLines + minimumVerticalBackPorchLines);
            verticalBlankPerField = verticalSyncBackPorchLines + minimumVerticalFrontPorchLines;

            double idealDutyCycle = Math.Max(20d, cPrime - (mPrime * horizontalPeriodMicroseconds / 1000d));
            horizontalBlank = checked((int)(
                Math.Floor(horizontalActive * idealDutyCycle / (100d - idealDutyCycle) / 16d) * 16d));
            int horizontalTotal = checked(horizontalActive + horizontalBlank);
            pixelClockMhz = Math.Floor((horizontalTotal / horizontalPeriodMicroseconds) / 0.25d) * 0.25d;
        }
        else
        {
            const double defaultMinimumVerticalBlankMicroseconds = 460d;
            const int fixedVerticalBackPorchLines = 6;

            CvtReducedBlankingV3Options? options = input.CvtReducedBlankingV3;

            horizontalBlank = revision switch
            {
                CvtRevision.ReducedBlankingV1 => 160,
                CvtRevision.ReducedBlankingV2 => 80,
                CvtRevision.ReducedBlankingV3 => ResolveRbV3HorizontalBlank(options),
                _ => throw new InvalidOperationException(),
            };

            double minimumVerticalBlankMicroseconds = revision == CvtRevision.ReducedBlankingV3
                ? ResolveRbV3MinimumVerticalBlank(options)
                : defaultMinimumVerticalBlankMicroseconds;
            int verticalFrontPorchLines = revision == CvtRevision.ReducedBlankingV1 ? 3 : 1;
            int verticalSyncLines = revision == CvtRevision.ReducedBlankingV1
                ? ResolveCvtVerticalSyncLines(horizontalActive, input.VerticalPixels)
                : 8;
            int verticalBackPorchLines = revision == CvtRevision.ReducedBlankingV1
                ? 7
                : fixedVerticalBackPorchLines;

            double horizontalPeriodMicroseconds =
                ((1_000_000d / fieldRateHz) - minimumVerticalBlankMicroseconds) / verticalActivePerField;
            EnsurePositiveFinite(horizontalPeriodMicroseconds, "CVT reduced-blanking horizontal period");

            double minimumVerticalBlankLines = Math.Floor(minimumVerticalBlankMicroseconds / horizontalPeriodMicroseconds) + 1d;
            verticalBlankPerField = Math.Max(
                minimumVerticalBlankLines,
                verticalFrontPorchLines + verticalSyncLines + verticalBackPorchLines);

            // Early VSync changes porch placement, not the total blanking interval. Validate
            // the option here even though the bandwidth-facing timing metrics do not expose porches.
            if (revision == CvtRevision.ReducedBlankingV3 && options?.EarlyVsync == true)
            {
                double earlyBackPorch = Math.Floor(minimumVerticalBlankLines / 2d);
                _ = Math.Min(earlyBackPorch, verticalBlankPerField - verticalSyncLines - verticalFrontPorchLines);
            }

            double totalVerticalLinesPerField = verticalActivePerField + verticalBlankPerField + interlace;
            int horizontalTotal = checked(horizontalActive + horizontalBlank);
            double unroundedPixelClockMhz =
                fieldRateHz * totalVerticalLinesPerField * horizontalTotal * refreshMultiplier / 1_000_000d;
            pixelClockMhz = revision == CvtRevision.ReducedBlankingV3
                ? Math.Ceiling(unroundedPixelClockMhz / 0.001d) * 0.001d
                : Math.Floor(unroundedPixelClockMhz / (revision == CvtRevision.ReducedBlankingV1 ? 0.25d : 0.001d)) *
                  (revision == CvtRevision.ReducedBlankingV1 ? 0.25d : 0.001d);
        }

        double verticalTotalPerField = verticalActivePerField + verticalBlankPerField + interlace;
        int verticalTotal = interlaced
            ? checked((int)Math.Round(verticalTotalPerField * 2d, MidpointRounding.AwayFromZero))
            : checked((int)verticalTotalPerField);

        return BuildMetrics(
            input.Timing,
            standardTimingId: null,
            input.ScanMode,
            horizontalActive,
            input.VerticalPixels,
            horizontalActive + horizontalBlank,
            verticalTotal,
            Frequency.FromMegahertz(pixelClockMhz));
    }

    private static TimingMetrics ResolveDmt(CalculationInput input)
    {
        ValidateBaseInput(input);
        if (input.CvtReducedBlankingV3 is not null)
            throw new ArgumentException("CVT reduced blanking v3 options are not valid for DMT timings.", nameof(input));

        IEnumerable<(int Id, DmtTiming Timing)> candidates = BandwidthTimingDatabase.DmtTimings
            .Select((timing, index) => (BandwidthTimingDatabase.DmtTimingIds[index], timing));

        if (input.StandardTimingId is int requestedId)
        {
            candidates = candidates.Where(candidate => candidate.Id == requestedId);
            if (!candidates.Any())
                throw new ArgumentException($"DMT timing ID 0x{requestedId:X2} is not in the timing database.", nameof(input));
        }

        candidates = candidates.Where(candidate =>
            candidate.Timing.HorizontalActive == input.HorizontalPixels &&
            candidate.Timing.VerticalActive == input.VerticalPixels &&
            candidate.Timing.Interlaced == (input.ScanMode == ScanMode.Interlaced) &&
            DmtRateMatches(input, candidate));

        (int Id, DmtTiming Timing)[] matches = candidates.ToArray();
        if (matches.Length == 0)
            throw new ArgumentException("No DMT mode exactly matches the requested identifier, dimensions, scan mode, and refresh rate.", nameof(input));
        if (matches.Length > 1)
            throw new ArgumentException("The requested DMT mode is ambiguous; specify StandardTimingId.", nameof(input));

        (int id, DmtTiming timing) = matches[0];
        TimingMetrics metrics = timing.Kind switch
        {
            DmtTimingKind.Direct => BuildMetrics(
                TimingStandard.Dmt,
                id,
                timing.Interlaced ? ScanMode.Interlaced : ScanMode.Progressive,
                timing.HorizontalActive,
                timing.VerticalActive,
                timing.HorizontalTotal,
                timing.VerticalTotal,
                timing.PixelClock),
            DmtTimingKind.Cvt => CalculateDmtCvt(input, id, timing, TimingStandard.Cvt, RefreshRateConvention.Exact),
            DmtTimingKind.CvtReducedBlanking => CalculateDmtCvt(input, id, timing, TimingStandard.CvtReducedBlanking, RefreshRateConvention.Exact),
            DmtTimingKind.CvtReducedBlankingV2 => CalculateDmtCvt(
                input,
                id,
                timing,
                TimingStandard.CvtReducedBlankingV2,
                id == 0x58 ? RefreshRateConvention.VideoOptimized : RefreshRateConvention.Exact,
                id == 0x58 ? 60d : timing.VerticalFrequency.Hertz),
            DmtTimingKind.Cta861 => ResolveDmtCta(input, id, timing),
            _ => throw new ArgumentOutOfRangeException(nameof(timing.Kind)),
        };

        ValidateStandardTiming(metrics);
        return metrics;
    }

    private static TimingMetrics CalculateDmtCvt(
        CalculationInput input,
        int id,
        DmtTiming timing,
        TimingStandard standard,
        RefreshRateConvention convention,
        double? refreshRateHz = null)
    {
        CalculationInput cvtInput = input with
        {
            Timing = standard,
            RefreshRate = Frequency.FromHertz(refreshRateHz ?? timing.VerticalFrequency.Hertz),
            RefreshRateConvention = convention,
            StandardTimingId = null,
            CvtReducedBlankingV3 = null,
        };
        return CalculateCvt(
            cvtInput,
            standard switch
            {
                TimingStandard.Cvt => CvtRevision.Standard,
                TimingStandard.CvtReducedBlanking => CvtRevision.ReducedBlankingV1,
                TimingStandard.CvtReducedBlankingV2 => CvtRevision.ReducedBlankingV2,
                _ => throw new InvalidOperationException(),
            }) with
        {
            Standard = TimingStandard.Dmt,
            StandardTimingId = id,
        };
    }

    private static TimingMetrics ResolveDmtCta(CalculationInput input, int id, DmtTiming timing)
    {
        int vic = id switch
        {
            0x52 => 16,
            0x55 => 4,
            _ => throw new InvalidOperationException($"DMT ID 0x{id:X2} has no CTA-861 mapping."),
        };
        CalculationInput ctaInput = input with
        {
            Timing = TimingStandard.Cta861,
            RefreshRate = timing.VerticalFrequency,
            RefreshRateConvention = RefreshRateConvention.Exact,
            StandardTimingId = vic,
        };
        return ResolveCta(ctaInput) with
        {
            Standard = TimingStandard.Dmt,
            StandardTimingId = id,
        };
    }

    private static TimingMetrics ResolveCta(CalculationInput input)
    {
        ValidateBaseInput(input);
        if (input.CvtReducedBlankingV3 is not null)
            throw new ArgumentException("CVT reduced blanking v3 options are not valid for CTA-861 timings.", nameof(input));

        IEnumerable<CtaCandidate> candidates = BandwidthTimingDatabase.CeaTimings
            .Select((timing, index) => new CtaCandidate(BandwidthTimingDatabase.GetCtaTimingId(index), timing, timing.PixelClock));

        if (input.StandardTimingId is int requestedVic)
        {
            candidates = candidates.Where(candidate => candidate.Id == requestedVic);
            if (!candidates.Any())
                throw new ArgumentException($"CTA-861 VIC {requestedVic} is not in the timing database.", nameof(input));
        }

        candidates = candidates.Where(candidate =>
            candidate.Timing.HorizontalActive == input.HorizontalPixels &&
            candidate.Timing.VerticalActive == input.VerticalPixels &&
            candidate.Timing.Interlaced == (input.ScanMode == ScanMode.Interlaced));

        double targetRateHz = input.RefreshRateConvention == RefreshRateConvention.VideoOptimized
            ? input.RefreshRate.Hertz * VideoRateMultiplier
            : input.RefreshRate.Hertz;

        List<CtaCandidate> rateMatches = [];
        foreach (CtaCandidate candidate in candidates)
        {
            AddCtaVariantIfMatching(rateMatches, candidate, candidate.PixelClock, targetRateHz, input.RefreshRateConvention);

            double baseRateHz = GetCtaSignalRateHz(candidate.Timing, candidate.PixelClock);
            if (TryGetPairedCtaPixelClock(candidate.PixelClock, baseRateHz, out Frequency pairedPixelClock))
                AddCtaVariantIfMatching(rateMatches, candidate, pairedPixelClock, targetRateHz, input.RefreshRateConvention);
        }

        if (rateMatches.Count == 0)
            throw new ArgumentException("No CTA-861 mode exactly matches the requested identifier, dimensions, scan mode, and refresh rate.", nameof(input));

        CtaCandidate selected = rateMatches[0];
        int? selectedId = selected.Id;
        if (input.StandardTimingId is null && rateMatches.Count > 1)
        {
            bool allEquivalent = rateMatches.All(candidate =>
                candidate.Timing.HorizontalTotal == selected.Timing.HorizontalTotal &&
                candidate.Timing.VerticalTotal == selected.Timing.VerticalTotal &&
                NearlyEqual(candidate.PixelClock.Hertz, selected.PixelClock.Hertz, 0.5d));
            if (!allEquivalent)
                throw new ArgumentException("The requested CTA-861 mode is ambiguous; specify StandardTimingId (VIC).", nameof(input));

            // Identical CTA timings can have separate VICs solely for picture aspect ratio,
            // which is outside this calculator's timing model.
            selectedId = null;
        }

        TimingMetrics metrics = BuildMetrics(
            TimingStandard.Cta861,
            selectedId,
            selected.Timing.Interlaced ? ScanMode.Interlaced : ScanMode.Progressive,
            selected.Timing.HorizontalActive,
            selected.Timing.VerticalActive,
            selected.Timing.HorizontalTotal,
            selected.Timing.VerticalTotal,
            selected.PixelClock);
        ValidateStandardTiming(metrics);
        return metrics;
    }

    private static TimingMetrics ResolveCtaOptimizedVideoTiming(CalculationInput input)
    {
        ValidateBaseInput(input);
        if (input.ScanMode != ScanMode.Progressive)
            throw new ArgumentException("CTA-861 Optimized Video Timing only supports progressive scan.", nameof(input));
        if (input.StandardTimingId is not null)
            throw new ArgumentException("CTA-861 Optimized Video Timing does not use a VIC.", nameof(input));
        if (input.CvtReducedBlankingV3 is not null)
            throw new ArgumentException("CVT reduced blanking v3 options are not valid for CTA-861 Optimized Video Timing.", nameof(input));
        if (input.HorizontalPixels % 8 != 0)
            throw new ArgumentException("CTA-861 Optimized Video Timing requires horizontal active pixels to be a multiple of 8.", nameof(input));

        try
        {
            return CalculateCtaOptimizedVideoTiming(input);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                input,
                "The requested CTA-861 Optimized Video Timing cannot be represented by the timing model.");
        }
    }

    private static TimingMetrics CalculateCtaOptimizedVideoTiming(CalculationInput input)
    {
        (int nominalRate, bool useFractionalRate) = ResolveOvtRefreshRate(input);
        (int maximumGroupRate, int verticalTotalGranularity) = nominalRate switch
        {
            24 or 25 or 30 => (30, 20),
            48 or 50 or 60 => (60, 20),
            100 or 120 => (120, 5),
            200 or 240 => (240, 5),
            300 or 360 => (360, 5),
            400 or 480 => (480, 5),
            _ => (nominalRate, 1),
        };

        const int minimumVerticalBlankMicroseconds = 460;
        const int minimumVerticalBlankLines = 20;
        const int minimum420PixelClockMhz = 590;
        const int minimumHorizontalBlank444 = 80;
        const int minimumHorizontalBlank420 = 128;
        const int pixelClockGranularityHz = 1000;
        const int minimumHorizontalGranularity = 8;
        const int audioPacketRateKhz = 195;
        const int audioPacketSize = 32;
        const int lineOverhead = 32;
        const int pixelsPer420Character = 2;
        const int maximumChunkRateMhz = 650;

        double maximumActiveTimeMicroseconds =
            1_000_000d / maximumGroupRate - minimumVerticalBlankMicroseconds;
        EnsurePositiveFinite(maximumActiveTimeMicroseconds, "CTA-861 OVT maximum active time");
        double minimumLineTimeMicroseconds = maximumActiveTimeMicroseconds / input.VerticalPixels;
        int minimumVerticalBlank = Math.Max(
            minimumVerticalBlankLines,
            CheckedCeiling(minimumVerticalBlankMicroseconds / minimumLineTimeMicroseconds, nameof(input.VerticalPixels)));
        int minimumVerticalTotal = RoundUpToMultiple(
            checked(input.VerticalPixels + minimumVerticalBlank),
            verticalTotalGranularity,
            nameof(input.VerticalPixels));

        long minimumLineRate = checked((long)maximumGroupRate * minimumVerticalTotal);
        int maximumAudioPacketsPerLine = CheckedCeiling(
            audioPacketRateKhz * 1000d / minimumLineRate,
            nameof(input.RefreshRate));
        int requiredHorizontalBlank = checked(lineOverhead + audioPacketSize * maximumAudioPacketsPerLine);
        int minimumHorizontalTotal = checked(input.HorizontalPixels + Math.Max(minimumHorizontalBlank444, requiredHorizontalBlank));

        long minimumPixelClock = checked((long)maximumGroupRate * minimumHorizontalTotal * minimumVerticalTotal);
        int horizontalChunkGranularity = SmallestPowerOfTwoAtLeast(
            minimumPixelClock / (maximumChunkRateMhz * 1_000_000d),
            nameof(input.HorizontalPixels));
        int horizontalTotalGranularity = Math.Max(minimumHorizontalGranularity, horizontalChunkGranularity);
        minimumHorizontalTotal = RoundUpToMultiple(
            minimumHorizontalTotal,
            horizontalTotalGranularity,
            nameof(input.HorizontalPixels));

        OvtTotals totals;
        while (true)
        {
            totals = FindOvtTotals(
                minimumHorizontalTotal,
                minimumVerticalTotal,
                maximumGroupRate,
                pixelClockGranularityHz,
                horizontalTotalGranularity,
                verticalTotalGranularity,
                maximumChunkRateMhz * 1_000_000L);

            int requiredHorizontalBlank420 = checked(
                pixelsPer420Character * (lineOverhead + audioPacketSize * maximumAudioPacketsPerLine));
            int minimumHorizontalTotal420 = checked(
                input.HorizontalPixels + Math.Max(minimumHorizontalBlank420, requiredHorizontalBlank420));
            minimumHorizontalTotal420 = RoundUpToMultiple(
                minimumHorizontalTotal420,
                horizontalTotalGranularity,
                nameof(input.HorizontalPixels));

            if (totals.PixelClockHz >= minimum420PixelClockMhz * 1_000_000L &&
                totals.HorizontalTotal < minimumHorizontalTotal420)
            {
                minimumHorizontalTotal = minimumHorizontalTotal420;
                continue;
            }

            break;
        }

        long adjustedVerticalTotal = checked((long)totals.VerticalTotal * maximumGroupRate / nominalRate);
        double pixelClockHz = useFractionalRate
            ? totals.PixelClockHz / 1.001d
            : totals.PixelClockHz;
        TimingMetrics metrics = BuildMetrics(
            TimingStandard.Cta861OptimizedVideoTiming,
            null,
            ScanMode.Progressive,
            input.HorizontalPixels,
            input.VerticalPixels,
            totals.HorizontalTotal,
            checked((int)adjustedVerticalTotal),
            Frequency.FromHertz(pixelClockHz));
        ValidateStandardTiming(metrics);
        return metrics;
    }

    private static (int NominalRate, bool UseFractionalRate) ResolveOvtRefreshRate(CalculationInput input)
    {
        double requestedRate = input.RefreshRate.Hertz;
        bool useFractionalRate = input.RefreshRateConvention == RefreshRateConvention.VideoOptimized;
        double nominalRate = requestedRate;

        if (input.RefreshRateConvention == RefreshRateConvention.Automatic &&
            TryInferAnyIntegerVideoNominal(requestedRate, out double inferredRate))
        {
            nominalRate = inferredRate;
            useFractionalRate = true;
        }

        if (nominalRate != Math.Truncate(nominalRate) || nominalRate > int.MaxValue)
            throw new ArgumentException("CTA-861 Optimized Video Timing requires an integer nominal refresh rate.", nameof(input));

        return (checked((int)nominalRate), useFractionalRate);
    }

    private static OvtTotals FindOvtTotals(
        int minimumHorizontalTotal,
        int minimumVerticalTotal,
        int verticalRate,
        int pixelClockGranularityHz,
        int horizontalTotalGranularity,
        int verticalTotalGranularity,
        long maximumChunkRateHz)
    {
        int resolutionGranularity = pixelClockGranularityHz / GreatestCommonDivisor(pixelClockGranularityHz, verticalRate);
        long minimumRasterSize = long.MaxValue;
        int selectedHorizontalTotal = 0;
        int selectedVerticalTotal = 0;

        for (long verticalTotal = minimumVerticalTotal; ; verticalTotal += verticalTotalGranularity)
        {
            long initialRasterSize = checked((long)minimumHorizontalTotal * verticalTotal);
            if (minimumRasterSize != long.MaxValue && initialRasterSize > minimumRasterSize)
                break;

            for (long horizontalTotal = minimumHorizontalTotal; ; horizontalTotal += horizontalTotalGranularity)
            {
                long rasterSize = checked(horizontalTotal * verticalTotal);
                long chunkDivisor = GreatestPowerOfTwoDivisor(horizontalTotal);
                bool isClockGranular = rasterSize % resolutionGranularity == 0;
                bool isWithinChunkRate = verticalRate * (double)rasterSize / chunkDivisor <= maximumChunkRateHz;
                if (isClockGranular && isWithinChunkRate)
                {
                    if (rasterSize < minimumRasterSize)
                    {
                        selectedHorizontalTotal = checked((int)horizontalTotal);
                        selectedVerticalTotal = checked((int)verticalTotal);
                        minimumRasterSize = rasterSize;
                    }

                    break;
                }

                if (horizontalTotal > int.MaxValue - horizontalTotalGranularity)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(minimumHorizontalTotal),
                        "CTA-861 OVT horizontal total cannot be represented by the timing model.");
                }
            }

            if (verticalTotal > int.MaxValue - verticalTotalGranularity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumVerticalTotal),
                    "CTA-861 OVT vertical total cannot be represented by the timing model.");
            }
        }

        long pixelClockHz = checked((long)verticalRate * minimumRasterSize);
        return new OvtTotals(selectedHorizontalTotal, selectedVerticalTotal, pixelClockHz);
    }

    private static bool TryInferAnyIntegerVideoNominal(double requestedRateHz, out double nominalRateHz)
    {
        nominalRateHz = Math.Round(requestedRateHz / VideoRateMultiplier);
        return nominalRateHz > 0d &&
               nominalRateHz == Math.Truncate(nominalRateHz) &&
               NearlyEqual(requestedRateHz, nominalRateHz * VideoRateMultiplier, RateMatchToleranceHz);
    }

    private static int CheckedCeiling(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value > int.MaxValue)
            throw new ArgumentOutOfRangeException(parameterName, "CTA-861 OVT timing total cannot be represented by the timing model.");
        return checked((int)Math.Ceiling(value));
    }

    private static int RoundUpToMultiple(int value, int multiple, string parameterName)
    {
        long rounded = ((long)value + multiple - 1) / multiple * multiple;
        if (rounded > int.MaxValue)
            throw new ArgumentOutOfRangeException(parameterName, "CTA-861 OVT timing total cannot be represented by the timing model.");
        return (int)rounded;
    }

    private static int SmallestPowerOfTwoAtLeast(double value, string parameterName)
    {
        int ceiling = CheckedCeiling(value, parameterName);
        int result = 1;
        while (result < ceiling)
        {
            if (result > int.MaxValue / 2)
                throw new ArgumentOutOfRangeException(parameterName, "CTA-861 OVT chunk granularity cannot be represented.");
            result *= 2;
        }

        return result;
    }

    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0)
            (left, right) = (right, left % right);
        return left;
    }

    private static long GreatestPowerOfTwoDivisor(long value) => value & -value;

    private static void AddCtaVariantIfMatching(
        ICollection<CtaCandidate> matches,
        CtaCandidate candidate,
        Frequency pixelClock,
        double targetRateHz,
        RefreshRateConvention convention)
    {
        double signalRateHz = GetCtaSignalRateHz(candidate.Timing, pixelClock);
        if (!NearlyEqual(signalRateHz, targetRateHz, RateMatchToleranceHz))
            return;

        if (convention == RefreshRateConvention.VideoOptimized &&
            !IsVideoOptimizedRate(signalRateHz, targetRateHz / VideoRateMultiplier))
            return;

        if (!matches.Any(existing =>
                existing.Id == candidate.Id &&
                NearlyEqual(existing.PixelClock.Hertz, pixelClock.Hertz, 0.5d)))
            matches.Add(candidate with { PixelClock = pixelClock });
    }

    private static bool TryGetPairedCtaPixelClock(
        Frequency basePixelClock,
        double baseRateHz,
        out Frequency pairedPixelClock)
    {
        double integerNominal = Math.Round(baseRateHz);
        if (IsVideoNominal(integerNominal) && NearlyEqual(baseRateHz, integerNominal, RateMatchToleranceHz))
        {
            pairedPixelClock = basePixelClock * VideoRateMultiplier;
            return true;
        }

        double fractionalNominal = Math.Round(baseRateHz / VideoRateMultiplier);
        if (IsVideoNominal(fractionalNominal) &&
            NearlyEqual(baseRateHz, fractionalNominal * VideoRateMultiplier, RateMatchToleranceHz))
        {
            pairedPixelClock = Frequency.FromHertz(
                basePixelClock.Hertz / baseRateHz * fractionalNominal);
            return true;
        }

        pairedPixelClock = default;
        return false;
    }

    private static bool DmtRateMatches(CalculationInput input, (int Id, DmtTiming Timing) candidate)
    {
        double expectedHz = input.RefreshRateConvention == RefreshRateConvention.VideoOptimized
            ? input.RefreshRate.Hertz * VideoRateMultiplier
            : input.RefreshRate.Hertz;

        if (candidate.Id == 0x58)
            return NearlyEqual(expectedHz, 60d * VideoRateMultiplier, RateMatchToleranceHz) ||
                   NearlyEqual(input.RefreshRate.Hertz, candidate.Timing.VerticalFrequency.Hertz, RateMatchToleranceHz);

        return NearlyEqual(expectedHz, candidate.Timing.VerticalFrequency.Hertz, RateMatchToleranceHz);
    }

    private static (double BlankingFrameRateHz, double RefreshMultiplier) ResolveCvtRefreshRate(
        RefreshRateConvention convention,
        double requestedFrameRateHz,
        CvtRevision revision)
    {
        if (revision == CvtRevision.ReducedBlankingV2)
        {
            if (convention == RefreshRateConvention.VideoOptimized)
                return (requestedFrameRateHz, VideoRateMultiplier);

            if (convention == RefreshRateConvention.Automatic &&
                TryInferVideoNominal(requestedFrameRateHz, out double nominalRateHz))
                return (nominalRateHz, VideoRateMultiplier);
        }

        return convention == RefreshRateConvention.VideoOptimized
            ? (requestedFrameRateHz * VideoRateMultiplier, 1d)
            : (requestedFrameRateHz, 1d);
    }

    private static bool TryInferVideoNominal(double requestedRateHz, out double nominalRateHz)
    {
        nominalRateHz = Math.Round(requestedRateHz / VideoRateMultiplier);
        return IsVideoNominal(nominalRateHz) &&
               NearlyEqual(requestedRateHz, nominalRateHz * VideoRateMultiplier, RateMatchToleranceHz);
    }

    private static bool IsVideoOptimizedRate(double actualRateHz, double nominalRateHz) =>
        IsVideoNominal(nominalRateHz) &&
        NearlyEqual(actualRateHz, nominalRateHz * VideoRateMultiplier, RateMatchToleranceHz);

    private static bool IsVideoNominal(double rateHz) =>
        rateHz >= 24d && NearlyEqual(rateHz / 6d, Math.Round(rateHz / 6d), 1e-9d);

    private static int ResolveRbV3HorizontalBlank(CvtReducedBlankingV3Options? options)
    {
        int horizontalBlank = options?.HorizontalBlankPixels ?? 80;
        if (horizontalBlank is < 80 or > 200 || horizontalBlank % 8 != 0)
            throw new ArgumentOutOfRangeException(
                nameof(CvtReducedBlankingV3Options.HorizontalBlankPixels),
                "CVT-RBv3 horizontal blanking must be from 80 through 200 pixels in 8-pixel increments.");
        return horizontalBlank;
    }

    private static double ResolveRbV3MinimumVerticalBlank(CvtReducedBlankingV3Options? options)
    {
        int minimumVerticalBlank = options?.MinimumVerticalBlankMicroseconds ?? 460;
        if (minimumVerticalBlank is < 300 or > 920)
            throw new ArgumentOutOfRangeException(
                nameof(CvtReducedBlankingV3Options.MinimumVerticalBlankMicroseconds),
                "CVT-RBv3 minimum vertical blanking must be from 300 through 920 microseconds.");

        // CVT-RBv3 encodes its short range through 440 us, then resumes at the
        // legacy-compatible 460 us minimum. Values in the reserved gap resolve
        // to the next valid minimum, as required by the standard algorithm.
        return minimumVerticalBlank is > 440 and < 460 ? 460 : minimumVerticalBlank;
    }

    private static int ResolveCvtVerticalSyncLines(int horizontalActive, int verticalActive)
    {
        if (HasExactAspectRatio(horizontalActive, verticalActive, 4, 3))
            return 4;
        if (HasExactAspectRatio(horizontalActive, verticalActive, 16, 9))
            return 5;
        if (HasExactAspectRatio(horizontalActive, verticalActive, 16, 10))
            return 6;
        if (HasExactAspectRatio(horizontalActive, verticalActive, 5, 4))
            return 7;
        if (HasExactAspectRatio(horizontalActive, verticalActive, 15, 9))
            return 7;
        return 10;
    }

    private static bool HasExactAspectRatio(
        int horizontalActive,
        int verticalActive,
        int horizontalRatio,
        int verticalRatio) =>
        (long)horizontalActive * verticalRatio == (long)verticalActive * horizontalRatio;

    private static TimingMetrics BuildMetrics(
        TimingStandard standard,
        int? standardTimingId,
        ScanMode scanMode,
        int horizontalActive,
        int verticalActive,
        int horizontalTotal,
        int verticalTotal,
        Frequency pixelClock)
    {
        if (horizontalTotal <= horizontalActive)
            throw new InvalidOperationException("Resolved horizontal total must exceed horizontal active pixels.");
        if (verticalTotal <= verticalActive)
            throw new InvalidOperationException("Resolved vertical total must exceed vertical active lines.");
        if (!double.IsFinite(pixelClock.Hertz) || pixelClock.Hertz <= 0d)
            throw new InvalidOperationException("Resolved pixel clock must be positive and finite.");

        Frequency horizontalFrequency = pixelClock / horizontalTotal;
        Frequency frameFrequency = horizontalFrequency / verticalTotal;
        Frequency fieldFrequency = scanMode == ScanMode.Interlaced ? frameFrequency * 2d : frameFrequency;

        return new TimingMetrics(
            standard,
            standardTimingId,
            scanMode,
            horizontalActive,
            horizontalTotal - horizontalActive,
            horizontalTotal,
            verticalActive,
            verticalTotal - verticalActive,
            verticalTotal,
            pixelClock,
            horizontalFrequency,
            frameFrequency,
            fieldFrequency);
    }

    private static void ValidateBaseInput(CalculationInput input)
    {
        if (input.HorizontalPixels <= 0)
            throw new ArgumentOutOfRangeException(nameof(input.HorizontalPixels));
        if (input.VerticalPixels <= 0)
            throw new ArgumentOutOfRangeException(nameof(input.VerticalPixels));
        if (!double.IsFinite(input.RefreshRate.Hertz) || input.RefreshRate.Hertz <= 0d)
            throw new ArgumentOutOfRangeException(nameof(input.RefreshRate));
        if (!Enum.IsDefined(input.ScanMode))
            throw new ArgumentOutOfRangeException(nameof(input.ScanMode));
        if (!Enum.IsDefined(input.RefreshRateConvention))
            throw new ArgumentOutOfRangeException(nameof(input.RefreshRateConvention));
        if (input.ScanMode == ScanMode.Interlaced && input.VerticalPixels % 2 != 0)
            throw new ArgumentException("Interlaced timings require an even number of active vertical lines.", nameof(input));
    }

    private static void ValidateStandardTiming(TimingMetrics timing)
    {
        double expectedHorizontalHz = timing.PixelClock.Hertz / timing.HorizontalTotal;
        double expectedFrameHz = expectedHorizontalHz / timing.VerticalTotal;
        double expectedFieldHz = expectedFrameHz * (timing.ScanMode == ScanMode.Interlaced ? 2d : 1d);

        if (!NearlyEqual(timing.HorizontalFrequency.Hertz, expectedHorizontalHz, 1e-6d) ||
            !NearlyEqual(timing.FrameFrequency.Hertz, expectedFrameHz, 1e-9d) ||
            !NearlyEqual(timing.FieldFrequency.Hertz, expectedFieldHz, 1e-9d))
            throw new InvalidOperationException("Resolved timing periods, totals, and clock are inconsistent.");
    }

    private static double GetCtaSignalRateHz(CeaTiming timing, Frequency pixelClock)
    {
        double frameRateHz = pixelClock.Hertz / timing.HorizontalTotal / timing.VerticalTotal;
        return timing.Interlaced ? frameRateHz * 2d : frameRateHz;
    }

    private static bool NearlyEqual(double left, double right, double tolerance) =>
        Math.Abs(left - right) <= tolerance;

    private static void EnsurePositiveFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0d)
            throw new ArgumentOutOfRangeException(name, "The requested mode cannot produce a positive finite timing period.");
    }

    private readonly record struct CtaCandidate(int Id, CeaTiming Timing, Frequency PixelClock);

    private readonly record struct OvtTotals(int HorizontalTotal, int VerticalTotal, long PixelClockHz);

    private enum CvtRevision
    {
        Standard,
        ReducedBlankingV1,
        ReducedBlankingV2,
        ReducedBlankingV3,
    }
}
