namespace Seamless.Collections;

public interface IRecord<out TKey> {
    TKey Key { get; }
}
