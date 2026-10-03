using System.Buffers;
using System.Runtime.InteropServices;
using Shared.Linq.Ideal;

namespace Shared.Linq.IdealRange;

/// <summary>
/// Range 起点(Enumerable.Range(1,n))の手書き理想ループ版。Shared/Linq.Range/Range_start_count.cs と同じメソッド名・同じ枠。
/// 範囲の個数・端・添字、値が一意で昇順であることを使う。ラムダは .Anchor が無いので純粋とみなし、
/// 結果が範囲の形から決まるもの(偶奇の個数、比較の境界、和、単調な選択子の端、x%1000 の並びとグループ)は数式で求める。
/// n は実行時の個数(CountFor)なので、数式は n によらず成り立つ形にする(桁あふれし得る n では要素を回す)。
/// </summary>
public abstract class Range_start_count(int count):global::Common.Base<int>(count) {
    private const int ElementCount = 4_096;
    private int CountFor(int count) => this.EffectiveSourceCount(count);

    /// <summary>Enumerable.Range と同じ引数検査。</summary>
    private static void Check(int start,int count) {
        if(count<0||(long)start+count-1>int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
    }

    /// <summary>1..n の和。n は int なので long で桁あふれしない。</summary>
    private static long TriangularSum(int count) => (long)count*(count+1)/2;

    /// <summary>x%1000 の値 key を持つ 1..n の要素の最小値(無ければ n を超える)。</summary>
    private static int FirstWithKey(int key) => key==0 ? 1000 : key;

    /// <summary>1..n に現れる x%1000 の key の個数。初出順は 1,2,…,999、0。</summary>
    private static int KeyCount(int count) {
        if(count>=1000) {
            return 1000;
        }
        return count;
    }

    /// <summary>初出順で ordinal 番目の key(0 始まり)。反復子を使わず添字で回すための補助。</summary>
    private static int KeyAt(int ordinal) {
        if(ordinal<999) {
            return ordinal+1;
        }
        return 0;
    }

    private static void ConsumeInterval(global::Common.Consumer consumer,long from,long to) {
        for(var value = from;value<to;value++) {
            consumer.Consume((int)value);
        }
    }

    [BenchFact,Benchmark]
    public void Consume() => this.Consume_Impl();
    private void Consume_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1,1L+count);
    }

    [BenchFact,Benchmark]
    public void Aggregate_func() => this.Aggregate_func_Impl();
    private void Aggregate_func_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        this.consumer.Consume(TriangularSum(count));
    }

    [BenchFact,Benchmark]
    public void All_predicate() => this.All_predicate_Impl();
    private void All_predicate_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // 始点が 1 なので x>0 はすべての要素で真(空でも真)。
        this.consumer.Consume(true);
    }

    [BenchFact,Benchmark]
    public void Any_predicate() => this.Any_predicate_Impl();
    private void Any_predicate_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // 最大値 n が 2048 以上なら真。
        this.consumer.Consume(count>=2_048);
    }

    [BenchFact,Benchmark]
    public void Append_element() => this.Append_element_Impl();
    private void Append_element_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1,1L+count);
        this.consumer.Consume(0);
    }

    [BenchFact,Benchmark]
    public void Average() => this.Average_Impl();
    private void Average_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        if(count==0) {
            throw IdealErrors.NoElements();
        }
        this.consumer.Consume((double)TriangularSum(count)/count);
    }

    [BenchFact,Benchmark]
    public void Chunk_size() => this.Chunk_size_Impl();
    private void Chunk_size_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        var consumer = this.consumer;
        for(var offset = 0;offset<count;offset+=128) {
            var chunk = new int[Math.Min(128,count-offset)];
            for(var index = 0;index<chunk.Length;index++) {
                chunk[index]=1+offset+index;
            }
            consumer.Consume(chunk);
        }
    }

    [BenchFact,Benchmark]
    public void Concat_second() => this.Concat_second_Impl();
    private void Concat_second_Impl() {
        var length = this.CountFor(ElementCount);
        Check(1,length);
        Check(length/2+1,length/2);
        ConsumeInterval(this.consumer,1,1L+length);
        ConsumeInterval(this.consumer,length/2+1,length/2+1L+length/2);
    }

    [BenchFact,Benchmark]
    public void Contains_value() => this.Contains_value_Impl();
    private void Contains_value_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        this.consumer.Consume((uint)(2_048-1)<(uint)count);
    }

    [BenchFact,Benchmark]
    public void Count() => this.Count_Impl();
    private void Count_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        this.consumer.Consume(count);
    }

    [BenchFact,Benchmark]
    public void Count_predicate() => this.Count_predicate_Impl();
    private void Count_predicate_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // 1..n の偶数は n/2 個。
        this.consumer.Consume(count/2);
    }

    [BenchFact,Benchmark]
    public void CountBy_keySelector() => this.CountBy_keySelector_Impl();
    private void CountBy_keySelector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // x%1000 が key の要素は key(0 は 1000)から 1000 刻み。初出順は 1..999、0。
        for(var ordinal = 0;ordinal<KeyCount(count);ordinal++) {
            var key = KeyAt(ordinal);
            this.consumer.Consume(new KeyValuePair<int,int>(key,(count-FirstWithKey(key))/1000+1));
        }
    }

    [BenchFact,Benchmark]
    public void DefaultIfEmpty() => this.DefaultIfEmpty_Impl();
    private void DefaultIfEmpty_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        if(count==0) {
            this.consumer.Consume(0);
            return;
        }
        ConsumeInterval(this.consumer,1,1L+count);
    }

    [BenchFact,Benchmark]
    public void Distinct() => this.Distinct_Impl();
    private void Distinct_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1,1L+count);
    }

    [BenchFact,Benchmark]
    public void DistinctBy_keySelector() => this.DistinctBy_keySelector_Impl();
    private void DistinctBy_keySelector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // 各 key の最初の要素は 1..1000 で、その順に初めて現れる。
        ConsumeInterval(this.consumer,1,1L+Math.Min(count,1000));
    }

    [BenchFact,Benchmark]
    public void ElementAt_index() => this.ElementAt_index_Impl();
    private void ElementAt_index_Impl() {
        var count = this.CountFor(ElementCount);
        var index = count/2;
        Check(1,count);
        if((uint)index>=(uint)count) {
            throw IdealErrors.IndexOutOfRange();
        }
        this.consumer.Consume(1+index);
    }

    [BenchFact,Benchmark]
    public void Except_second() => this.Except_second_Impl();
    private void Except_second_Impl() {
        var length = this.CountFor(ElementCount);
        Check(1,length);
        Check(length/2+1,length/2);
        this.ConsumeExcept(1,length,length/2+1,length/2);
    }

    /// <summary>区間 [a,a+c) から区間 [b,b+d) を除いた値を a の順に流す。</summary>
    private void ConsumeExcept(long a,long c,long b,long d) {
        var end = a+c;
        if(d<=0) {
            ConsumeInterval(this.consumer,a,end);
            return;
        }
        ConsumeInterval(this.consumer,a,Math.Min(end,Math.Max(a,b)));
        ConsumeInterval(this.consumer,Math.Max(a,b+d),end);
    }

    [BenchFact,Benchmark]
    public void First_predicate() => this.First_predicate_Impl();
    private void First_predicate_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // 始点 1 が x>=1 を満たす。
        if(count==0) {
            throw IdealErrors.NoMatch();
        }
        this.consumer.Consume(1);
    }

    [BenchFact,Benchmark]
    public void GroupBy_keySelector() => this.GroupBy_keySelector_Impl();
    private void GroupBy_keySelector_Impl() => this.ConsumeGroups(1);

    /// <summary>
    /// キー x%1000 でグループ化する。要素は x*multiplier(要素選択子なしは 1、x => x*2 は 2)。
    /// グループの初出順は 1..999、0 で、各グループの要素は key(0 は 1000)から 1000 刻み。ハッシュを使わずに並べる。
    /// </summary>
    private void ConsumeGroups(int multiplier) {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        var sorted = new int[count];
        var position = 0;
        for(var ordinal = 0;ordinal<KeyCount(count);ordinal++) {
            var key = KeyAt(ordinal);
            var start = position;
            for(var x = FirstWithKey(key);x<=count;x+=1000) {
                sorted[position++]=x*multiplier;
            }
            this.consumer.Consume(new IdealGrouping<int,int>(key,sorted,start,position-start));
        }
    }

    [BenchFact,Benchmark]
    public void Index() => this.Index_Impl();
    private void Index_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        for(var index = 0;index<count;index++) {
            this.consumer.Consume((Index: index,Item: 1+index));
        }
    }

    [BenchFact,Benchmark]
    public void Intersect_second() => this.Intersect_second_Impl();
    private void Intersect_second_Impl() {
        var length = this.CountFor(ElementCount);
        Check(1,length);
        Check(length/2+1,length/2);
        ConsumeInterval(this.consumer,Math.Max(1,length/2+1),Math.Min(1L+length,length/2+1L+length/2));
    }

    [BenchFact,Benchmark]
    public void Join_inner() => this.Join_inner_Impl();
    private void Join_inner_Impl() {
        var length = this.CountFor(ElementCount);
        Check(1,length);
        Check(length/2+1,length/2);
        // 恒等キーの等値結合は、外側の順に区間の共通部分だけが1件ずつ一致する。
        var from = Math.Max(1L,length/2+1L);
        var to = Math.Min(1L+length,length/2+1L+length/2);
        for(var x = from;x<to;x++) {
            this.consumer.Consume((int)x+(int)x);
        }
    }

    [BenchFact,Benchmark]
    public void Last_predicate() => this.Last_predicate_Impl();
    private void Last_predicate_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // 終点 n が x>=1 を満たす。
        if(count==0) {
            throw IdealErrors.NoMatch();
        }
        this.consumer.Consume(count);
    }

    [BenchFact,Benchmark]
    public void Max() => this.Max_Impl();
    private void Max_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        if(count==0) {
            throw IdealErrors.NoElements();
        }
        this.consumer.Consume(count);
    }

    [BenchFact,Benchmark]
    public void Min() => this.Min_Impl();
    private void Min_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        if(count==0) {
            throw IdealErrors.NoElements();
        }
        this.consumer.Consume(1);
    }

    [BenchFact,Benchmark]
    public void OrderBy_keySelector() => this.OrderBy_keySelector_Impl();
    private void OrderBy_keySelector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // key 0..999 の昇順に、同じ key の要素を元の順(昇順)で流す。安定ソートと同じ並び。
        for(var key = 0;key<1000;key++) {
            for(var x = FirstWithKey(key);x<=count;x+=1000) {
                this.consumer.Consume(x);
            }
        }
    }

    [BenchFact,Benchmark]
    public void Prepend_element() => this.Prepend_element_Impl();
    private void Prepend_element_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        this.consumer.Consume(0);
        ConsumeInterval(this.consumer,1,1L+count);
    }

    [BenchFact,Benchmark]
    public void Reverse() => this.Reverse_Impl();
    private void Reverse_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        for(var x = count;x>=1;x--) {
            this.consumer.Consume(x);
        }
    }

    [BenchFact,Benchmark]
    public void Select_selector() => this.Select_selector_Impl();
    private void Select_selector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        for(var x = 1;x<=count;x++) {
            this.consumer.Consume(x*2);
        }
    }

    [BenchFact,Benchmark]
    public void SequenceEqual_second() => this.SequenceEqual_second_Impl();
    private void SequenceEqual_second_Impl() {
        var length = this.CountFor(ElementCount);
        Check(1,length);
        Check(1,length);
        // 同じ始点・同じ個数の範囲は等しい。
        this.consumer.Consume(true);
    }

    [BenchFact,Benchmark]
    public void Skip_count() => this.Skip_count_Impl();
    private void Skip_count_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1+Math.Clamp(1_024,0,count),1L+count);
    }

    [BenchFact,Benchmark]
    public void SkipWhile_predicate() => this.SkipWhile_predicate_Impl();
    private void SkipWhile_predicate_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // x<1024 は 1..1023 で真なので、1024 から流す。
        ConsumeInterval(this.consumer,Math.Min(1_024,1L+count),1L+count);
    }

    [BenchFact,Benchmark]
    public void Sum() => this.Sum_Impl();
    private void Sum_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // int の合計は桁あふれで例外になる(System.Linq と同じ)。
        this.consumer.Consume(checked((int)TriangularSum(count)));
    }

    [BenchFact,Benchmark]
    public void Take_count() => this.Take_count_Impl();
    private void Take_count_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1,1L+Math.Clamp(3_072,0,count));
    }

    [BenchFact,Benchmark]
    public void TakeWhile_predicate() => this.TakeWhile_predicate_Impl();
    private void TakeWhile_predicate_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1,1L+Math.Min(count,3_072));
    }

    [BenchFact,Benchmark]
    public void ToArray() => this.ToArray_Impl();
    private void ToArray_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1,1L+count);
    }

    [BenchFact,Benchmark]
    public void ToDictionary_keySelector() => this.ToDictionary_keySelector_Impl();
    private void ToDictionary_keySelector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // 範囲の値は一意なので重複キーの例外は起きず、辞書の列挙順は挿入順。
        for(var x = 1;x<=count;x++) {
            this.consumer.Consume(new KeyValuePair<int,int>(x,x));
        }
    }

    [BenchFact,Benchmark]
    public void ToHashSet() => this.ToHashSet_Impl();
    private void ToHashSet_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1,1L+count);
    }

    [BenchFact,Benchmark]
    public void ToList() => this.ToList_Impl();
    private void ToList_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1,1L+count);
    }

    [BenchFact,Benchmark]
    public void Union_second() => this.Union_second_Impl();
    private void Union_second_Impl() {
        var length = this.CountFor(ElementCount);
        Check(1,length);
        Check(length/2+1,length/2);
        this.ConsumeUnion(1,length,length/2+1,length/2);
    }

    /// <summary>区間 [a,a+c) を流したあと、区間 [b,b+d) のうち前者に無い値を b の順に流す。</summary>
    private void ConsumeUnion(long a,long c,long b,long d) {
        var firstEnd = a+c;
        var secondEnd = b+d;
        ConsumeInterval(this.consumer,a,firstEnd);
        if(c<=0) {
            ConsumeInterval(this.consumer,b,secondEnd);
            return;
        }
        ConsumeInterval(this.consumer,b,Math.Min(secondEnd,Math.Max(b,a)));
        ConsumeInterval(this.consumer,Math.Max(b,firstEnd),secondEnd);
    }

    [BenchFact,Benchmark]
    public void Where_predicate() => this.Where_predicate_Impl();
    private void Where_predicate_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        for(var x = 2;x<=count;x+=2) {
            this.consumer.Consume(x);
        }
    }

    [BenchFact,Benchmark]
    public void Zip_second() => this.Zip_second_Impl();
    private void Zip_second_Impl() {
        var length = this.CountFor(ElementCount);
        Check(1,length);
        Check(1,length);
        for(var x = 1;x<=length;x++) {
            this.consumer.Consume(x+x);
        }
    }

    [BenchFact,Benchmark]
    public void Aggregate_seed_func_resultSelector() => this.Aggregate_seed_func_resultSelector_Impl();
    private void Aggregate_seed_func_resultSelector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        this.consumer.Consume((double)TriangularSum(count)/1_000_000);
    }

    [BenchFact,Benchmark]
    public void Any() => this.Any_Impl();
    private void Any_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        this.consumer.Consume(count!=0);
    }

    [BenchFact,Benchmark]
    public void Average_selector() => this.Average_selector_Impl();
    private void Average_selector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        if(count==0) {
            throw IdealErrors.NoElements();
        }
        // double の逐次加算は 2^53 未満の整数の間は正確なので、和の公式と同じ値になる。
        if(TriangularSum(count)<(1L<<53)) {
            this.consumer.Consume((double)TriangularSum(count)/count);
            return;
        }
        var sum = 0d;
        for(var x = 1;x<=count;x++) {
            sum+=(double)x;
        }
        this.consumer.Consume(sum/count);
    }

    [BenchFact,Benchmark]
    public void Cast() => this.Cast_Impl();
    private void Cast_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1,1L+count);
    }

    [BenchFact,Benchmark]
    public void ElementAtOrDefault_index() => this.ElementAtOrDefault_index_Impl();
    private void ElementAtOrDefault_index_Impl() {
        var length = this.CountFor(ElementCount);
        Check(1,length);
        var index = length/2;
        if((uint)index<(uint)length) {
            this.consumer.Consume(1+index);
        } else {
            this.consumer.Consume(0);
        }
    }

    [BenchFact,Benchmark]
    public void ExceptBy_keySelector() => this.ExceptBy_keySelector_Impl();
    private void ExceptBy_keySelector_Impl() {
        var length = this.CountFor(ElementCount);
        Check(1,length);
        Check(length/2+1,length/2);
        this.ConsumeExcept(1,length,length/2+1,length/2);
    }

    [BenchFact,Benchmark]
    public void First() => this.First_Impl();
    private void First_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        if(count==0) {
            throw IdealErrors.NoElements();
        }
        this.consumer.Consume(1);
    }

    [BenchFact,Benchmark]
    public void FirstOrDefault() => this.FirstOrDefault_Impl();
    private void FirstOrDefault_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        if(count==0) {
            this.consumer.Consume(0);
        } else {
            this.consumer.Consume(1);
        }
    }

    [BenchFact,Benchmark]
    public void FirstOrDefault_predicate() => this.FirstOrDefault_predicate_Impl();
    private void FirstOrDefault_predicate_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        if(count==0) {
            this.consumer.Consume(0);
        } else {
            this.consumer.Consume(1);
        }
    }

    [BenchFact,Benchmark]
    public void GroupBy_elementSelector() => this.GroupBy_elementSelector_Impl();
    private void GroupBy_elementSelector_Impl() => this.ConsumeGroups(2);

    [BenchFact,Benchmark]
    public void GroupBy_resultSelector() => this.GroupBy_resultSelector_Impl();
    private void GroupBy_resultSelector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        for(var ordinal = 0;ordinal<KeyCount(count);ordinal++) {
            this.consumer.Consume(KeyAt(ordinal));
        }
    }

    [BenchFact,Benchmark]
    public void GroupJoin_inner() => this.GroupJoin_inner_Impl();
    private void GroupJoin_inner_Impl() {
        var length = this.CountFor(ElementCount);
        Check(1,length);
        Check(length/2+1,length/2);
        ConsumeInterval(this.consumer,1,1L+length);
    }

    [BenchFact,Benchmark]
    public void IntersectBy_keySelector() => this.IntersectBy_keySelector_Impl();
    private void IntersectBy_keySelector_Impl() {
        var length = this.CountFor(ElementCount);
        Check(1,length);
        Check(length/2+1,length/2);
        ConsumeInterval(this.consumer,Math.Max(1,length/2+1),Math.Min(1L+length,length/2+1L+length/2));
    }

    [BenchFact,Benchmark]
    public void Last() => this.Last_Impl();
    private void Last_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        if(count==0) {
            throw IdealErrors.NoElements();
        }
        this.consumer.Consume(count);
    }

    [BenchFact,Benchmark]
    public void LastOrDefault() => this.LastOrDefault_Impl();
    private void LastOrDefault_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        this.consumer.Consume(count);
    }

    [BenchFact,Benchmark]
    public void LongCount() => this.LongCount_Impl();
    private void LongCount_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        this.consumer.Consume((long)count);
    }

    [BenchFact,Benchmark]
    public void LongCount_predicate() => this.LongCount_predicate_Impl();
    private void LongCount_predicate_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        this.consumer.Consume((long)(count/2));
    }

    [BenchFact,Benchmark]
    public void Max_selector() => this.Max_selector_Impl();
    private void Max_selector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        if(count==0) {
            throw IdealErrors.NoElements();
        }
        // x*2 は桁あふれしない範囲で単調増加なので、最大は終点の値。
        if(count<=int.MaxValue/2) {
            this.consumer.Consume(count*2);
            return;
        }
        var result = 1*2;
        for(var x = 2;x<=count;x++) {
            var value = x*2;
            if(value>result) {
                result=value;
            }
        }
        this.consumer.Consume(result);
    }

    [BenchFact,Benchmark]
    public void MaxBy_keySelector() => this.MaxBy_keySelector_Impl();
    private void MaxBy_keySelector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        if(count==0) {
            throw IdealErrors.NoElements();
        }
        // key x%1000 の最大は 999(n<999 なら n)で、最初に現れる要素を返す。
        this.consumer.Consume(Math.Min(count,999));
    }

    [BenchFact,Benchmark]
    public void Min_selector() => this.Min_selector_Impl();
    private void Min_selector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        if(count==0) {
            throw IdealErrors.NoElements();
        }
        // x*2 は桁あふれしない範囲で単調増加なので、最小は始点の値。
        if(count<=int.MaxValue/2) {
            this.consumer.Consume(1*2);
            return;
        }
        var result = 1*2;
        for(var x = 2;x<=count;x++) {
            var value = x*2;
            if(value<result) {
                result=value;
            }
        }
        this.consumer.Consume(result);
    }

    [BenchFact,Benchmark]
    public void MinBy_keySelector() => this.MinBy_keySelector_Impl();
    private void MinBy_keySelector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        if(count==0) {
            throw IdealErrors.NoElements();
        }
        // key x%1000 の最小は 0(x=1000)、n<1000 なら 1(x=1)。最初に現れる要素を返す。
        if(count>=1000) {
            this.consumer.Consume(1000);
        } else {
            this.consumer.Consume(1);
        }
    }

    [BenchFact,Benchmark]
    public void OfType() => this.OfType_Impl();
    private void OfType_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1,1L+count);
    }

    [BenchFact,Benchmark]
    public void Order() => this.Order_Impl();
    private void Order_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1,1L+count);
    }

    [BenchFact,Benchmark]
    public void OrderByDescending_keySelector() => this.OrderByDescending_keySelector_Impl();
    private void OrderByDescending_keySelector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // key 999..0 の降順に、同じ key の要素を元の順(昇順)で流す。安定ソートと同じ並び。
        for(var key = 999;key>=0;key--) {
            for(var x = FirstWithKey(key);x<=count;x+=1000) {
                this.consumer.Consume(x);
            }
        }
    }

    [BenchFact,Benchmark]
    public void OrderDescending() => this.OrderDescending_Impl();
    private void OrderDescending_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        for(var x = count;x>=1;x--) {
            this.consumer.Consume(x);
        }
    }

    [BenchFact,Benchmark]
    public void Select_indexSelector() => this.Select_indexSelector_Impl();
    private void Select_indexSelector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        for(var index = 0;index<count;index++) {
            this.consumer.Consume(1+index+index);
        }
    }

    [BenchFact,Benchmark]
    public void SkipLast_count() => this.SkipLast_count_Impl();
    private void SkipLast_count_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1,1L+count-Math.Clamp(1_024,0,count));
    }

    [BenchFact,Benchmark]
    public void SkipWhile_indexPredicate() => this.SkipWhile_indexPredicate_Impl();
    private void SkipWhile_indexPredicate_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1+Math.Min(count,1_024),1L+count);
    }

    [BenchFact,Benchmark]
    public void Sum_selector() => this.Sum_selector_Impl();
    private void Sum_selector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        this.consumer.Consume(TriangularSum(count));
    }

    [BenchFact,Benchmark]
    public void TakeLast_count() => this.TakeLast_count_Impl();
    private void TakeLast_count_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1L+count-Math.Clamp(1_024,0,count),1L+count);
    }

    [BenchFact,Benchmark]
    public void TakeWhile_indexPredicate() => this.TakeWhile_indexPredicate_Impl();
    private void TakeWhile_indexPredicate_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        ConsumeInterval(this.consumer,1,1L+Math.Min(count,3_072));
    }

    [BenchFact,Benchmark]
    public void ToDictionary_elementSelector() => this.ToDictionary_elementSelector_Impl();
    private void ToDictionary_elementSelector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        for(var x = 1;x<=count;x++) {
            this.consumer.Consume(new KeyValuePair<int,int>(x,x*2));
        }
    }

    [BenchFact,Benchmark]
    public void ToLookup_keySelector() => this.ToLookup_keySelector_Impl();
    private void ToLookup_keySelector_Impl() => this.ConsumeGroups(1);

    [BenchFact,Benchmark]
    public void ToLookup_elementSelector() => this.ToLookup_elementSelector_Impl();
    private void ToLookup_elementSelector_Impl() => this.ConsumeGroups(2);

    [BenchFact,Benchmark]
    public void UnionBy_keySelector() => this.UnionBy_keySelector_Impl();
    private void UnionBy_keySelector_Impl() {
        var length = this.CountFor(ElementCount);
        Check(1,length);
        Check(length/2+1,length/2);
        this.ConsumeUnion(1,length,length/2+1,length/2);
    }

    [BenchFact,Benchmark]
    public void Where_indexPredicate() => this.Where_indexPredicate_Impl();
    private void Where_indexPredicate_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // 添字が偶数の要素は 1,3,5,…。
        for(var x = 1;x<=count;x+=2) {
            this.consumer.Consume(x);
        }
    }

    [BenchFact,Benchmark]
    public void Zip_tuple() => this.Zip_tuple_Impl();
    private void Zip_tuple_Impl() {
        var length = this.CountFor(ElementCount);
        Check(1,length);
        Check(1,length);
        for(var x = 1;x<=length;x++) {
            this.consumer.Consume((First: x,Second: x));
        }
    }

    [BenchFact,Benchmark]
    public void ThenBy_keySelector() => this.ThenBy_keySelector_Impl();
    private void ThenBy_keySelector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // 同じ key の中は x の昇順で、それは元の順と同じ。
        for(var key = 0;key<1000;key++) {
            for(var x = FirstWithKey(key);x<=count;x+=1000) {
                this.consumer.Consume(x);
            }
        }
    }

    [BenchFact,Benchmark]
    public void ThenByDescending_keySelector() => this.ThenByDescending_keySelector_Impl();
    private void ThenByDescending_keySelector_Impl() {
        var count = this.CountFor(ElementCount);
        Check(1,count);
        // 同じ key の中は x の降順。
        for(var key = 0;key<1000;key++) {
            var first = FirstWithKey(key);
            if(first>count) {
                continue;
            }
            for(var x = first+(count-first)/1000*1000;x>=first;x-=1000) {
                this.consumer.Consume(x);
            }
        }
    }
}
