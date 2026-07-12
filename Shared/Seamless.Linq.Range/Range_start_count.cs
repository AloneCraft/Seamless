namespace Seamless.Linq.Shared;

/// <summary>2個のLINQ演算子、または生成rootと1個の演算子を組み合わせるベンチマークです。</summary>
public abstract class Range_start_count:global::Common.Base<int>{
    private const int ElementCount=4_096;

    protected Range_start_count(int count):base(count){
    }

    private int CountFor(int count)=>this.EffectiveSourceCount(count);

    [BenchFact,Benchmark]public void Consume()=>this.Consume_Impl();
    [LinqIntercept(true)]private void Consume_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Consume(this.consumer);

    [BenchFact,Benchmark]public void Aggregate_func()=>this.Aggregate_func_Impl();
    [LinqIntercept(true)]private void Aggregate_func_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Aggregate(0L,static (a,x)=>a+x));
    [BenchFact,Benchmark]public void All_predicate()=>this.All_predicate_Impl();
    [LinqIntercept(true)]private void All_predicate_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).All(static x=>x>0));
    [BenchFact,Benchmark]public void Any_predicate()=>this.Any_predicate_Impl();
    [LinqIntercept(true)]private void Any_predicate_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Any(static x=>x>=2_048));
    [BenchFact,Benchmark]public void Append_element()=>this.Append_element_Impl();
    [LinqIntercept(true)]private void Append_element_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Append(0).Consume(this.consumer);
    [BenchFact,Benchmark]public void Average()=>this.Average_Impl();
    [LinqIntercept(true)]private void Average_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Average());
    [BenchFact,Benchmark]public void Chunk_size()=>this.Chunk_size_Impl();
    [LinqIntercept(true)]private void Chunk_size_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Chunk(128).Consume(this.consumer);
    [BenchFact,Benchmark]public void Concat_second()=>this.Concat_second_Impl();
    [LinqIntercept(true)]private void Concat_second_Impl(){var length=this.CountFor(ElementCount);RangeX.Create(1,length).Concat(RangeX.Create(length/2+1,length/2)).Consume(this.consumer);}
    [BenchFact,Benchmark]public void Contains_value()=>this.Contains_value_Impl();
    [LinqIntercept(true)]private void Contains_value_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Contains(2_048));
    [BenchFact,Benchmark]public void Count()=>this.Count_Impl();
    [LinqIntercept(true)]private void Count_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Count());
    [BenchFact,Benchmark]public void Count_predicate()=>this.Count_predicate_Impl();
    [LinqIntercept(true)]private void Count_predicate_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Count(static x=>(x&1)==0));
    [BenchFact,Benchmark]public void CountBy_keySelector()=>this.CountBy_keySelector_Impl();
    [LinqIntercept(true)]private void CountBy_keySelector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).CountBy(static x=>x%1000).Consume(this.consumer);
    [BenchFact,Benchmark]public void DefaultIfEmpty()=>this.DefaultIfEmpty_Impl();
    [LinqIntercept(true)]private void DefaultIfEmpty_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).DefaultIfEmpty(0).Consume(this.consumer);
    [BenchFact,Benchmark]public void Distinct()=>this.Distinct_Impl();
    [LinqIntercept(true)]private void Distinct_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Distinct().Consume(this.consumer);
    [BenchFact,Benchmark]public void DistinctBy_keySelector()=>this.DistinctBy_keySelector_Impl();
    [LinqIntercept(true)]private void DistinctBy_keySelector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).DistinctBy(static x=>x%1000).Consume(this.consumer);
    [BenchFact,Benchmark]public void ElementAt_index()=>this.ElementAt_index_Impl();
    [LinqIntercept(true)]private void ElementAt_index_Impl(){var count=this.CountFor(ElementCount);var index=count/2;this.consumer.Consume(RangeX.Create(1,count).ElementAt(index));}
    [BenchFact,Benchmark]public void Except_second()=>this.Except_second_Impl();
    [LinqIntercept(true)]private void Except_second_Impl(){var length=this.CountFor(ElementCount);RangeX.Create(1,length).Except(RangeX.Create(length/2+1,length/2)).Consume(this.consumer);}
    [BenchFact,Benchmark]public void First_predicate()=>this.First_predicate_Impl();
    [LinqIntercept(true)]private void First_predicate_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).First(static x=>x>=1));
    [BenchFact,Benchmark]public void GroupBy_keySelector()=>this.GroupBy_keySelector_Impl();
    [LinqIntercept(true)]private void GroupBy_keySelector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).GroupBy(static x=>x%1000).Consume(this.consumer);
    [BenchFact,Benchmark]public void Index()=>this.Index_Impl();
    [LinqIntercept(true)]private void Index_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Index().Consume(this.consumer);
    [BenchFact,Benchmark]public void Intersect_second()=>this.Intersect_second_Impl();
    [LinqIntercept(true)]private void Intersect_second_Impl(){var length=this.CountFor(ElementCount);RangeX.Create(1,length).Intersect(RangeX.Create(length/2+1,length/2)).Consume(this.consumer);}
    [BenchFact,Benchmark]public void Join_inner()=>this.Join_inner_Impl();
    [LinqIntercept(true)]private void Join_inner_Impl(){var length=this.CountFor(ElementCount);RangeX.Create(1,length).Join(RangeX.Create(length/2+1,length/2),static x=>x,static x=>x,static (a,b)=>a+b).Consume(this.consumer);}
    [BenchFact,Benchmark]public void Last_predicate()=>this.Last_predicate_Impl();
    [LinqIntercept(true)]private void Last_predicate_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Last(static x=>x>=1));
    [BenchFact,Benchmark]public void Max()=>this.Max_Impl();
    [LinqIntercept(true)]private void Max_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Max());
    [BenchFact,Benchmark]public void Min()=>this.Min_Impl();
    [LinqIntercept(true)]private void Min_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Min());
    [BenchFact,Benchmark]public void OrderBy_keySelector()=>this.OrderBy_keySelector_Impl();
    [LinqIntercept(true)]private void OrderBy_keySelector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).OrderBy(static x=>x%1000).Consume(this.consumer);
    [BenchFact,Benchmark]public void Prepend_element()=>this.Prepend_element_Impl();
    [LinqIntercept(true)]private void Prepend_element_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Prepend(0).Consume(this.consumer);
    [BenchFact,Benchmark]public void Reverse()=>this.Reverse_Impl();
    [LinqIntercept(true)]private void Reverse_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Reverse().Consume(this.consumer);
    [BenchFact,Benchmark]public void Select_selector()=>this.Select_selector_Impl();
    [LinqIntercept(true)]private void Select_selector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Select(static x=>x*2).Consume(this.consumer);
    [BenchFact,Benchmark]public void SequenceEqual_second()=>this.SequenceEqual_second_Impl();
    [LinqIntercept(true)]private void SequenceEqual_second_Impl(){var length=this.CountFor(ElementCount);this.consumer.Consume(RangeX.Create(1,length).SequenceEqual(RangeX.Create(1,length)));}
    [BenchFact,Benchmark]public void Skip_count()=>this.Skip_count_Impl();
    [LinqIntercept(true)]private void Skip_count_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Skip(1_024).Consume(this.consumer);
    [BenchFact,Benchmark]public void SkipWhile_predicate()=>this.SkipWhile_predicate_Impl();
    [LinqIntercept(true)]private void SkipWhile_predicate_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).SkipWhile(static x=>x<1_024).Consume(this.consumer);
    [BenchFact,Benchmark]public void Sum()=>this.Sum_Impl();
    [LinqIntercept(true)]private void Sum_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Sum());
    [BenchFact,Benchmark]public void Take_count()=>this.Take_count_Impl();
    [LinqIntercept(true)]private void Take_count_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Take(3_072).Consume(this.consumer);
    [BenchFact,Benchmark]public void TakeWhile_predicate()=>this.TakeWhile_predicate_Impl();
    [LinqIntercept(true)]private void TakeWhile_predicate_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).TakeWhile(static x=>x<=3_072).Consume(this.consumer);
    [BenchFact,Benchmark]public void ToArray()=>this.ToArray_Impl();
    [LinqIntercept(true)]private void ToArray_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).ToArray().Consume(this.consumer);
    [BenchFact,Benchmark]public void ToDictionary_keySelector()=>this.ToDictionary_keySelector_Impl();
    [LinqIntercept(true)]private void ToDictionary_keySelector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).ToDictionary(static x=>x).Consume(this.consumer);
    [BenchFact,Benchmark]public void ToHashSet()=>this.ToHashSet_Impl();
    [LinqIntercept(true)]private void ToHashSet_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).ToHashSet().Consume(this.consumer);
    [BenchFact,Benchmark]public void ToList()=>this.ToList_Impl();
    [LinqIntercept(true)]private void ToList_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).ToList().Consume(this.consumer);
    [BenchFact,Benchmark]public void Union_second()=>this.Union_second_Impl();
    [LinqIntercept(true)]private void Union_second_Impl(){var length=this.CountFor(ElementCount);RangeX.Create(1,length).Union(RangeX.Create(length/2+1,length/2)).Consume(this.consumer);}
    [BenchFact,Benchmark]public void Where_predicate()=>this.Where_predicate_Impl();
    [LinqIntercept(true)]private void Where_predicate_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Where(static x=>(x&1)==0).Consume(this.consumer);
    [BenchFact,Benchmark]public void Zip_second()=>this.Zip_second_Impl();
    [LinqIntercept(true)]private void Zip_second_Impl(){var length=this.CountFor(ElementCount);RangeX.Create(1,length).Zip(RangeX.Create(1,length),static (a,b)=>a+b).Consume(this.consumer);}

    [BenchFact,Benchmark]public void Aggregate_seed_func_resultSelector()=>this.Aggregate_seed_func_resultSelector_Impl();
    [LinqIntercept(true)]private void Aggregate_seed_func_resultSelector_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Aggregate(0L,static (a,x)=>a+x,static a=>(double)a/1_000_000));
    [BenchFact,Benchmark]public void Any()=>this.Any_Impl();
    [LinqIntercept(true)]private void Any_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Any());
    [BenchFact,Benchmark]public void Average_selector()=>this.Average_selector_Impl();
    [LinqIntercept(true)]private void Average_selector_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Average(static x=>(double)x));
    [BenchFact,Benchmark]public void Cast()=>this.Cast_Impl();
    [LinqIntercept(true)]private void Cast_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Cast<int>().Consume(this.consumer);
    [BenchFact,Benchmark]public void ElementAtOrDefault_index()=>this.ElementAtOrDefault_index_Impl();
    [LinqIntercept(true)]private void ElementAtOrDefault_index_Impl(){var length=this.CountFor(ElementCount);this.consumer.Consume(RangeX.Create(1,length).ElementAtOrDefault(length/2));}
    [BenchFact,Benchmark]public void ExceptBy_keySelector()=>this.ExceptBy_keySelector_Impl();
    [LinqIntercept(true)]private void ExceptBy_keySelector_Impl(){var length=this.CountFor(ElementCount);RangeX.Create(1,length).ExceptBy(RangeX.Create(length/2+1,length/2),static x=>x).Consume(this.consumer);}
    [BenchFact,Benchmark]public void First()=>this.First_Impl();
    [LinqIntercept(true)]private void First_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).First());
    [BenchFact,Benchmark]public void FirstOrDefault()=>this.FirstOrDefault_Impl();
    [LinqIntercept(true)]private void FirstOrDefault_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).FirstOrDefault());
    [BenchFact,Benchmark]public void FirstOrDefault_predicate()=>this.FirstOrDefault_predicate_Impl();
    [LinqIntercept(true)]private void FirstOrDefault_predicate_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).FirstOrDefault(static x=>x>=1));
    [BenchFact,Benchmark]public void GroupBy_elementSelector()=>this.GroupBy_elementSelector_Impl();
    [LinqIntercept(true)]private void GroupBy_elementSelector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).GroupBy(static x=>x%1000,static x=>x*2).Consume(this.consumer);
    [BenchFact,Benchmark]public void GroupBy_resultSelector()=>this.GroupBy_resultSelector_Impl();
    [LinqIntercept(true)]private void GroupBy_resultSelector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).GroupBy(static x=>x%1000,static (key,g)=>key).Consume(this.consumer);
    [BenchFact,Benchmark]public void GroupJoin_inner()=>this.GroupJoin_inner_Impl();
    [LinqIntercept(true)]private void GroupJoin_inner_Impl(){var length=this.CountFor(ElementCount);RangeX.Create(1,length).GroupJoin(RangeX.Create(length/2+1,length/2),static x=>x,static x=>x,static (outer,inner)=>outer).Consume(this.consumer);}
    [BenchFact,Benchmark]public void IntersectBy_keySelector()=>this.IntersectBy_keySelector_Impl();
    [LinqIntercept(true)]private void IntersectBy_keySelector_Impl(){var length=this.CountFor(ElementCount);RangeX.Create(1,length).IntersectBy(RangeX.Create(length/2+1,length/2),static x=>x).Consume(this.consumer);}
    [BenchFact,Benchmark]public void Last()=>this.Last_Impl();
    [LinqIntercept(true)]private void Last_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Last());
    [BenchFact,Benchmark]public void LastOrDefault()=>this.LastOrDefault_Impl();
    [LinqIntercept(true)]private void LastOrDefault_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).LastOrDefault());
    [BenchFact,Benchmark]public void LongCount()=>this.LongCount_Impl();
    [LinqIntercept(true)]private void LongCount_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).LongCount());
    [BenchFact,Benchmark]public void LongCount_predicate()=>this.LongCount_predicate_Impl();
    [LinqIntercept(true)]private void LongCount_predicate_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).LongCount(static x=>(x&1)==0));
    [BenchFact,Benchmark]public void Max_selector()=>this.Max_selector_Impl();
    [LinqIntercept(true)]private void Max_selector_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Max(static x=>x*2));
    [BenchFact,Benchmark]public void MaxBy_keySelector()=>this.MaxBy_keySelector_Impl();
    [LinqIntercept(true)]private void MaxBy_keySelector_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).MaxBy(static x=>x%1000));
    [BenchFact,Benchmark]public void Min_selector()=>this.Min_selector_Impl();
    [LinqIntercept(true)]private void Min_selector_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Min(static x=>x*2));
    [BenchFact,Benchmark]public void MinBy_keySelector()=>this.MinBy_keySelector_Impl();
    [LinqIntercept(true)]private void MinBy_keySelector_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).MinBy(static x=>x%1000));
    [BenchFact,Benchmark]public void OfType()=>this.OfType_Impl();
    [LinqIntercept(true)]private void OfType_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).OfType<int>().Consume(this.consumer);
    [BenchFact,Benchmark]public void Order()=>this.Order_Impl();
    [LinqIntercept(true)]private void Order_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Order().Consume(this.consumer);
    [BenchFact,Benchmark]public void OrderByDescending_keySelector()=>this.OrderByDescending_keySelector_Impl();
    [LinqIntercept(true)]private void OrderByDescending_keySelector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).OrderByDescending(static x=>x%1000).Consume(this.consumer);
    [BenchFact,Benchmark]public void OrderDescending()=>this.OrderDescending_Impl();
    [LinqIntercept(true)]private void OrderDescending_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).OrderDescending().Consume(this.consumer);
    [BenchFact,Benchmark]public void Select_indexSelector()=>this.Select_indexSelector_Impl();
    [LinqIntercept(true)]private void Select_indexSelector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Select(static (x,i)=>x+i).Consume(this.consumer);
    [BenchFact,Benchmark]public void SkipLast_count()=>this.SkipLast_count_Impl();
    [LinqIntercept(true)]private void SkipLast_count_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).SkipLast(1_024).Consume(this.consumer);
    [BenchFact,Benchmark]public void SkipWhile_indexPredicate()=>this.SkipWhile_indexPredicate_Impl();
    [LinqIntercept(true)]private void SkipWhile_indexPredicate_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).SkipWhile(static (x,i)=>i<1_024).Consume(this.consumer);
    [BenchFact,Benchmark]public void Sum_selector()=>this.Sum_selector_Impl();
    [LinqIntercept(true)]private void Sum_selector_Impl()=>this.consumer.Consume(RangeX.Create(1,this.CountFor(ElementCount)).Sum(static x=>(long)x));
    [BenchFact,Benchmark]public void TakeLast_count()=>this.TakeLast_count_Impl();
    [LinqIntercept(true)]private void TakeLast_count_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).TakeLast(1_024).Consume(this.consumer);
    [BenchFact,Benchmark]public void TakeWhile_indexPredicate()=>this.TakeWhile_indexPredicate_Impl();
    [LinqIntercept(true)]private void TakeWhile_indexPredicate_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).TakeWhile(static (x,i)=>i<3_072).Consume(this.consumer);
    [BenchFact,Benchmark]public void ToDictionary_elementSelector()=>this.ToDictionary_elementSelector_Impl();
    [LinqIntercept(true)]private void ToDictionary_elementSelector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).ToDictionary(static x=>x,static x=>x*2).Consume(this.consumer);
    [BenchFact,Benchmark]public void ToLookup_keySelector()=>this.ToLookup_keySelector_Impl();
    [LinqIntercept(true)]private void ToLookup_keySelector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).ToLookup(static x=>x%1000).Consume(this.consumer);
    [BenchFact,Benchmark]public void ToLookup_elementSelector()=>this.ToLookup_elementSelector_Impl();
    [LinqIntercept(true)]private void ToLookup_elementSelector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).ToLookup(static x=>x%1000,static x=>x*2).Consume(this.consumer);
    [BenchFact,Benchmark]public void UnionBy_keySelector()=>this.UnionBy_keySelector_Impl();
    [LinqIntercept(true)]private void UnionBy_keySelector_Impl(){var length=this.CountFor(ElementCount);RangeX.Create(1,length).UnionBy(RangeX.Create(length/2+1,length/2),static x=>x).Consume(this.consumer);}
    [BenchFact,Benchmark]public void Where_indexPredicate()=>this.Where_indexPredicate_Impl();
    [LinqIntercept(true)]private void Where_indexPredicate_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).Where(static (x,i)=>(i&1)==0).Consume(this.consumer);
    [BenchFact,Benchmark]public void Zip_tuple()=>this.Zip_tuple_Impl();
    [LinqIntercept(true)]private void Zip_tuple_Impl(){var length=this.CountFor(ElementCount);RangeX.Create(1,length).Zip(RangeX.Create(1,length)).Consume(this.consumer);}

    public sealed class OrderBy_keySelector_chain:global::Common.Base<int>{
        private const int ElementCount=4_096;
        public OrderBy_keySelector_chain():base(ElementCount){
        }
        private int CountFor(int count)=>this.EffectiveSourceCount(count);
        [BenchFact,Benchmark]public void ThenBy_keySelector()=>this.ThenBy_keySelector_Impl();
        [LinqIntercept(true)]private void ThenBy_keySelector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).OrderBy(static x=>x%1000).ThenBy(static x=>x).Consume(this.consumer);
        [BenchFact,Benchmark]public void ThenByDescending_keySelector()=>this.ThenByDescending_keySelector_Impl();
        [LinqIntercept(true)]private void ThenByDescending_keySelector_Impl()=>RangeX.Create(1,this.CountFor(ElementCount)).OrderBy(static x=>x%1000).ThenByDescending(static x=>x).Consume(this.consumer);
    }
}
