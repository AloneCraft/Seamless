namespace Seamless.ScanIteratorFunctions;
using Collections;
public sealed partial class KeyIterator<T,TSerializer>:ScanIteratorFunctions<T>
    where TSerializer:ISerializer<T>;
public sealed partial class KeyIterator<T,TKey,TValue>:ScanIteratorFunctions<T,TKey,TValue>;
