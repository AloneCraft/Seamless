#pragma warning disable
namespace Seamless.Linq {
    /// <summary>
    /// グループ化・結合の探索表を、キーへ初出順のグループ番号を振ってから、要素を1本の配列へグループ順に並べて作る。
    /// LookupBuilder はグループごとに要素の配列を伸ばす(1→2→4… と作り直してコピーする)ので、小さなグループが多いと割り当てが支配的になる。
    /// こちらはキーからグループ番号への表と、要素・グループ番号の作業領域(ArrayPool から借りる)だけを使い、
    /// グループは結果の1本の配列の区間を指す(1つのグループを持ち続けると、結果の配列全体が生き残る)。
    /// グループの並びはキーの初出順、グループ内の要素は追加順(System.Linq と同じ)。null キーも1つのグループになる。
    /// 可変の構造体なので、局所変数かフィールドに置いて複製せずに使い、最後に Dispose する。
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct ContiguousLookupBuilder<TKey, TElement>:IDisposable {
        private const int MinimumSize = 16;
        private const double LoadFactor = 0.72;
        private readonly IEqualityComparer<TKey> comparer;
        private Dictionary<TKey,int>? map;
        private int nullGroup;
        private TKey[]? keys;
        private int[]? counts;
        private int groupCount;
        private int[]? groupOf;
        private TElement[]? elements;
        private int count;
        // PrepareProbe の後: グループ順に並べた要素(借り物)と、各グループの先頭位置。
        private TElement[]? arranged;
        private int[]? starts;

        /// <param name="elementCapacity">要素数の見込み(分からなければ 0)。</param>
        /// <param name="groupCapacity">グループ数の見込み(分からなければ 0)。</param>
        public ContiguousLookupBuilder(int elementCapacity,int groupCapacity,IEqualityComparer<TKey>? comparer) {
            this.comparer=comparer??EqualityComparer<TKey>.Default;
            this.map=new Dictionary<TKey,int>(Math.Max(groupCapacity,0),this.comparer);
            this.nullGroup=-1;
            var groupSize = Math.Max(groupCapacity,MinimumSize);
            this.keys=ArrayPool<TKey>.Shared.Rent(groupSize);
            this.counts=ArrayPool<int>.Shared.Rent(groupSize);
            this.groupCount=0;
            var elementSize = Math.Max(elementCapacity,MinimumSize);
            this.groupOf=ArrayPool<int>.Shared.Rent(elementSize);
            this.elements=ArrayPool<TElement>.Shared.Rent(elementSize);
            this.count=0;
            this.arranged=null;
            this.starts=null;
        }

        public readonly int Count => this.count;

        public readonly int GroupCount => this.groupCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(TKey key,TElement element) {
            int group;
            if(key is null) {
                if(this.nullGroup<0) {
                    this.nullGroup=this.NewGroup(key);
                }
                group=this.nullGroup;
            } else {
                ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(this.map!,key,out var exists);
                if(!exists) {
                    // NewGroup は表(map)を変えないので slot の参照はそのまま使える。
                    slot=this.NewGroup(key);
                }
                group=slot;
            }
            this.counts![group]++;
            if(this.count==this.groupOf!.Length) {
                this.GrowElements();
            }
            this.groupOf[this.count]=group;
            this.elements![this.count]=element;
            this.count++;
        }

        private int NewGroup(TKey key) {
            if(this.groupCount==this.keys!.Length) {
                this.keys=Grow(this.keys,this.groupCount);
                this.counts=Grow(this.counts!,this.groupCount);
            }
            this.keys[this.groupCount]=key;
            this.counts![this.groupCount]=0;
            return this.groupCount++;
        }

        private void GrowElements() {
            this.groupOf=Grow(this.groupOf!,this.count);
            this.elements=Grow(this.elements!,this.count);
        }

        private static T[] Grow<T>(T[] array,int used) {
            var next = ArrayPool<T>.Shared.Rent(checked(array.Length*2));
            array.AsSpan(0,used).CopyTo(next);
            Return(array,used);
            return next;
        }

        private static void Return<T>(T[]? array,int used) {
            if(array is null) {
                return;
            }
            if(RuntimeHelpers.IsReferenceOrContainsReferences<T>()) {
                array.AsSpan(0,used).Clear();
            }
            ArrayPool<T>.Shared.Return(array);
        }

        // 要素をグループ順に destination へ並べ、各グループの先頭位置を返す(借り物の配列)。
        // 各グループの末尾位置から逆順に詰めると、グループ内は元の順になり、詰め終えたとき末尾位置が先頭位置になる。
        private readonly int[] Arrange(TElement[] destination) {
            var result = ArrayPool<int>.Shared.Rent(Math.Max(this.groupCount,1));
            var counts = this.counts!;
            var offset = 0;
            for(var group = 0;group<this.groupCount;group++) {
                offset+=counts[group];
                result[group]=offset;
            }
            var groupOf = this.groupOf!;
            var elements = this.elements!;
            for(var index = this.count-1;index>=0;index--) {
                destination[--result[groupOf[index]]]=elements[index];
            }
            return result;
        }

        /// <summary>
        /// GroupBy 用: 全グループを作り、追加順の循環リスト(NextGroupInAddOrder)の先頭を返す(グループが無ければ null)。作業領域は返す。
        /// </summary>
        public Grouping<TKey,TElement>? GetRootGroupAndClear() {
            if(this.groupCount==0) {
                this.Dispose();
                return null;
            }
            var sorted = new TElement[this.count];
            var groupStarts = this.Arrange(sorted);
            Grouping<TKey,TElement>? first = null;
            Grouping<TKey,TElement>? last = null;
            for(var group = 0;group<this.groupCount;group++) {
                var grouping = new Grouping<TKey,TElement>(this.keys![group],0,sorted,groupStarts[group],this.counts![group]);
                if(last is null) {
                    first=grouping;
                } else {
                    last.NextGroupInAddOrder=grouping;
                }
                last=grouping;
            }
            last!.NextGroupInAddOrder=first;
            ArrayPool<int>.Shared.Return(groupStarts);
            this.Dispose();
            return first;
        }

        /// <summary>ToLookup 用: 索引を作る(キーで引ける)。作業領域は返す。</summary>
        public Lookup<TKey,TElement> BuildAndClear() {
            if(this.groupCount==0) {
                this.Dispose();
                return Lookup<TKey,TElement>.Empty;
            }
            var sorted = new TElement[this.count];
            var groupStarts = this.Arrange(sorted);
            var bucketCapacity = (int)Math.Ceiling((this.groupCount+1)/LoadFactor);
            var bucketCount = Math.Max((int)BitOperations.RoundUpToPowerOf2((uint)bucketCapacity),MinimumSize);
            var buckets = new Grouping<TKey,TElement>[bucketCount];
            Grouping<TKey,TElement>? first = null;
            Grouping<TKey,TElement>? last = null;
            for(var group = 0;group<this.groupCount;group++) {
                var key = this.keys![group];
                var hashCode = this.InternalGetHashCode(key);
                var grouping = new Grouping<TKey,TElement>(key,hashCode,sorted,groupStarts[group],this.counts![group]);
                // 【重要】Lookup.GetBucketIndex と同じ式にする。
                ref var bucket = ref buckets[(int)(hashCode&(uint)(bucketCount-1))];
                grouping.NextGroupInSameHashCode=bucket;
                bucket=grouping;
                if(last is null) {
                    first=grouping;
                } else {
                    last.NextGroupInAddOrder=grouping;
                }
                last=grouping;
            }
            last!.NextGroupInAddOrder=first;
            ArrayPool<int>.Shared.Return(groupStarts);
            var groupCount = this.groupCount;
            this.Dispose();
            return new Lookup<TKey,TElement>(buckets,last,groupCount,this.comparer,bucketCount,false);
        }

        // 【重要】Lookup.InternalGetHashCode と同じ式にする。
        private readonly uint InternalGetHashCode(TKey key) =>
            (uint)((key is null) ? 0 : this.comparer.GetHashCode(key)&0x7FFFFFFF);

        /// <summary>結合用: 要素をグループ順に並べる(借り物の配列。Dispose で返す)。以後は TryGetRange と Arranged で引く。</summary>
        public void PrepareProbe() {
            var destination = ArrayPool<TElement>.Shared.Rent(Math.Max(this.count,1));
            this.starts=this.Arrange(destination);
            this.arranged=destination;
        }

        /// <summary>PrepareProbe の後に、グループ順に並べた要素。キーの範囲は TryGetRange で得る。</summary>
        public readonly TElement[] Arranged => this.arranged!;

        /// <summary>PrepareProbe の後に、key のグループの要素の範囲 Arranged[start..end) を得る。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryGetRange(TKey key,out int start,out int end) {
            int group;
            if(key is null) {
                group=this.nullGroup;
                if(group<0) {
                    start=end=0;
                    return false;
                }
            } else if(!this.map!.TryGetValue(key,out group)) {
                start=end=0;
                return false;
            }
            start=this.starts![group];
            end=start+this.counts![group];
            return true;
        }

        public void Dispose() {
            Return(this.keys,this.groupCount);
            Return(this.counts,0);
            Return(this.groupOf,0);
            Return(this.elements,this.count);
            Return(this.arranged,this.count);
            Return(this.starts,0);
            this.keys=null;
            this.counts=null;
            this.groupOf=null;
            this.elements=null;
            this.arranged=null;
            this.starts=null;
            this.map=null;
            this.groupCount=0;
            this.count=0;
            this.nullGroup=-1;
        }
    }
}
