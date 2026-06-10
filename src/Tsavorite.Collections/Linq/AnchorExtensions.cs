namespace Tsavorite.Linq;

public static class AnchorExtensions {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Anchor<T>(this T value) => value;
}
