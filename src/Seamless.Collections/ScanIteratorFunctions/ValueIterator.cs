namespace Seamless.ScanIteratorFunctions;
using Collections;
internal sealed partial class ValueIterator<TValue,TSerializer>:ScanIteratorFunctions<TValue>
    where TSerializer:ISerializer<TValue>;
