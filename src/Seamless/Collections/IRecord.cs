namespace Seamless.Collections;

public interface IRecord<TKey>
    where TKey : IEquatable<TKey> {
    TKey Key { get; }
}
