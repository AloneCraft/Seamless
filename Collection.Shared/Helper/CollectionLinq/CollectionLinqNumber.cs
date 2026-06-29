using Seamless.Collections;

namespace Collection.Helper.CollectionLinq;

public static class CollectionLinqNumber {
    public static int Parse<T>(T value) =>
        value switch {
            int number => number,
            string text => ParseText(text),
            UnmanagedStruct unmanaged => unmanaged.Value,
            ManagedStruct managed => ParseText(managed.Name),
            ManagedClass managed => ParseText(managed.Name),
            _ => throw new NotSupportedException($"Number extraction is not defined for {typeof(T).FullName}.")
        };

    private static int ParseText(string text) {
        for(var index = 0;index<text.Length;index++) {
            if(char.IsDigit(text[index])) {
                return int.Parse(text[index..],System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        throw new FormatException($"The value '{text}' does not contain a numeric suffix.");
    }
}
