namespace Tsavorite.Collections;

public interface IRecord<TKey> {
    TKey Key { get; }
}
