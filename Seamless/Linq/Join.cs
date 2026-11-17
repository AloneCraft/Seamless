using System;
using System.Buffers;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Seamless.Linq;
namespace Seamless.Linq {
    partial class Enumerable {
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static TResult[] SmallSideJoinToArray<T,TResult>(IEnumerable<T> outer,IEnumerable<T> inner,Func<T,T,TResult> resultSelector) where T:struct =>
            SmallSideJoinToArray(outer,inner,resultSelector,parentBudget:null);

        internal static TResult[] SmallSideJoinToArray<T,TResult>(IEnumerable<T> outer,IEnumerable<T> inner,Func<T,T,TResult> resultSelector,
            global::Seamless.Execution.ResourceBudget? parentBudget) where T:struct {
            ArgumentNullException.ThrowIfNull(outer);
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentNullException.ThrowIfNull(resultSelector);
            if((typeof(T)!=typeof(int)&&typeof(T)!=typeof(long))
                ||!TryJoinInputCount(outer,out var leftCount)||!TryJoinInputCount(inner,out var rightCount)
                ||leftCount==0||leftCount>4096||leftCount>rightCount/4
                ||parentBudget is not null&&parentBudget.AvailableMemoryBytes<leftCount*1024L+256){
                return global::System.Linq.Enumerable.ToArray(global::System.Linq.Enumerable.Join(outer,inner,static value=>value,static value=>value,resultSelector));
            }
            // 固定長キー1件につき1 KiBを上限見積りとし、Lookupの予約はscopeで追跡する。
            using var scope=new global::Seamless.Execution.ExecutionScope(new(){
                ParentBudget=parentBudget,
                MemoryLimitBytes=4096L*1024+256,
                TemporaryStorageLimitBytes=0
            });
            using var lookup=new ScopedLookup<T,T>(scope,EqualityComparer<T>.Default,allowSpill:false);
            foreach(var value in outer){lookup.Add(value,value);}
            var result=new List<TResult>();
            foreach(var right in inner){
                foreach(var left in lookup.Match(right)){result.Add(resultSelector(left,right));}
            }
            return result.ToArray();
        }

        private static bool TryJoinInputCount<T>(IEnumerable<T> source,out int count){
            if(source is IReadOnlyCollection<T> collection){count=collection.Count;return true;}
            return global::System.Linq.Enumerable.TryGetNonEnumeratedCount(source,out count);
        }

        public static ValueEnumerable<Join<TEnumerator,TEnumerator2,TOuter,TInner,TKey,TResult>,TResult> Join<TEnumerator, TEnumerator2, TOuter, TInner, TKey, TResult>(this ValueEnumerable<TEnumerator,TOuter> source,ValueEnumerable<TEnumerator2,TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TOuter>
            where TEnumerator2 : struct, IValueEnumerator<TInner>
            => new(new(source.Enumerator,inner.Enumerator,Throws.IfNull(outerKeySelector),Throws.IfNull(innerKeySelector),Throws.IfNull(resultSelector),null));

        public static ValueEnumerable<Join<TEnumerator,TEnumerator2,TOuter,TInner,TKey,TResult>,TResult> Join<TEnumerator, TEnumerator2, TOuter, TInner, TKey, TResult>(this ValueEnumerable<TEnumerator,TOuter> source,ValueEnumerable<TEnumerator2,TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TOuter>
            where TEnumerator2 : struct, IValueEnumerator<TInner>
            => new(new(source.Enumerator,inner.Enumerator,Throws.IfNull(outerKeySelector),Throws.IfNull(innerKeySelector),Throws.IfNull(resultSelector),comparer));



        public static ValueEnumerable<Join<TEnumerator,FromEnumerable<TInner>,TOuter,TInner,TKey,TResult>,TResult> Join<TEnumerator, TOuter, TInner, TKey, TResult>(this ValueEnumerable<TEnumerator,TOuter> source,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector)
            where TEnumerator : struct, IValueEnumerator<TOuter>
            => new(new(source.Enumerator,Throws.IfNull(inner).AsValueEnumerable().Enumerator,Throws.IfNull(outerKeySelector),Throws.IfNull(innerKeySelector),Throws.IfNull(resultSelector),null));

        public static ValueEnumerable<Join<TEnumerator,FromEnumerable<TInner>,TOuter,TInner,TKey,TResult>,TResult> Join<TEnumerator, TOuter, TInner, TKey, TResult>(this ValueEnumerable<TEnumerator,TOuter> source,IEnumerable<TInner> inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer)
            where TEnumerator : struct, IValueEnumerator<TOuter>
            => new(new(source.Enumerator,Throws.IfNull(inner).AsValueEnumerable().Enumerator,Throws.IfNull(outerKeySelector),Throws.IfNull(innerKeySelector),Throws.IfNull(resultSelector),comparer));
    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Join<TEnumerator, TEnumerator2, TOuter, TInner, TKey, TResult>(TEnumerator source,TEnumerator2 inner,Func<TOuter,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<TOuter,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer)
        :IValueEnumerator<TResult>
            where TEnumerator : struct, IValueEnumerator<TOuter>
            where TEnumerator2 : struct, IValueEnumerator<TInner> {
        private TEnumerator source = source;
        private TEnumerator2 inner = inner;
        private Lookup<TKey,TInner>? innerLookup;
        private Grouping<TKey,TInner>? currentGroup;
        private int currentGroupIndex;
        private TOuter currentOuter = default!;

        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return false;
        }

        public bool TryGetSpan(out ReadOnlySpan<TResult> span) {
            span=default;
            return false;
        }

        public bool TryCopyTo(scoped Span<TResult> destination,Index offset) => false;

        public bool TryGetNext(out TResult current) {
            if(this.innerLookup==null) {
                try {
                    this.innerLookup=Lookup.CreateForJoin(ref this.inner,innerKeySelector,comparer);
                } finally {
                    this.inner.Dispose();
                }
            }

            if(this.innerLookup.Count==0) {
                goto END;
            }

            // iterating group
        ITERATE:
            if(this.currentGroup!=null) {
                if(this.currentGroupIndex<this.currentGroup.Count) {
                    current=resultSelector(this.currentOuter,this.currentGroup[this.currentGroupIndex]);
                    this.currentGroupIndex++;
                    return true;
                } else {
                    this.currentGroup=null;
                }
            }

            while(this.source.TryGetNext(out var value)) {
                var key = outerKeySelector(value);
                if(key is not null) // Enumerable.Join ignores null keys
                {
                    var group = this.innerLookup.GetGroup(key);
                    if(group!=null) {
                        this.currentOuter=value;
                        this.currentGroup=group;
                        this.currentGroupIndex=0;
                        goto ITERATE;
                    }
                }
            }

        END:
            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
            if(this.innerLookup==null) {
                this.inner.Dispose();
            }
            this.source.Dispose();
        }
    }
}
