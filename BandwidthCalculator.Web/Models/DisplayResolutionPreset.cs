namespace BandwidthCalculator.Web.Models;

public sealed record DisplayResolutionPreset(
    string Id,
    string Label,
    string CommonName,
    int Width,
    int Height,
    bool IsTypical = false,
    int? AspectRatioWidth = null,
    int? AspectRatioHeight = null,
    double? DisplayDiagonalInches = null)
{
    public const string CustomId = "custom";

    public static IReadOnlyList<DisplayResolutionPreset> All { get; } =
    [
        new("1280x720", "HD (720p)", "720p", 1280, 720),
        new("1920x1080", "Full HD (1080p)", "1080p", 1920, 1080, IsTypical: true),
        new("1920x1200", "WUXGA", "WUXGA", 1920, 1200),
        new("2560x1440", "QHD (1440p)", "1440p", 2560, 1440, IsTypical: true),
        new("2560x1600", "WQXGA", "WQXGA", 2560, 1600),
        new("2560x1664", "Apple 13-inch MacBook Air", "13″ MacBook Air", 2560, 1664, DisplayDiagonalInches: 13.6d),
        new("2880x1864", "Apple 15-inch MacBook Air", "15″ MacBook Air", 2880, 1864, DisplayDiagonalInches: 15.3d),
        new("3024x1964", "Apple 14-inch MacBook Pro", "14″ MacBook Pro", 3024, 1964, DisplayDiagonalInches: 14.2d),
        new("3440x1440", "UWQHD ultrawide", "UWQHD", 3440, 1440, IsTypical: true),
        new("3456x2234", "Apple 16-inch MacBook Pro", "16″ MacBook Pro", 3456, 2234, DisplayDiagonalInches: 16.2d),
        new("3840x1600", "UWQHD+ ultrawide", "UWQHD+", 3840, 1600),
        new("3840x2160", "4K UHD (2160p)", "4K UHD", 3840, 2160, IsTypical: true),
        new("4096x2160", "DCI 4K", "DCI 4K", 4096, 2160),
        new("4096x2304", "Apple Retina 4K (21.5-inch iMac)", "Retina 4K", 4096, 2304, DisplayDiagonalInches: 21.5d),
        new("4480x2520", "Apple Retina 4.5K (24-inch iMac)", "Retina 4.5K", 4480, 2520, DisplayDiagonalInches: 23.5d),
        new("5120x1440", "Dual QHD super-ultrawide", "Dual QHD", 5120, 1440),
        new("5120x2160", "5K2K ultrawide", "5K2K", 5120, 2160),
        new("5120x2880", "Apple Retina 5K (27-inch iMac / Studio Display)", "Retina 5K", 5120, 2880, DisplayDiagonalInches: 27d),
        new("6016x3384", "Apple Pro Display XDR (6K Retina)", "Retina 6K", 6016, 3384, DisplayDiagonalInches: 32d),
        new("6144x3456", "6K", "6K", 6144, 3456),
        new("6720x2880", "Apple Vision Pro Mac Virtual Display Wide", "Vision Pro Wide", 6720, 2880, AspectRatioWidth: 21, AspectRatioHeight: 9),
        new("7680x4320", "8K UHD (4320p)", "8K UHD", 7680, 4320),
        new("10240x2880", "Apple Vision Pro Mac Virtual Display Ultrawide", "Vision Pro Ultrawide", 10240, 2880),
    ];

    public static DisplayResolutionPreset? Find(string? id) =>
        All.FirstOrDefault(preset => preset.Id == id);

    public static string Match(int width, int height) =>
        All.FirstOrDefault(preset => preset.Width == width && preset.Height == height)?.Id ?? CustomId;
}
