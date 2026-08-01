namespace BandwidthCalculator.Web.Models;

public sealed record SelectOption<TValue>(TValue Value, string Label, bool IsTypical = false);
