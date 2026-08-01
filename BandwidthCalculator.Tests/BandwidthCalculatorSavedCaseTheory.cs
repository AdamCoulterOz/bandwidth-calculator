using UnitsNet;

namespace BandwidthCalculator.Tests;

public sealed class BandwidthCalculatorSavedCaseTheory
{
    private static readonly IReadOnlyDictionary<string, SavedCase> SavedCasesByName =
        new SavedCase[]
        {
            new("1080P FHD TV", 1920, 1080, 60d, 8, 3.20d, 3.07d, 2.99d, 1.07d),
            new("2K Gaming", 2560, 1440, 165d, 10, 20.37d, 19.76d, 18.25d, 5.43d),
            new("4K TV", 3840, 2160, 120d, 10, 32.27d, 31.62d, 29.86d, 8.61d),
            new("4K Gaming", 3840, 2160, 144d, 10, 39.19d, 38.39d, 35.83d, 10.45d),
            new("6K Display", 6016, 3384, 60d, 10, 38.20d, 37.70d, 36.64d, 10.19d),
            new("8K Display", 7680, 4320, 60d, 10, 62.06d, 61.42d, 59.72d, 16.55d),
            new("Apple Ultra-Wide XDR", 6720, 2880, 120d, 10, 74.64d, 73.76d, 69.67d, 19.90d),
            new("Default", 1512, 982, 144d, 10, 7.24d, 6.87d, 6.41d, 1.93d),
        }.ToDictionary(savedCase => savedCase.Name, StringComparer.Ordinal);

    public static TheoryData<string> SavedCaseNames =>
        [.. SavedCasesByName.Keys];

    [Theory]
    [MemberData(nameof(SavedCaseNames))]
    public void Calculate_preserves_saved_bandwidth_scenarios(string savedCaseName)
    {
        SavedCase savedCase = SavedCasesByName[savedCaseName];
        CalculationResult result = global::BandwidthCalculator.BandwidthCalculator.Calculate(savedCase.CreateInput());

        AssertDisplayedBandwidth(savedCase.MaximumGbps, result.Bandwidth.Maximum);
        AssertDisplayedBandwidth(savedCase.LineGbps, result.Bandwidth.Line);
        AssertDisplayedBandwidth(savedCase.ActiveGbps, result.Bandwidth.Active);
        AssertDisplayedBandwidth(savedCase.MinimumDscGbps, result.Bandwidth.MinimumDsc);

        Assert.Equal(savedCase.HorizontalPixels, result.Timing.HorizontalActive);
        Assert.Equal(savedCase.VerticalPixels, result.Timing.VerticalActive);
        Assert.InRange((double)result.Timing.FrameFrequency.Hertz, savedCase.RefreshRateHz - 0.01d, savedCase.RefreshRateHz + 0.01d);
        Assert.NotEmpty(result.DisplayPort);
        Assert.NotEmpty(result.Hdmi);
        Assert.NotEmpty(result.DisplayPortAltModeOnUsbC);
    }

    private static void AssertDisplayedBandwidth(double expectedGigabitsPerSecond, BitRate actual) =>
        Assert.InRange((double)actual.GigabitsPerSecond, expectedGigabitsPerSecond - 0.005d, expectedGigabitsPerSecond + 0.005d);

    private sealed record SavedCase(
        string Name,
        int HorizontalPixels,
        int VerticalPixels,
        double RefreshRateHz,
        int ColorBitDepth,
        double MaximumGbps,
        double LineGbps,
        double ActiveGbps,
        double MinimumDscGbps)
    {
        public CalculationInput CreateInput() =>
            new(
                VerticalPixels,
                HorizontalPixels,
                Frequency.FromHertz(RefreshRateHz),
                ColorBitDepth,
                ColorEncoding.Rgb,
                ChromaSubsampling.Cs444,
                TimingStandard.CvtReducedBlankingV2);
    }
}
