namespace Hakari.Core.Pricing;

public static class PricingFiles
{
    public const string BundledFileName = "pricing.json";

    public static string BundledPath => Path.Combine(AppContext.BaseDirectory, BundledFileName);
}
