using UnitsNet;

namespace BandwidthCalculator.Tests;

public sealed class InterfaceCorrectnessTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0d)]
    [InlineData(-1d)]
    public void Link_modes_reject_non_finite_or_non_positive_payload_bandwidth(double bitsPerSecond)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new LinkModeCapabilities(
                "Invalid",
                LinkTransport.DisplayPortMainLink,
                BitRate.FromBitsPerSecond(bitsPerSecond),
                laneCount: 4));
    }

    [Fact]
    public void Reference_presets_use_published_DisplayPort_payloads_and_explicit_Alt_Mode_lane_counts()
    {
        InterfaceConfiguration presets = InterfaceConfiguration.ReferencePresets;

        Assert.Equal(77.37d, presets.DisplayPort[0].LinkModes.Max(mode => mode.PayloadBandwidth.GigabitsPerSecond), 6);
        Assert.Equal(77.37d, presets.DisplayPortAltModeOnUsbC[0].LinkModes.Max(mode => mode.PayloadBandwidth.GigabitsPerSecond), 6);
        Assert.Equal(38.685d, presets.DisplayPortAltModeOnUsbC[1].LinkModes.Max(mode => mode.PayloadBandwidth.GigabitsPerSecond), 6);
        Assert.All(presets.DisplayPortAltModeOnUsbC[0].LinkModes, mode => Assert.Equal(4, mode.LaneCount));
        Assert.All(presets.DisplayPortAltModeOnUsbC[1].LinkModes, mode => Assert.Equal(2, mode.LaneCount));

        Assert.Equal(["2.1", "1.4", "1.3", "1.2", "1.0-1.1a"], presets.DisplayPort.Select(profile => profile.Profile));
        Assert.Equal(["2.2", "2.1", "2.0", "1.4", "1.3", "1.0-1.2"], presets.Hdmi.Select(profile => profile.Profile));
        Assert.Equal(["2.0", "2.0", "1.0", "1.0"], presets.DisplayPortAltModeOnUsbC.Select(profile => profile.Profile));

        IEnumerable<InterfaceCapabilities> allProfiles = presets.DisplayPort
            .Concat(presets.Hdmi)
            .Concat(presets.DisplayPortAltModeOnUsbC);
        Assert.All(allProfiles, profile => Assert.DoesNotContain("reference profile", profile.Profile, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Reference_HDMI_profiles_include_lower_negotiated_modes_and_documented_HDMI_2_2_rate_names()
    {
        InterfaceConfiguration presets = InterfaceConfiguration.ReferencePresets;
        InterfaceCapabilities hdmi22 = presets.Hdmi[0];
        InterfaceCapabilities hdmi21 = presets.Hdmi[1];

        Assert.Equal(
            ["96 Gbps FRL", "80 Gbps FRL", "64 Gbps FRL", "FRL6", "FRL5", "FRL4", "FRL3", "FRL2", "FRL1", "600MHz TMDS", "340MHz TMDS", "165MHz TMDS"],
            hdmi22.LinkModes.Select(mode => mode.Name));
        Assert.Equal(
            ["FRL6", "FRL5", "FRL4", "FRL3", "FRL2", "FRL1", "600MHz TMDS", "340MHz TMDS", "165MHz TMDS"],
            hdmi21.LinkModes.Select(mode => mode.Name));
        Assert.Equal(96d * 16d / 18d, hdmi22.LinkModes[0].PayloadBandwidth.GigabitsPerSecond, 9);
        Assert.DoesNotContain(hdmi22.LinkModes, mode => mode.Name is "FRL7" or "FRL8" or "FRL9");
    }

    [Fact]
    public void Custom_profile_factories_expand_maximum_rates_without_list_heavy_configuration()
    {
        InterfaceCapabilities displayPort = InterfaceConfiguration.DisplayPortProfile(
            "Dock",
            DisplayPortLinkRate.Hbr2,
            supportsDsc: true,
            laneCount: 2);
        InterfaceCapabilities hdmi = InterfaceConfiguration.HdmiProfile(
            "Receiver",
            HdmiFixedRateLinkRate.Gbps64,
            supportsDsc: true);

        Assert.Equal(["HBR2", "HBR", "RBR"], displayPort.LinkModes.Select(mode => mode.Name));
        Assert.All(displayPort.LinkModes, mode => Assert.Equal(2, mode.LaneCount));
        Assert.True(displayPort.SupportsDsc);
        Assert.Equal("64 Gbps FRL", hdmi.LinkModes[0].Name);
        Assert.Equal("165MHz TMDS", hdmi.LinkModes[^1].Name);
        Assert.True(hdmi.SupportsDsc);
    }

    [Fact]
    public void Low_level_capabilities_reject_cross_transport_coupling()
    {
        LinkModeCapabilities hdmiMode = LinkModeCapabilities.HdmiTmds(HdmiTmdsRate.MHz600);
        PixelFormatCapabilities format = new(ColorEncoding.Rgb, ChromaSubsampling.Cs444, [8]);

        Assert.Throws<ArgumentException>(() =>
            new InterfaceCapabilities(
                InterfaceKind.DisplayPort,
                "Invalid",
                [hdmiMode],
                [format]));
    }

    [Theory]
    [InlineData(LinkTransport.DisplayPortMainLink, false)]
    [InlineData(LinkTransport.HdmiTmds, false)]
    [InlineData(LinkTransport.HdmiFixedRateLink, true)]
    public void Blanking_transport_behavior_is_owned_by_the_protocol(
        LinkTransport transport,
        bool usesActiveVideoBandwidth)
    {
        LinkModeCapabilities mode = new(
            "Custom",
            transport,
            BitRate.FromGigabitsPerSecond(10d),
            laneCount: 4);

        Assert.Equal(usesActiveVideoBandwidth, mode.UsesActiveVideoBandwidth);
    }

    [Fact]
    public void Dsc_bits_per_pixel_are_quantized_down_and_never_exceed_link_capacity()
    {
        CalculationInput input = Input(ColorEncoding.Rgb, ChromaSubsampling.Cs444, colorBitDepth: 10);
        CalculationResult baseline = Calculate(input, new InterfaceConfiguration());
        BitRate capacity = Information.FromBits(29.695d) * baseline.Timing.PixelClock;
        InterfaceCapabilities profile = CustomProfile(
            InterfaceKind.DisplayPort,
            LinkTransport.DisplayPortMainLink,
            capacity,
            input,
            supportsDsc: true);

        InterfaceEvaluation evaluation = Calculate(
            input,
            new InterfaceConfiguration(displayPort: [profile])).DisplayPort.Single();

        Assert.Equal(InterfaceSupport.Compressed, evaluation.Support);
        Assert.Equal(29.6875d, evaluation.DscBitsPerPixel);
        Assert.Equal(30d / 29.6875d, evaluation.DscCompressionRatio);
        Assert.Equal("Boundary", evaluation.SelectedLinkMethod?.Name);
        Assert.Equal(LinkTransport.DisplayPortMainLink, evaluation.SelectedLinkMethod?.Transport);
        Assert.Equal(capacity, evaluation.SelectedLinkMethod?.PayloadBandwidth);
        Assert.Equal(4, evaluation.SelectedLinkMethod?.LaneCount);
        Assert.Equal(InterfaceFailureKind.None, evaluation.FailureKind);
        Assert.Equal(
            new InterfaceProfileSummary(
                InterfaceKind.DisplayPort,
                "Boundary",
                4,
                SupportsDsc: true,
                Supports420: false,
                MaximumBitsPerComponent: 10),
            evaluation.ProfileSummary);
        BitRate selectedBandwidth = Information.FromBits(evaluation.DscBitsPerPixel!.Value) * baseline.Timing.PixelClock;
        Assert.True(selectedBandwidth <= capacity);
    }

    [Theory]
    [InlineData(ChromaSubsampling.Cs444, 8d)]
    [InlineData(ChromaSubsampling.Cs422, 7d)]
    [InlineData(ChromaSubsampling.Cs420, 6d)]
    public void Dsc_accepts_the_exact_quality_floor_and_rejects_the_next_quantum_below_it(
        ChromaSubsampling chromaSubsampling,
        double floorBitsPerPixel)
    {
        CalculationInput input = Input(ColorEncoding.YCbCr, chromaSubsampling, colorBitDepth: 8);
        CalculationResult baseline = Calculate(input, new InterfaceConfiguration());
        InterfaceCapabilities exactProfile = CustomProfile(
            InterfaceKind.DisplayPort,
            LinkTransport.DisplayPortMainLink,
            Information.FromBits(floorBitsPerPixel) * baseline.Timing.PixelClock,
            input,
            supportsDsc: true);
        InterfaceCapabilities belowProfile = CustomProfile(
            InterfaceKind.DisplayPort,
            LinkTransport.DisplayPortMainLink,
            Information.FromBits(floorBitsPerPixel - (1d / 16d)) * baseline.Timing.PixelClock,
            input,
            supportsDsc: true);

        CalculationResult result = Calculate(
            input,
            new InterfaceConfiguration(displayPort: [exactProfile, belowProfile]));

        Assert.Equal(InterfaceSupport.Compressed, result.DisplayPort[0].Support);
        Assert.Equal(floorBitsPerPixel, result.DisplayPort[0].DscBitsPerPixel);
        Assert.Equal(InterfaceSupport.No, result.DisplayPort[1].Support);
        Assert.Null(result.DisplayPort[1].DscBitsPerPixel);
        Assert.Null(result.DisplayPort[1].DscCompressionRatio);
        Assert.Null(result.DisplayPort[1].SelectedLinkMethod);
        Assert.Equal(InterfaceFailureKind.DscQualityFloor, result.DisplayPort[1].FailureKind);
    }

    [Fact]
    public void Hdmi_FRL_evaluates_active_video_payload_instead_of_reapplying_blanking()
    {
        CalculationInput input = Input(ColorEncoding.Rgb, ChromaSubsampling.Cs444, colorBitDepth: 8);
        CalculationResult baseline = Calculate(input, new InterfaceConfiguration());
        BitRate activeVideoCapacity = Information.FromBits(24d)
            * baseline.Timing.FrameFrequency
            * baseline.Timing.HorizontalActive
            * baseline.Timing.VerticalActive;
        InterfaceCapabilities profile = CustomProfile(
            InterfaceKind.Hdmi,
            LinkTransport.HdmiFixedRateLink,
            activeVideoCapacity,
            input,
            supportsDsc: false);

        InterfaceEvaluation evaluation = Calculate(
            input,
            new InterfaceConfiguration(hdmi: [profile])).Hdmi.Single();

        Assert.True(baseline.Bandwidth.Maximum > activeVideoCapacity);
        Assert.Equal(InterfaceSupport.Native, evaluation.Support);
        Assert.Null(evaluation.DscCompressionRatio);
        Assert.Equal("Boundary", evaluation.SelectedLinkMethod?.Name);
        Assert.Equal(LinkTransport.HdmiFixedRateLink, evaluation.SelectedLinkMethod?.Transport);
    }

    [Fact]
    public void Hdmi_422_uses_the_fixed_24_bit_transport_container()
    {
        CalculationInput input = Input(ColorEncoding.YCbCr, ChromaSubsampling.Cs422, colorBitDepth: 8);
        CalculationResult baseline = Calculate(input, new InterfaceConfiguration());
        BitRate capacity = Information.FromBits(20d) * baseline.Timing.PixelClock;
        InterfaceCapabilities profile = CustomProfile(
            InterfaceKind.Hdmi,
            LinkTransport.HdmiTmds,
            capacity,
            input,
            supportsDsc: false);

        InterfaceEvaluation evaluation = Calculate(
            input,
            new InterfaceConfiguration(hdmi: [profile])).Hdmi.Single();

        Assert.True(baseline.Bandwidth.Maximum < capacity);
        Assert.Equal(InterfaceSupport.No, evaluation.Support);
        Assert.Equal(InterfaceFailureKind.Bandwidth, evaluation.FailureKind);
    }

    [Fact]
    public void Unsupported_profiles_report_a_short_semantic_failure_kind_without_losing_the_detailed_reason()
    {
        CalculationInput input = Input(ColorEncoding.Rgb, ChromaSubsampling.Cs444, colorBitDepth: 10);
        InterfaceCapabilities profile = new(
            InterfaceKind.DisplayPort,
            "Format boundary",
            [LinkModeCapabilities.DisplayPort(DisplayPortLinkRate.Hbr3, laneCount: 4)],
            [new PixelFormatCapabilities(ColorEncoding.Rgb, ChromaSubsampling.Cs444, [8])]);

        InterfaceEvaluation evaluation = Calculate(
            input,
            new InterfaceConfiguration(displayPort: [profile])).DisplayPort.Single();

        Assert.Equal(InterfaceSupport.No, evaluation.Support);
        Assert.Equal(InterfaceFailureKind.PixelFormat, evaluation.FailureKind);
        Assert.Contains("Pixel format", evaluation.FailureReason, StringComparison.Ordinal);
        Assert.NotNull(evaluation.ProfileSummary);
    }

    private static CalculationInput Input(
        ColorEncoding encoding,
        ChromaSubsampling chromaSubsampling,
        int colorBitDepth) =>
        new(
            VerticalPixels: 1080,
            HorizontalPixels: 1920,
            RefreshRate: Frequency.FromHertz(60d),
            ColorBitDepth: colorBitDepth,
            ColorEncoding: encoding,
            ChromaSubsampling: chromaSubsampling,
            Timing: TimingStandard.CvtReducedBlankingV2);

    private static InterfaceCapabilities CustomProfile(
        InterfaceKind kind,
        LinkTransport transport,
        BitRate capacity,
        CalculationInput input,
        bool supportsDsc) =>
        new(
            kind,
            "Custom",
            [new LinkModeCapabilities("Boundary", transport, capacity, laneCount: 4)],
            [new PixelFormatCapabilities(input.ColorEncoding, input.ChromaSubsampling, [input.ColorBitDepth])],
            supportsDsc,
            dscInputBitDepths: supportsDsc ? [input.ColorBitDepth] : null);

    private static CalculationResult Calculate(CalculationInput input, InterfaceConfiguration configuration) =>
        global::BandwidthCalculator.BandwidthCalculator.Calculate(input, configuration);
}
