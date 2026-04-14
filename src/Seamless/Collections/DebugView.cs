using System.Diagnostics;
namespace Seamless.Collections;

public class DebugView<TKey> {
    /// <summary>
    /// デバッガーに表示する要素の上限。.NET の List&lt;T&gt; デバッグビューに倣い 10,000 件とする。
    /// </summary>
    private const int MaxItems = 10_000;
    private readonly ICollection<TKey> _set;
    public DebugView(ICollection<TKey> set) {
        ArgumentNullException.ThrowIfNull(set);
        _set=set;
    }
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public TKey[] Items {
        get {
            var count = Math.Min(_set.Count,MaxItems);
            var items = new TKey[count];
            var i = 0;
            foreach(var item in _set) {
                if(i>=count) break;
                items[i++]=item;
            }
            return items;
        }
    }
}
public class DebugView<TKey, TValue>:DebugView<KeyValuePair<TKey,TValue>> {
    public DebugView(ICollection<KeyValuePair<TKey,TValue>> set) : base(set) {
    }
}

///// <summary>
///// コレクション型のデバッガービュー。
///// ウォッチ式で大量の要素を展開した際にデバッガーが固まるのを防ぐため、
///// 表示件数を <see cref="MaxItems"/> で制限する。
///// </summary>
//public static class DebugView {
//    /// <summary>
//    /// デバッガーに表示する要素の上限。.NET の List&lt;T&gt; デバッグビューに倣い 10,000 件とする。
//    /// </summary>
//    internal const int MaxItems=10_000;

//    /// <summary>
//    /// <see cref="UnmanagedHashSet{TKey}"/> のデバッガービュー。
//    /// </summary>
//    public sealed class UnmanagedHashSet<TKey> where TKey : unmanaged, IEquatable<TKey>{
//        private readonly Collections.UnmanagedHashSet<TKey> _set;
//        public UnmanagedHashSet(Collections.UnmanagedHashSet<TKey> set){
//            ArgumentNullException.ThrowIfNull(set);
//            _set=set;
//        }
//        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
//        public TKey[] Items {
//            get {
//                var count=Math.Min(_set.Count, MaxItems);
//                var items=new TKey[count];
//                var i=0;
//                foreach(var item in _set){
//                    if(i >= count) break;
//                    items[i++]=item;
//                }
//                return items;
//            }
//        }
//    }

//    /// <summary>
//    /// <see cref="Collections.ManagedHashSet{TKey}"/> のデバッガービュー。
//    /// </summary>
//    public sealed class HashSet<TKey> where TKey : IEquatable<TKey>{
//        private readonly Collections.ManagedHashSet<TKey> _set;
//        public HashSet(Collections.ManagedHashSet<TKey> set){
//            ArgumentNullException.ThrowIfNull(set);
//            _set=set;
//        }
//        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
//        public TKey[] Items {
//            get {
//                var count=Math.Min(_set.Count, MaxItems);
//                var items=new TKey[count];
//                var i=0;
//                foreach(var item in _set){
//                    if(i >= count) break;
//                    items[i++]=item;
//                }
//                return items;
//            }
//        }
//    }

//    /// <summary>
//    /// <see cref="Collections.ManagedDictionary{TKey,TValue,TKeySerializer,TValueSerializer}"/> のデバッガービュー。
//    /// </summary>
//    public sealed class Dictionary<TKey, TValue, TKeySerializer, TValueSerializer>
//        where TKey : IEquatable<TKey>
//        where TValue : IEquatable<TValue>
//        where TKeySerializer : ISerializer<TKey>
//        where TValueSerializer : ISerializer<TValue>{
//        private readonly ManagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer> _dictionary;
//        public Dictionary(ManagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer> dictionary){
//            ArgumentNullException.ThrowIfNull(dictionary);
//            _dictionary=dictionary;
//        }
//        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
//        public KeyValuePair<TKey, TValue>[] Items {
//            get {
//                var count=Math.Min(_dictionary.Count, MaxItems);
//                var items=new KeyValuePair<TKey, TValue>[count];
//                var i=0;
//                foreach(var kvp in _dictionary){
//                    if(i >= count) break;
//                    items[i++]=kvp;
//                }
//                return items;
//            }
//        }
//    }

//    /// <summary>
//    /// <see cref="Collections.UnmanagedDictionary{TKey,TValue,TKeySerializer,TValueSerializer}"/> のデバッガービュー。
//    /// </summary>
//    public sealed class UnmanagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer>
//        where TKey : unmanaged, IEquatable<TKey>
//        where TValue : unmanaged, IEquatable<TValue>
//        where TKeySerializer : ISerializer<TKey>
//        where TValueSerializer : ISerializer<TValue>{
//        private readonly Collections.UnmanagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer> _dictionary;
//        public UnmanagedDictionary(Collections.UnmanagedDictionary<TKey, TValue, TKeySerializer, TValueSerializer> dictionary){
//            ArgumentNullException.ThrowIfNull(dictionary);
//            _dictionary=dictionary;
//        }
//        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
//        public KeyValuePair<TKey, TValue>[] Items {
//            get {
//                var count=Math.Min(_dictionary.Count, MaxItems);
//                var items=new KeyValuePair<TKey, TValue>[count];
//                var i=0;
//                foreach(var kvp in _dictionary){
//                    if(i >= count) break;
//                    items[i++]=kvp;
//                }
//                return items;
//            }
//        }
//    }
//}
