namespace BandwidthCalculator.Web.Models;

public static class DisplayText
{
    public static string TimingStandard(TimingStandard standard) => standard switch
    {
        global::BandwidthCalculator.TimingStandard.Cvt => "CVT",
        global::BandwidthCalculator.TimingStandard.CvtReducedBlanking => "CVT Reduced Blanking v1",
        global::BandwidthCalculator.TimingStandard.CvtReducedBlankingV2 => "CVT Reduced Blanking v2",
        global::BandwidthCalculator.TimingStandard.CvtReducedBlankingV3 => "CVT Reduced Blanking v3",
        global::BandwidthCalculator.TimingStandard.Dmt => "VESA DMT",
        global::BandwidthCalculator.TimingStandard.Cta861 => "CTA-861 VIC",
        global::BandwidthCalculator.TimingStandard.Cta861OptimizedVideoTiming => "CTA-861 Optimized Video Timing",
        _ => standard.ToString(),
    };

    public static string ChromaSubsampling(ChromaSubsampling subsampling) => subsampling switch
    {
        global::BandwidthCalculator.ChromaSubsampling.Cs444 => "4:4:4",
        global::BandwidthCalculator.ChromaSubsampling.Cs422 => "4:2:2",
        global::BandwidthCalculator.ChromaSubsampling.Cs420 => "4:2:0",
        _ => subsampling.ToString(),
    };

    public static string DisplayPortRate(DisplayPortLinkRate rate) => rate switch
    {
        global::BandwidthCalculator.DisplayPortLinkRate.Rbr => "RBR",
        global::BandwidthCalculator.DisplayPortLinkRate.Hbr => "HBR",
        global::BandwidthCalculator.DisplayPortLinkRate.Hbr2 => "HBR2",
        global::BandwidthCalculator.DisplayPortLinkRate.Hbr3 => "HBR3",
        global::BandwidthCalculator.DisplayPortLinkRate.Uhbr10 => "UHBR10",
        global::BandwidthCalculator.DisplayPortLinkRate.Uhbr13Point5 => "UHBR13.5",
        global::BandwidthCalculator.DisplayPortLinkRate.Uhbr20 => "UHBR20",
        _ => rate.ToString(),
    };

    public static string HdmiFrlRate(HdmiFixedRateLinkRate rate) => rate switch
    {
        global::BandwidthCalculator.HdmiFixedRateLinkRate.Frl1 => "FRL1 · 9 Gbps",
        global::BandwidthCalculator.HdmiFixedRateLinkRate.Frl2 => "FRL2 · 18 Gbps",
        global::BandwidthCalculator.HdmiFixedRateLinkRate.Frl3 => "FRL3 · 24 Gbps",
        global::BandwidthCalculator.HdmiFixedRateLinkRate.Frl4 => "FRL4 · 32 Gbps",
        global::BandwidthCalculator.HdmiFixedRateLinkRate.Frl5 => "FRL5 · 40 Gbps",
        global::BandwidthCalculator.HdmiFixedRateLinkRate.Frl6 => "FRL6 · 48 Gbps",
        global::BandwidthCalculator.HdmiFixedRateLinkRate.Gbps64 => "64 Gbps FRL",
        global::BandwidthCalculator.HdmiFixedRateLinkRate.Gbps80 => "80 Gbps FRL",
        global::BandwidthCalculator.HdmiFixedRateLinkRate.Gbps96 => "96 Gbps FRL",
        _ => rate.ToString(),
    };

    public static string HdmiTmdsRate(HdmiTmdsRate rate) => rate switch
    {
        global::BandwidthCalculator.HdmiTmdsRate.MHz165 => "165 MHz TMDS",
        global::BandwidthCalculator.HdmiTmdsRate.MHz340 => "340 MHz TMDS",
        global::BandwidthCalculator.HdmiTmdsRate.MHz600 => "600 MHz TMDS",
        _ => rate.ToString(),
    };
}
