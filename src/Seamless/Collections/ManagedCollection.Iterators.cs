using Seamless.ScanIteratorFunctions;
namespace Seamless.Collections;
public abstract partial class ManagedCollection<TKey,TValue,TKeySerializer,TValueSerializer> {
	public sealed partial class KeyIterator:ScanIteratorFunctions<TKey>;
	internal sealed partial class ValueIterator:ScanIteratorFunctions<TValue>;
	public sealed partial class KeyValueIterator:ScanIteratorFunctions<KeyValuePair<TKey,TValue>>;
}
