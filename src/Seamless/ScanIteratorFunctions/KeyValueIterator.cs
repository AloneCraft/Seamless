namespace Seamless.ScanIteratorFunctions;
using Collections;
public sealed partial class KeyValueIterator<TKey,TValue,TKeySerializer,TValueSerializer>:ScanIteratorFunctions<KeyValuePair<TKey,TValue>>
    where TKeySerializer:ISerializer<TKey>
    where TValueSerializer:ISerializer<TValue>;
