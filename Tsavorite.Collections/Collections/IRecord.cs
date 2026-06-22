namespace Seamless.Collections.Tsavorite;

public interface IRecord<TKey> {
    TKey Key { get; }
}
