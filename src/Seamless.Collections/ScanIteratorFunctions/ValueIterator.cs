namespace Seamless.ScanIteratorFunctions;
using Seamless.Collections;
internal sealed partial class ValueIterator<TValue,TSerializer>:ScanIteratorFunctions<TValue>
    where TSerializer:ISerializer<TValue>;
