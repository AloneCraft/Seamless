#nullable enable

namespace System.Linq {
    // query syntax は最初に同名 extension を探す。List<T>.Where をここで受けて、最適化 enumerable 経路を維持する。
    internal static class ListWhereQueryExtensions {
        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static OptimizedListWhereEnumerable<TSource> Where<TSource>(this global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,bool> predicate) => new(source,predicate);

        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static OptimizedListWhereIndexedEnumerable<TSource> Where<TSource>(this global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,int,bool> predicate) => new(source,predicate);
    }

    internal readonly struct OptimizedListWhereEnumerable<TSource>:global::System.Collections.Generic.IEnumerable<TSource> {
        private readonly global::System.Collections.Generic.List<TSource> source;
        private readonly global::System.Func<TSource,bool> predicate;

        public OptimizedListWhereEnumerable(global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,bool> predicate) {
            this.source=source??throw new global::System.ArgumentNullException(nameof(source));
            this.predicate=predicate??throw new global::System.ArgumentNullException(nameof(predicate));
        }

        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new(this.source,this.predicate);

        global::System.Collections.Generic.IEnumerator<TSource> global::System.Collections.Generic.IEnumerable<TSource>.GetEnumerator() {
            var enumerator = this.GetEnumerator();
            while(enumerator.MoveNext()) {
                yield return enumerator.Current;
            }
        }

        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => ((global::System.Collections.Generic.IEnumerable<TSource>)this).GetEnumerator();

        public struct Enumerator {
            private readonly global::System.Collections.Generic.List<TSource> source;
            private readonly global::System.Func<TSource,bool> predicate;
            private int index;

            public Enumerator(global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,bool> predicate) {
                this.source=source;
                this.predicate=predicate;
                this.index=-1;
                this.Current=default!;
            }

            public TSource Current { get; private set; }

            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() {
                while(true) {
                    var next = this.index+1;
                    if(next>=this.source.Count) {
                        return false;
                    }

                    this.index=next;
                    var candidate = this.source[next];
                    if(!this.predicate(candidate)) {
                        continue;
                    }

                    this.Current=candidate;
                    return true;
                }
            }
        }
    }

    internal readonly struct OptimizedListWhereIndexedEnumerable<TSource>:global::System.Collections.Generic.IEnumerable<TSource> {
        private readonly global::System.Collections.Generic.List<TSource> source;
        private readonly global::System.Func<TSource,int,bool> predicate;

        public OptimizedListWhereIndexedEnumerable(global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,int,bool> predicate) {
            this.source=source??throw new global::System.ArgumentNullException(nameof(source));
            this.predicate=predicate??throw new global::System.ArgumentNullException(nameof(predicate));
        }

        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new(this.source,this.predicate);

        global::System.Collections.Generic.IEnumerator<TSource> global::System.Collections.Generic.IEnumerable<TSource>.GetEnumerator() {
            var enumerator = this.GetEnumerator();
            while(enumerator.MoveNext()) {
                yield return enumerator.Current;
            }
        }

        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator() => ((global::System.Collections.Generic.IEnumerable<TSource>)this).GetEnumerator();

        public struct Enumerator {
            private readonly global::System.Collections.Generic.List<TSource> source;
            private readonly global::System.Func<TSource,int,bool> predicate;
            private int index;

            public Enumerator(global::System.Collections.Generic.List<TSource> source,global::System.Func<TSource,int,bool> predicate) {
                this.source=source;
                this.predicate=predicate;
                this.index=-1;
                this.Current=default!;
            }

            public TSource Current { get; private set; }

            [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() {
                while(true) {
                    var next = this.index+1;
                    if(next>=this.source.Count) {
                        return false;
                    }

                    this.index=next;
                    var candidate = this.source[next];
                    if(!this.predicate(candidate,next)) {
                        continue;
                    }

                    this.Current=candidate;
                    return true;
                }
            }
        }
    }
}
