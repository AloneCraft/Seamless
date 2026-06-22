namespace Seamless.Collections.Tsavorite.Linq;

public static class AnchorExtensions {
    extension<T>(T value) {
        public T Anchor {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => value;
        }
    }
}
