namespace Tsavorite.Collection.Stress;

internal static class StressScale {
    // Scale=1 is the calibrated baseline. Increase for heavier stress runs.
    public const int Scale = 1;

    public static int Apply(int baseCount) {
        var scaled = baseCount*Scale;
        return scaled<1 ? 1 : scaled;
    }
}
