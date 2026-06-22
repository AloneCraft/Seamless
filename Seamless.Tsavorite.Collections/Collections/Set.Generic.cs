using Seamless.Tsavorite.Collections.Stores;

namespace Seamless.Tsavorite.Collections;

public class Set<TRecord> : HashSet<TRecord> {
    internal Set(CollectionStore<TRecord,EqualityEmpty> collectionStore,IDisposable? ownedStore = null) : base(collectionStore,ownedStore) {
    }
}
