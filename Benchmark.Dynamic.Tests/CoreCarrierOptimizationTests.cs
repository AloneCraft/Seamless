using Test.SourceGenerator;

namespace Benchmark.Dynamic.Tests;

public sealed class CoreCarrierOptimizationTests{
    [Fact]
    public void PartitionedLookupCountsProducerInExecutionSlotLimit(){
        foreach(var slots in new[]{1,2}){
            using var scope=new global::Seamless.Execution.ExecutionScope(new(){MaximumQueryWorkers=2});
            using var lookup=new global::Seamless.Linq.PartitionedLookupBuilder<int,int>(scope,null,maxExecutionSlots:slots);
            var capacity=global::Seamless.Execution.CpuScheduler.Shared.Usage.Capacity;
            Assert.Equal(slots==2&&capacity>=2,lookup.IsParallel);
            Assert.Equal(1,lookup.WorkerCount);
            lookup.Build(target=>{
                target.Add(2,10);
                target.Add(1,20);
                target.Add(2,30);
            });
            Assert.Equal(new[]{2,1},lookup.Groups().Select(static group=>group.Key));
            Assert.Equal(new[]{10,30},lookup.Match(2));
        }
    }

    [Fact]
    public void DictionarySlimRetainsOrderWhenLateCollisionsSwitchBuckets(){
        using var dictionary=new global::Seamless.Linq.DictionarySlim<int,int>();
        var keys=Enumerable.Range(0,1024).Concat(Enumerable.Range(1,1024).Select(static key=>key<<16)).ToArray();
        for(var index=0;index<keys.Length;index++){
            dictionary.GetValueRefOrAddDefault(keys[index],out var exists)=index;
            Assert.False(exists);
        }
        Assert.Equal(keys.Length,dictionary.Count);
        for(var index=0;index<keys.Length;index++){
            dictionary.GetKeyAndValue(index,out var key,out var value);
            Assert.Equal(keys[index],key);
            Assert.Equal(index,value);
            Assert.Equal(index,dictionary.GetValueRefOrNullRef(key));
        }
        Assert.True(System.Runtime.CompilerServices.Unsafe.IsNullRef(ref dictionary.GetValueRefOrNullRef(int.MinValue)));
    }

    [Fact]
    public void LookupDistributesKeysWithIdenticalLowBits(){
        const int size=1024;
        var comparer=new CountingShiftedKeyComparer();
        using var builder=new global::Seamless.Linq.LookupBuilder<int,int>(0,comparer,true);
        for(var index=0;index<size;index++){
            builder.Add(index<<16,index);
        }
        builder.Add(0,-1);
        using var lookup=builder.BuildPooledAndClear();
        Assert.Equal(Enumerable.Range(0,size).Select(static index=>index<<16),lookup.Select(static group=>group.Key));
        Assert.Equal(new[]{0,-1},lookup[0]);
        for(var index=1;index<size;index++){
            Assert.Equal(index,Assert.Single(lookup[index<<16]));
        }
        Assert.Empty(lookup[int.MinValue]);
        Assert.True(comparer.EqualityCalls<size*16,$"Equality calls: {comparer.EqualityCalls}");
    }

    private sealed class CountingShiftedKeyComparer:IEqualityComparer<int>{
        public int EqualityCalls{get;private set;}
        public bool Equals(int left,int right){this.EqualityCalls++;return left==right;}
        public int GetHashCode(int value)=>value;
    }

    [Fact]
    public void RangeExceptCountsMergeOverlapsAfterValidatingInSourceOrder(){
        const string source="""
            using System;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Container{
                private string trace="";
                private int Start(int value){this.trace+=value.ToString()+",";return value;}
                [LinqIntercept(true)]
                private int Count(int size,int a,int b,int middleCount)=>Enumerable.Range(0,size)
                    .Except(Enumerable.Range(this.Start(a),4)).Except(Enumerable.Range(this.Start(b),middleCount))
                    .Except(Enumerable.Range(this.Start(a),4)).Count();
                [LinqIntercept(true)]
                private static long LongCount(int start,int count,int b,int bc)=>Enumerable.Range(start,count).Except(Enumerable.Range(b,bc)).LongCount();
                public static string Run(){
                    var item=new Container();
                    var count=item.Count(10,6,3,5);
                    var longCount=LongCount(1,int.MaxValue,int.MaxValue,1);
                    var outside=LongCount(3,4,-3,2);
                    string? error=null;
                    try{item.Count(10,6,3,-1);}
                    catch(ArgumentOutOfRangeException ex){error=ex.ParamName;}
                    return $"{count}|{longCount}|{outside}|{error}|{item.trace}";
                }
            }
            """;
        var (compilation,generated)=ExecutableGeneration.Generate(source,"RangeExceptCounts",4);
        Assert.Contains(".CountDifference()",generated);
        var result=ExecutableGeneration.Run(compilation,"Sample.Container",container=>container.GetMethod("Run")!.Invoke(null,null));
        Assert.Equal("3|2147483646|4|count|6,3,6,6,3,",result);
    }

    [Fact]
    public void RangeIntersectionCountsPreserveValidationAndBounds(){
        const string source="""
            using System;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Container{
                private int calls;
                private int Right(int value){this.calls++;return value;}
                [LinqIntercept(true)]
                private int Count(int a,int ac,int b,int bc)=>Enumerable.Range(a,ac).Intersect(Enumerable.Range(this.Right(b),bc)).Count();
                [LinqIntercept(true)]
                private long LongCount(int a,int ac,int b,int bc)=>Enumerable.Range(a,ac).Intersect(Enumerable.Range(this.Right(b),bc)).LongCount();
                public static string Run(){
                    var item=new Container();
                    var overlap=item.Count(3,5,1,5);
                    var disjoint=item.Count(3,2,-2,3);
                    var maximum=item.LongCount(1,int.MaxValue,1,int.MaxValue);
                    string? left=null,right=null;
                    try{item.Count(0,-1,1,1);}
                    catch(ArgumentOutOfRangeException ex){left=ex.ParamName;}
                    try{item.LongCount(0,1,int.MaxValue,2);}
                    catch(ArgumentOutOfRangeException ex){right=ex.ParamName;}
                    return $"{overlap}|{disjoint}|{maximum}|{left}|{right}|{item.calls}";
                }
            }
            """;
        var (compilation,generated)=ExecutableGeneration.Generate(source,"RangeIntersectionCounts",5);
        Assert.Contains(".CountIntersection(",generated);
        var result=ExecutableGeneration.Run(compilation,"Sample.Container",container=>container.GetMethod("Run")!.Invoke(null,null));
        Assert.Equal("3|0|2147483647|count|count|4",result);
    }

    [Fact]
    public void RangeUnionCountsValidateInOrderAndCheckOverflow(){
        const string source="""
            using System;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Container{
                private int calls;
                private int Right(int value){this.calls++;return value;}
                [LinqIntercept(true)]
                private int Count(int a,int ac,int b,int bc)=>Enumerable.Range(a,ac).Union(Enumerable.Range(this.Right(b),bc)).Count();
                [LinqIntercept(true)]
                private long LongCount(int a,int ac,int b,int bc)=>Enumerable.Range(a,ac).Union(Enumerable.Range(this.Right(b),bc)).LongCount();
                public static string Run(){
                    var item=new Container();
                    var overlapping=item.Count(3,5,1,5);
                    var empty=item.Count(3,0,1,5);
                    var wide=item.LongCount(int.MinValue,int.MaxValue,0,int.MaxValue);
                    var limit=item.Count(0,int.MaxValue,1,1);
                    var overflow=false;
                    try{item.Count(int.MinValue,int.MaxValue,0,int.MaxValue);}
                    catch(OverflowException){overflow=true;}
                    string? left=null,right=null;
                    try{item.Count(0,-1,1,1);}
                    catch(ArgumentOutOfRangeException ex){left=ex.ParamName;}
                    try{item.LongCount(0,1,int.MaxValue,2);}
                    catch(ArgumentOutOfRangeException ex){right=ex.ParamName;}
                    return $"{overlapping}|{empty}|{wide}|{limit}|{overflow}|{left}|{right}|{item.calls}";
                }
            }
            """;
        var (compilation,generated)=ExecutableGeneration.Generate(source,"RangeUnionCounts",7);
        Assert.Matches(@"(?:\.GetLongCount|countUnionRanges\w*)\s*\(",generated);
        var result=ExecutableGeneration.Run(compilation,"Sample.Container",container=>container.GetMethod("Run")!.Invoke(null,null));
        Assert.Equal("7|5|4294967294|2147483647|True|count|count|6",result);
    }

    [Fact]
    public void ChainedRangeUnionCountsValidateBeforeEvaluatingLaterRanges(){
        const string source="""
            using System;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Container{
                private int calls;
                private int Mark(int value){this.calls++;return value;}
                [LinqIntercept(true)]
                private int Count(int a,int ac,int b,int bc,int c,int cc)=>
                    Enumerable.Range(a,ac).Union(Enumerable.Range(this.Mark(b),bc)).Union(Enumerable.Range(this.Mark(c),cc)).Count();
                [LinqIntercept(true)]
                private long LongCount(int a,int ac,int b,int bc,int c,int cc)=>
                    Enumerable.Range(a,ac).Union(Enumerable.Range(b,bc)).Union(Enumerable.Range(c,cc)).LongCount();
                public static string Run(){
                    var item=new Container();
                    var overlap=item.Count(4,5,2,4,8,2);
                    var empty=item.Count(5,0,-3,2,9,2);
                    var wide=item.LongCount(int.MinValue,int.MaxValue,-1,int.MaxValue,int.MaxValue-1,2);
                    var overflow=false;
                    try{item.Count(int.MinValue,int.MaxValue,0,int.MaxValue,int.MaxValue,1);}
                    catch(OverflowException){overflow=true;}
                    var before=item.calls;
                    string? invalid=null;
                    try{item.Count(0,1,int.MaxValue,2,1,1);}
                    catch(ArgumentOutOfRangeException ex){invalid=ex.ParamName;}
                    return $"{overlap}|{empty}|{wide}|{overflow}|{invalid}|{item.calls-before}";
                }
            }
            """;
        var (compilation,generated)=ExecutableGeneration.Generate(source,"ChainedRangeUnionCounts",2);
        Assert.Matches(@"countUnionRanges\w*\s*\(",generated);
        var result=ExecutableGeneration.Run(compilation,"Sample.Container",container=>container.GetMethod("Run")!.Invoke(null,null));
        Assert.Equal("8|4|4294967296|True|count|1",result);
    }

    [Fact]
    public void WithOrderHintsPreserveUnionAndJoinResultsWhenNoUnorderedPlanApplies(){
        const string source="""
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Linq;
            namespace Sample;
            public sealed class Container{
                [LinqIntercept(true)]
                private static int[] Union(int[] left,int[] right)=>
                    left.Union(right).WithOrder(QueryOrder.Preserve).ToArray();
                [LinqIntercept(true)]
                private static int[] Join(int[] left,int[] right)=>
                    left.Join(right,static x=>x,static x=>x,static (x,y)=>x+y)
                        .WithOrder(QueryOrder.AllowUnordered).ToArray();
                public static string Run()=>
                    string.Join(",",Union([3,1,3],[1,2,4]))+"|"+string.Join(",",Join([3,1],[1,3,3]));
            }
            """;
        var(compilation,generated)=ExecutableGeneration.Generate(source,"WithOrderHints",2);
        Assert.Contains("WithOrder(Preserve)",generated);
        Assert.Contains("WithOrder(AllowUnordered)",generated);
        var result=ExecutableGeneration.Run(compilation,"Sample.Container",container=>container.GetMethod("Run")!.Invoke(null,null));
        Assert.Equal("3,1,2,4|6,6,2",result);
    }

    [Fact]
    public void InvalidOrderHintsProduceExpansionWarnings(){
        const string source="""
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Linq;
            namespace Sample;
            public sealed class Container{
                [LinqIntercept(true)]
                private static int[] WrongPosition(int[] left)=>
                    left.Where(static value=>value>0).WithOrder(QueryOrder.Preserve).ToArray();
                [LinqIntercept(true)]
                private static int[] Duplicate(int[] left,int[] right)=>
                    left.Union(right).WithOrder(QueryOrder.Preserve).WithOrder(QueryOrder.Auto).ToArray();
                [LinqIntercept(true)]
                private static int[] Variable(int[] left,int[] right,QueryOrder order)=>
                    left.Union(right).WithOrder(order).ToArray();
                public static int[] Run(int[] left,int[] right,QueryOrder order)=>
                    WrongPosition(left).Concat(Duplicate(left,right)).Concat(Variable(left,right,order)).ToArray();
            }
            """;
        var messages=new List<string>();
        var(_,generated)=ExecutableGeneration.Generate(source,"InvalidOrderHints",0,diagnostic=>{
            if(diagnostic.Id=="FLINQ002"){messages.Add(diagnostic.GetMessage());}
        });
        foreach(var name in new[]{"WrongPosition","Duplicate","Variable"}){
            Assert.Contains(messages,message=>message.Contains(name,StringComparison.Ordinal));
        }
        Assert.Contains("Place WithOrder immediately after Join or Union",generated);
        Assert.Contains("Use one WithOrder hint per query chain",generated);
        Assert.Contains("WithOrder requires a constant QueryOrder value",generated);
    }

    [Fact]
    public void ParallelUnionAcceptsBothHintOrdersAndKeepsOneOfEachValue(){
        const string source="""
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Linq;
            namespace Sample;
            public sealed class Container{
                [LinqIntercept(true)]
                private static int[] First(int[] left,int[] right,int slots)=>
                    left.Union(right).WithOrder(QueryOrder.AllowUnordered).Parallel(slots).ToArray();
                [LinqIntercept(true)]
                private static int[] Second(int[] left,int[] right)=>
                    left.Union(right).Parallel(4).WithOrder(QueryOrder.AllowUnordered).ToArray();
                [LinqIntercept(true)]
                private static int[] WithComparer(int[] left,int[] right,System.Collections.Generic.IEqualityComparer<int> comparer)=>
                    left.Union(right,comparer).Parallel(4).WithOrder(QueryOrder.AllowUnordered).ToArray();
                [LinqIntercept(true)]
                private static int FirstRow(int[] left,int[] right)=>
                    left.Union(right).WithOrder(QueryOrder.AllowUnordered).Parallel(4).First();
                [LinqIntercept(true)]
                private static int[] TakeRows(int[] left,int[] right)=>
                    left.Union(right).Parallel(4).WithOrder(QueryOrder.AllowUnordered).Take(2).ToArray();
                private sealed class RemainderComparer:System.Collections.Generic.IEqualityComparer<int>{
                    public bool Equals(int x,int y)=>x%10==y%10;
                    public int GetHashCode(int value)=>value%10;
                }
                public static bool Run(){
                    int[] left=[3,1,3],right=[1,2,4];
                    int[] expected=[1,2,3,4];
                    return First(left,right,4).OrderBy(static x=>x).SequenceEqual(expected)
                        &&Second(left,right).OrderBy(static x=>x).SequenceEqual(expected)
                        &&First(left,right,1).SequenceEqual(new[]{3,1,2,4})
                        &&FirstRow(left,right)==3&&TakeRows(left,right).SequenceEqual(new[]{3,1})
                        &&WithComparer([11,21,2],[31,12,3],new RemainderComparer()).SequenceEqual(new[]{11,2,3});
                }
            }
            """;
        var(compilation,generated)=ExecutableGeneration.Generate(source,"ParallelUnionHints",2);
        Assert.Contains("ParallelUnionToArray",generated);
        Assert.True(Assert.IsType<bool>(ExecutableGeneration.Run(compilation,"Sample.Container",container=>container.GetMethod("Run")!.Invoke(null,null))));
    }

    [Fact]
    public void SmallSideJoinReversesProbeOrderOnlyWhenLeftIsSmall(){
        const string source="""
            using System.Linq;
            using Seamless.FLinq;
            using Seamless.Linq;
            namespace Sample;
            public sealed class Container{
                [LinqIntercept(true)]
                private static (int,int)[] Join(int[] left,int[] right)=>
                    left.Join(right,static x=>x,static x=>x,static (x,y)=>(x,y))
                        .WithOrder(QueryOrder.AllowUnordered).ToArray();
                [LinqIntercept(true)]
                private static (int,int)[] OrderedFirst(int[] left,int[] right,int slots)=>
                    left.Join(right,static x=>x,static x=>x,static (x,y)=>(x,y))
                        .WithOrder(QueryOrder.AllowUnordered).Parallel(slots).ToArray();
                [LinqIntercept(true)]
                private static (int,int)[] OrderedSecond(int[] left,int[] right)=>
                    left.Join(right,static x=>x,static x=>x,static (x,y)=>(x,y))
                        .Parallel(4).WithOrder(QueryOrder.Preserve).Take(2).ToArray();
                public static bool Run(){
                    int[] left=[2,1,2],large=[1,2,2,3,4,5,6,7,8,9,10,11],small=[1,2];
                    (int,int)[] reversed=[(1,1),(2,2),(2,2),(2,2),(2,2)];
                    (int,int)[] preserved=[(2,2),(1,1),(2,2)];
                    (int,int)[] largePreserved=[(2,2),(2,2),(1,1),(2,2),(2,2)];
                    return Join(left,large).SequenceEqual(reversed)&&Join(left,small).SequenceEqual(preserved)
                        &&OrderedFirst(left,large,4).SequenceEqual(largePreserved)
                        &&OrderedSecond(left,large).SequenceEqual(new[]{(2,2),(2,2)})
                        &&Join([],large).Length==0;
                }
            }
            """;
        var(compilation,generated)=ExecutableGeneration.Generate(source,"SmallSideJoin",2);
        Assert.Contains("SmallSideJoinToArray",generated);
        Assert.True(Assert.IsType<bool>(ExecutableGeneration.Run(compilation,"Sample.Container",container=>container.GetMethod("Run")!.Invoke(null,null))));
    }

    [Fact]
    public void DenseArrayGroupJoinPreservesEvaluationAndCounts(){
        const string source="""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Container{
                private int[] outer=[-1,2,16];
                private readonly List<int> trace=new();
                private int InnerKey(int value){this.trace.Add(value);this.outer[0]=99;return value;}
                private int OuterKey(int value){this.trace.Add(100+value);return value;}
                [LinqIntercept(true)]
                private int[] Counts(int[] outer,int[] inner)=>outer.GroupJoin(inner,thisValue=>this.OuterKey(thisValue),value=>this.InnerKey(value)%8,
                    static (value,group)=>value*100+group.Count()).ToArray();
                [LinqIntercept(true)]
                private static long[] LongCounts(int[] outer,int[] inner)=>outer.GroupJoin(inner,static value=>value,static value=>value%8,
                    static (value,group)=>group.LongCount()).ToArray();
                [LinqIntercept(true)]
                private static int[] PredicateCounts(int[] outer,int[] inner)=>outer.GroupJoin(inner,static value=>value,static value=>value%8,
                    static (value,group)=>group.Count(item=>item<0)).ToArray();
                public static string Run(){
                    var item=new Container();
                    var inner=new[]{-1,2,-9,10};
                    var empty=item.Counts(Array.Empty<int>(),inner);
                    var count=item.Counts(item.outer,inner);
                    var longCount=LongCounts(new[]{-1,2,99},inner);
                    var predicate=PredicateCounts(new[]{-1,2,99},inner);
                    string? parameter=null;
                    try{item.Counts(Array.Empty<int>(),null!);}
                    catch(ArgumentNullException exception){parameter=exception.ParamName;}
                    return $"{empty.Length}|{string.Join(",",count)}|{string.Join(",",item.trace)}|{string.Join(",",longCount)}|{string.Join(",",predicate)}|{parameter}";
                }
            }
            """;
        var (compilation,generated)=ExecutableGeneration.Generate(source,"DenseArrayGroupJoin",4);
        var result=ExecutableGeneration.Run(compilation,"Sample.Container",container=>container.GetMethod("Run")!.Invoke(null,null));
        Assert.Equal("0|9900,202,1600|-1,2,-9,10,199,102,116|2,2,0|2,0,0|inner",result);
        // 配列の GroupJoin は、件数だけを読む集計を整数キーの範囲(x%8 なら -7..7 の15個)の件数表で数える(行を格納しない)。
        Assert.Contains("stackalloc int[15]",generated);
    }

    [Fact]
    public void DenseArrayGroupJoinRecognizesMaskOnLeft(){
        const string source="""
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Container{
                private const int Mask=15;
                [LinqIntercept(true)]
                private static int[] Counts(int[] outer,int[] inner)=>outer.GroupJoin(inner,static value=>value,
                    static value=>Mask&value,static (value,group)=>group.Count()).ToArray();
                public static int[] Run()=>Counts(new[]{15,-1,0,8,15},new[]{-1,15,31,0,16});
            }
            """;
        var (compilation,generated)=ExecutableGeneration.Generate(source,"DenseArrayGroupJoinLeftMask",1);
        // 定数のマスクが左にあっても範囲(0..15)を求め、件数表で数える。
        Assert.Contains("stackalloc int[16]",generated);
        var result=ExecutableGeneration.Run(compilation,"Sample.Container",container=>
            container.GetMethod("Run")!.Invoke(null,null));
        Assert.Equal(new[]{3,0,2,0,3},Assert.IsType<int[]>(result));
    }

    [Fact]
    public void RangeIntersectAndExceptPreserveArgumentOrderAndValidation(){
        const string source="""
            using System;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Container{
                private int calls;
                private int Start()=>++this.calls;
                private int Count()=>++this.calls==1?2:-1;
                [LinqIntercept(true)]
                private int[] Intersection()=>Enumerable.Range(this.Start(),4).Intersect(Enumerable.Range(this.Start(),4)).ToArray();
                [LinqIntercept(true)]
                private int[] InvalidIntersectionLeft()=>Enumerable.Range(0,-1).Intersect(Enumerable.Range(this.Start(),1)).ToArray();
                [LinqIntercept(true)]
                private int[] InvalidIntersectionRight()=>Enumerable.Range(0,this.Count()).Intersect(Enumerable.Range(0,this.Count())).ToList().ToArray();
                [LinqIntercept(true)]
                private static int[] IntersectionBy(int left,int right)=>Enumerable.Range(left,2).IntersectBy(Enumerable.Range(right,1),static value=>value).ToArray();
                [LinqIntercept(true)]
                private int[] InvalidRight()=>Enumerable.Range(0,this.Count()).Except(Enumerable.Range(0,this.Count())).ToArray();
                [LinqIntercept(true)]
                private int[] InvalidLeft()=>Enumerable.Range(0,-1).Except(Enumerable.Range(this.Start(),1)).ToArray();
                [LinqIntercept(true)]
                private int[] Difference()=>Enumerable.Range(0,5).Except(Enumerable.Range(this.Start(),1)).Except(Enumerable.Range(this.Start(),1)).ToArray();
                private static string RunOne(Func<int[]> query,Container owner){
                    try{return $"{string.Join(",",query())}|{owner.calls}";}
                    catch(ArgumentOutOfRangeException exception){return $"{exception.ParamName}|{owner.calls}";}
                }
                public static string[] Run(){
                    var intersection=new Container();
                    var right=new Container();
                    var left=new Container();
                    var difference=new Container();
                    var intersectionLeft=new Container();
                    var intersectionRight=new Container();
                    return new[]{
                        RunOne(()=>intersection.Intersection(),intersection),
                        RunOne(()=>right.InvalidRight(),right),
                        RunOne(()=>left.InvalidLeft(),left),
                        RunOne(()=>difference.Difference(),difference),
                        RunOne(()=>intersectionLeft.InvalidIntersectionLeft(),intersectionLeft),
                        RunOne(()=>intersectionRight.InvalidIntersectionRight(),intersectionRight),
                        string.Join(",",IntersectionBy(int.MaxValue-1,int.MaxValue)),
                        string.Join(",",IntersectionBy(int.MaxValue-1,int.MinValue))
                    };
                }
            }
            """;
        var (compilation,generated)=ExecutableGeneration.Generate(source,"RangeSetArgumentOrder",8);
        var result=ExecutableGeneration.Run(compilation,"Sample.Container",container=>(string[])container.GetMethod("Run")!.Invoke(null,null)!);
        Assert.Equal(new[]{"2,3,4|2","count|2","count|0","0,3,4|2","count|0","count|2","2147483647",""},result);
        Assert.Contains(".CreateIntersection(",generated);
    }

    [Fact]
    public void RangeUnionPreservesBothArgumentEvaluationsAndFirstOccurrenceOrder(){
        const string source="""
            using System;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public sealed class Container{
                private int calls;
                private int Start()=>10*++this.calls;
                private int Count()=>++this.calls==1?2:-1;
                [LinqIntercept(true)]
                private int[] ChangingStart()=>Enumerable.Range(this.Start(),2).Union(Enumerable.Range(this.Start(),2)).ToArray();
                [LinqIntercept(true)]
                private int[] InvalidRight()=>Enumerable.Range(0,this.Count()).Union(Enumerable.Range(0,this.Count())).ToArray();
                [LinqIntercept(true)]
                private static int[] Forward()=>Enumerable.Range(-2,4).Union(Enumerable.Range(0,4)).ToArray();
                [LinqIntercept(true)]
                private static int[] Backward()=>Enumerable.Range(0,4).Union(Enumerable.Range(-2,4)).ToArray();
                [LinqIntercept(true)]
                private int[] InvalidLeft()=>Enumerable.Range(0,-1).Union(Enumerable.Range(this.Start(),2)).ToArray();
                [LinqIntercept(true)]
                private static int[] Array(int a,int ac,int b,int bc)=>Enumerable.Range(a,ac).Union(Enumerable.Range(b,bc)).ToArray();
                [LinqIntercept(true)]
                private static System.Collections.Generic.List<int> List(int a,int ac,int b,int bc)=>
                    Enumerable.Range(a,ac).UnionBy(Enumerable.Range(b,bc),static value=>value).ToList();
                [LinqIntercept(true)]
                private int[] NamedRange()=>Enumerable.Range(count:this.Start()/10,start:this.Start()).Union(Enumerable.Range(0,2)).ToArray();
                [LinqIntercept(true)]
                private int[] NamedRangeInStaticUnion()=>Enumerable.Union(Enumerable.Range(count:this.Start()/10,start:this.Start()),Enumerable.Range(0,2)).ToArray();
                public static string Run(){
                    var first=new Container();
                    var second=new Container();
                    var values=first.ChangingStart();
                    string? error=null;
                    try{second.InvalidRight();}
                    catch(ArgumentOutOfRangeException exception){error=exception.ParamName;}
                    var invalidLeft=new Container();
                    string? leftError=null;
                    try{invalidLeft.InvalidLeft();}
                    catch(ArgumentOutOfRangeException exception){leftError=exception.ParamName;}
                    var cases=new[]{(0,0,-3,7),(0,4,5,2),(5,2,0,4),(1,2,0,4),(-2,4,0,0),
                        (int.MaxValue-1,2,int.MinValue,2),(int.MaxValue,1,int.MaxValue,1)};
                    foreach(var (a,ac,b,bc) in cases){
                        var expected=Enumerable.Range(a,ac).Union(Enumerable.Range(b,bc)).ToArray();
                        if(!expected.SequenceEqual(Array(a,ac,b,bc))||!expected.SequenceEqual(List(a,ac,b,bc))){
                            throw new InvalidOperationException($"Wrong union for ({a},{ac},{b},{bc}).");
                        }
                    }
                    var named=new Container();
                    var staticNamed=new Container();
                    return $"{string.Join(",",values)}|{first.calls}|{error}|{second.calls}|{string.Join(",",Forward())}|{string.Join(",",Backward())}|{leftError}|{invalidLeft.calls}|{string.Join(",",named.NamedRange())}|{named.calls}|{string.Join(",",staticNamed.NamedRangeInStaticUnion())}|{staticNamed.calls}";
                }
            }
            """;
        var (compilation,generated)=ExecutableGeneration.Generate(source,"RangeUnionArgumentOrder",9);
        var result=ExecutableGeneration.Run(compilation,"Sample.Container",container=>container.GetMethod("Run")!.Invoke(null,null));
        Assert.Equal("10,11,20,21|2|count|2|-2,-1,0,1,2,3|0,1,2,3,-2,-1|count|0|20,0,1|2|20,0,1|2",result);
        Assert.Contains("prefixNext",generated);
    }

    [Fact]
    public void ConsecutiveUnionValidatesTheInnerSourceBeforeEvaluatingTheOuterArgument(){
        const string source="""
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public class Container {
                [LinqIntercept(true)]
                private static int[] Query(int[] first,int[] second,List<int> trace)=>
                    first.Union(second).Union(Third(trace)).ToArray();
                [LinqIntercept(true)]
                private static int[] QueryStatic(int[] first,int[] second,List<int> trace)=>
                    Enumerable.Union(Enumerable.Union(first,second),Third(trace)).ToArray();
                [LinqIntercept(true)]
                private static int[] QueryNamed(int[] first,int[] second,List<int> trace)=>
                    Enumerable.ToArray(Enumerable.Union(second:Third(trace),first:first.Union(second)));
                private static int[] Third(List<int> trace){trace.Add(3);return new[]{3};}
                public static int[] Run(int[] first,int[] second,List<int> trace)=>Query(first,second,trace);
                public static int[] RunStatic(int[] first,int[] second,List<int> trace)=>QueryStatic(first,second,trace);
                public static int[] RunNamed(int[] first,int[] second,List<int> trace)=>QueryNamed(first,second,trace);
                public static int[] Reference(int[] first,int[] second,List<int> trace)=>
                    Enumerable.ToArray(Enumerable.Union(Enumerable.Union(first,second),Third(trace)));
                public static int[] ReferenceNamed(int[] first,int[] second,List<int> trace)=>
                    Enumerable.ToArray(Enumerable.Union(second:Third(trace),first:Enumerable.Union(first,second)));
            }
            """;
        var (compilation,_)=ExecutableGeneration.Generate(source,"ConsecutiveUnionArgumentOrder",3);
        ExecutableGeneration.Run(compilation,"Sample.Container",container=>{
            foreach(var suffix in new[]{"","Static","Named"}){
                var reference=container.GetMethod(suffix=="Named"?"ReferenceNamed":"Reference")!;
                var expectedTrace=new List<int>();
                var trace=new List<int>();
                var run=container.GetMethod("Run"+suffix)!;
                var expected=Assert.Throws<System.Reflection.TargetInvocationException>(()=>reference.Invoke(null,[new[]{1},null,expectedTrace]));
                var actual=Assert.Throws<System.Reflection.TargetInvocationException>(()=>run.Invoke(null,[new[]{1},null,trace]));
                Assert.Equal(expectedTrace,trace);
                Assert.Equal(Assert.IsType<ArgumentNullException>(expected.InnerException).ParamName,
                    Assert.IsType<ArgumentNullException>(actual.InnerException).ParamName);
                var expectedRows=(int[])reference.Invoke(null,[new[]{1,1},new[]{1,2},expectedTrace])!;
                var rows=(int[])run.Invoke(null,[new[]{1,1},new[]{1,2},trace])!;
                Assert.Equal(expectedRows,rows);
                Assert.Equal(expectedTrace,trace);
            }
            return true;
        });
    }

    [Fact]
    public void ConsecutiveUnionPreservesDistinctComparerBoundaries(){
        const string source="""
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public class Container {
                private sealed class Parity : IEqualityComparer<int>{
                    public bool Equals(int x,int y)=>(x&1)==(y&1);
                    public int GetHashCode(int value)=>value&1;
                }
                private static readonly IEqualityComparer<int> parity=new Parity();
                [LinqIntercept(true)]
                private static int[] Query(int[] first,int[] second,int[] third,bool innerParity)=>
                    first.Union(second,innerParity?parity:null).Union(third,innerParity?null:parity).ToArray();
                public static int[] Run(int[] first,int[] second,int[] third,bool innerParity)=>Query(first,second,third,innerParity);
            }
            """;
        // 比較器の境界を保つため、Union連鎖を含むクエリ本体をまとめて差し替える。
        var(compilation,_)=ExecutableGeneration.Generate(source,"ConsecutiveUnionComparers",1);
        ExecutableGeneration.Run(compilation,"Sample.Container",container=>{
            var run=container.GetMethod("Run")!;
            Assert.Equal(new[]{2,5,8,7},(int[])run.Invoke(null,[new[]{2,4},new[]{5,3},new[]{8,7},true])!);
            Assert.Equal(new[]{2,5},(int[])run.Invoke(null,[new[]{2,4},new[]{5,3},new[]{8,7},false])!);
            return true;
        });
    }

    [Fact]
    public void ConsecutiveExceptEnumeratesTheOuterRightInputFirst(){
        const string source="""
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public class Container {
                [LinqIntercept(true)]
                private static int[] Query(int[] first,int[] second,int[] third,List<int> trace)=>
                    first.Except(Observe(second,trace,2)).Except(Observe(third,trace,3)).ToArray();
                [LinqIntercept(true)]
                private static int[] QueryBy(int[] first,int[] second,int[] third,List<int> trace)=>
                    first.ExceptBy(Observe(second,trace,2),static x=>x).ExceptBy(Observe(third,trace,3),static x=>x).ToArray();
                private static IEnumerable<int> Observe(int[] rows,List<int> trace,int tag){
                    trace.Add(tag);
                    foreach(var row in rows){yield return row;}
                }
                public static int[] Run(int[] first,int[] second,int[] third,List<int> trace)=>Query(first,second,third,trace);
                public static int[] RunBy(int[] first,int[] second,int[] third,List<int> trace)=>QueryBy(first,second,third,trace);
                public static int[] Reference(int[] first,int[] second,int[] third,List<int> trace)=>
                    Enumerable.ToArray(Enumerable.Except(Enumerable.Except(first,Observe(second,trace,2)),Observe(third,trace,3)));
                public static int[] ReferenceBy(int[] first,int[] second,int[] third,List<int> trace)=>
                    Enumerable.ToArray(Enumerable.ExceptBy(Enumerable.ExceptBy(first,Observe(second,trace,2),static x=>x),Observe(third,trace,3),static x=>x));
            }
            """;
        var (compilation,_)=ExecutableGeneration.Generate(source,"ConsecutiveExceptInputOrder",2);
        ExecutableGeneration.Run(compilation,"Sample.Container",container=>{
            int[] first=[1,2,2,3,4],second=[2],third=[3];
            foreach(var suffix in new[]{"","By"}){
                var expectedTrace=new List<int>();
                var expected=(int[])container.GetMethod("Reference"+suffix)!.Invoke(null,[first,second,third,expectedTrace])!;
                var trace=new List<int>();
                var actual=(int[])container.GetMethod("Run"+suffix)!.Invoke(null,[first,second,third,trace])!;
                Assert.Equal(expected,actual);
                Assert.Equal(expectedTrace,trace);
            }
            return true;
        });
    }

    [Fact]
    public void ConsecutiveUnionByPreservesBothKeySelectorCalls(){
        const string source="""
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public class Container {
                [LinqIntercept(true)]
                private static int[] Query(int[] first,int[] second,int[] third,List<int> trace)=>
                    first.UnionBy(second,x=>Key(x,trace)).UnionBy(third,x=>Key(x,trace)).ToArray();
                private static int Key(int value,List<int> trace){trace.Add(value);return value;}
                public static int[] Run(int[] first,int[] second,int[] third,List<int> trace)=>Query(first,second,third,trace);
            }
            """;
        int[] first=[1,1,2],second=[2,3],third=[3,4];
        var expectedTrace=new List<int>();
        int Key(int value){expectedTrace.Add(value);return value;}
        var expected=first.UnionBy(second,Key).UnionBy(third,Key).ToArray();
        var (compilation,_)=ExecutableGeneration.Generate(source,"ConsecutiveUnionByCallbacks",1);
        ExecutableGeneration.Run(compilation,"Sample.Container",container=>{
            var trace=new List<int>();
            var actual=(int[])container.GetMethod("Run")!.Invoke(null,[first,second,third,trace])!;
            Assert.Equal(expected,actual);
            Assert.Equal(expectedTrace,trace);
            return true;
        });
    }

    [Theory]
    [InlineData("First")]
    [InlineData("Any")]
    [InlineData("Distinct")]
    [InlineData("PlainFirst")]
    [InlineData("PlainAny")]
    [InlineData("PlainContains")]
    public void UnionShortcutsPreserveArgumentsAndKeyCallbacks(string suffix){
        const string source="""
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public class Container {
                private static int[] Second(List<int> trace,bool missing){trace.Add(0);return missing?null!:new[]{2};}
                private static int Key(int value,List<int> trace){trace.Add(value);return value;}
                [LinqIntercept(true)]
                private static int First(int[] first,List<int> trace,bool missing)=>
                    first.Prepend(1).UnionBy(Second(trace,missing),x=>Key(x,trace)).First();
                [LinqIntercept(true)]
                private static bool Any(int[] first,List<int> trace,bool missing)=>
                    first.UnionBy(Second(trace,missing),x=>Key(x,trace)).Any();
                [LinqIntercept(true)]
                private static int[] Distinct(int[] first,List<int> trace,bool missing)=>
                    first.DistinctBy(x=>Key(x,trace)).UnionBy(Second(trace,missing),x=>Key(x,trace)).ToArray();
                [LinqIntercept(true)]
                private static int PlainFirst(int[] first,List<int> trace,bool missing)=>
                    first.Prepend(1).Union(Second(trace,missing)).First();
                [LinqIntercept(true)]
                private static bool PlainAny(int[] first,List<int> trace,bool missing)=>
                    first.Union(Second(trace,missing)).Any();
                [LinqIntercept(true)]
                private static bool PlainContains(int[] first,List<int> trace,bool missing)=>
                    first.Union(Second(trace,missing)).Contains(2);
                public static int RunFirst(int[] first,List<int> trace,bool missing)=>First(first,trace,missing);
                public static bool RunAny(int[] first,List<int> trace,bool missing)=>Any(first,trace,missing);
                public static int[] RunDistinct(int[] first,List<int> trace,bool missing)=>Distinct(first,trace,missing);
                public static int RunPlainFirst(int[] first,List<int> trace,bool missing)=>PlainFirst(first,trace,missing);
                public static bool RunPlainAny(int[] first,List<int> trace,bool missing)=>PlainAny(first,trace,missing);
                public static bool RunPlainContains(int[] first,List<int> trace,bool missing)=>PlainContains(first,trace,missing);
                public static int ReferenceFirst(int[] first,List<int> trace,bool missing)=>
                    Enumerable.First(Enumerable.UnionBy(Enumerable.Prepend(first,1),Second(trace,missing),x=>Key(x,trace)));
                public static bool ReferenceAny(int[] first,List<int> trace,bool missing)=>
                    Enumerable.Any(Enumerable.UnionBy(first,Second(trace,missing),x=>Key(x,trace)));
                public static int[] ReferenceDistinct(int[] first,List<int> trace,bool missing)=>
                    Enumerable.ToArray(Enumerable.UnionBy(Enumerable.DistinctBy(first,x=>Key(x,trace)),Second(trace,missing),x=>Key(x,trace)));
                public static int ReferencePlainFirst(int[] first,List<int> trace,bool missing)=>
                    Enumerable.First(Enumerable.Union(Enumerable.Prepend(first,1),Second(trace,missing)));
                public static bool ReferencePlainAny(int[] first,List<int> trace,bool missing)=>
                    Enumerable.Any(Enumerable.Union(first,Second(trace,missing)));
                public static bool ReferencePlainContains(int[] first,List<int> trace,bool missing)=>
                    Enumerable.Contains(Enumerable.Union(first,Second(trace,missing)),2);
            }
            """;
        var (compilation,_)=ExecutableGeneration.Generate(source,"UnionShortcuts",6);
        ExecutableGeneration.Run(compilation,"Sample.Container",container=>{
            var reference=container.GetMethod("Reference"+suffix)!;
            var run=container.GetMethod("Run"+suffix)!;
            foreach(var first in new[]{new[]{1,1,3},Array.Empty<int>()}){
                var expectedTrace=new List<int>();
                var actualTrace=new List<int>();
                var expected=reference.Invoke(null,[first,expectedTrace,false]);
                var actual=run.Invoke(null,[first,actualTrace,false]);
                if(expected is int[] rows){Assert.Equal(rows,Assert.IsType<int[]>(actual));}
                else{Assert.Equal(expected,actual);}
                Assert.Equal(expectedTrace,actualTrace);
                expectedTrace.Clear();actualTrace.Clear();
                var expectedError=Assert.Throws<System.Reflection.TargetInvocationException>(()=>reference.Invoke(null,[first,expectedTrace,true]));
                var actualError=Assert.Throws<System.Reflection.TargetInvocationException>(()=>run.Invoke(null,[first,actualTrace,true]));
                Assert.Equal(Assert.IsType<ArgumentNullException>(expectedError.InnerException).ParamName,
                    Assert.IsType<ArgumentNullException>(actualError.InnerException).ParamName);
                Assert.Equal(expectedTrace,actualTrace);
            }
            return true;
        });
    }

    [Fact]
    public void ObservableAnonymousTypeKeepsItsOriginalRepresentation(){
        const string source="""
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public class Container {
                [LinqIntercept(true)]
                private static string[] Query(int[] input)=>
                    input.Select(static value=>new { Value=value }).Select(static item=>item.ToString()).ToArray();
                public static string[] Run(int[] input)=>Query(input);
            }
            """;
        var (compilation,generated)=ExecutableGeneration.Generate(source,"ObservableAnonymousCarrier",0);
        Assert.Contains("[中間値/元の経路]",generated);
        ExecutableGeneration.Run(compilation,"Sample.Container",container=>{
            var result=(string[])container.GetMethod("Run")!.Invoke(null,[new[]{3}])!;
            Assert.Equal("{ Value = 3 }",Assert.Single(result));
            return true;
        });
    }

    [Fact]
    public void DenseJoinCountsPreserveSignedKeysAndSelectorOrder(){
        const string source="""
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public class Container {
                private static int Observe(int value,List<int> trace){trace.Add(value);return value;}
                [LinqIntercept(true)]
                private static int[] Query(int[] outer,int[] inner,List<int> trace)=>
                    outer.Join(inner,static key=>key,value=>Observe(value,trace)%32,(key,_)=>Observe(key,trace)).ToArray();
                [LinqIntercept(true)]
                private static int First(int[] outer,int[] inner,List<int> trace)=>
                    outer.Join(inner,static key=>key,value=>Observe(value,trace)%32,(key,_)=>Observe(key,trace)).FirstOrDefault();
                public static int[] Run(int[] outer,int[] inner,List<int> trace)=>Query(outer,inner,trace);
                public static int RunFirst(int[] outer,int[] inner,List<int> trace)=>First(outer,inner,trace);
                public static int[] Reference(int[] outer,int[] inner,List<int> trace)=>
                    Enumerable.ToArray(Enumerable.Join(outer,inner,static key=>key,value=>Observe(value,trace)%32,(key,_)=>Observe(key,trace)));
                public static int ReferenceFirst(int[] outer,int[] inner,List<int> trace)=>
                    Enumerable.FirstOrDefault(Enumerable.Join(outer,inner,static key=>key,value=>Observe(value,trace)%32,(key,_)=>Observe(key,trace)));
            }
            """;
        var (compilation,generated)=ExecutableGeneration.Generate(source,"DenseJoinCountsSigned",2);
        Assert.Contains("[Join/件数配列]",generated);
        ExecutableGeneration.Run(compilation,"Sample.Container",container=>{
            int[] outer=[int.MaxValue,-31,-1,0,31,int.MinValue];
            int[] inner=[-63,-33,-32,-31,-1,0,31,63];
            foreach(var input in new[]{outer,Array.Empty<int>()}){
                var expectedTrace=new List<int>();
                var actualTrace=new List<int>();
                var expected=(int[])container.GetMethod("Reference")!.Invoke(null,[input,inner,expectedTrace])!;
                var actual=(int[])container.GetMethod("Run")!.Invoke(null,[input,inner,actualTrace])!;
                Assert.Equal(expected,actual);
                Assert.Equal(expectedTrace,actualTrace);
                expectedTrace.Clear();actualTrace.Clear();
                var expectedFirst=(int)container.GetMethod("ReferenceFirst")!.Invoke(null,[input,inner,expectedTrace])!;
                var actualFirst=(int)container.GetMethod("RunFirst")!.Invoke(null,[input,inner,actualTrace])!;
                Assert.Equal(expectedFirst,actualFirst);
                Assert.Equal(expectedTrace,actualTrace);
            }
            return true;
        });
    }

    [Fact]
    public void ImplicitAnonymousDefaultKeepsItsNullSemantics(){
        const string source="""
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public class Container {
                [LinqIntercept(true)]
                private static int[] Query(int[] input)=>
                    input.Select(static value=>new { Value=value }).DefaultIfEmpty()
                         .Select(static item=>item!.Value).ToArray();
                public static int[] Run(int[] input)=>Query(input);
            }
            """;
        var (compilation,generated)=ExecutableGeneration.Generate(source,"AnonymousDefault",0);
        Assert.Contains("[中間値/元の経路]",generated);
        ExecutableGeneration.Run(compilation,"Sample.Container",container=>{
            var run=container.GetMethod("Run")!;
            Assert.Equal(new[]{3},(int[])run.Invoke(null,[new[]{3}])!);
            var exception=Assert.Throws<System.Reflection.TargetInvocationException>(()=>run.Invoke(null,[Array.Empty<int>()]));
            Assert.IsType<NullReferenceException>(exception.InnerException);
            return true;
        });
    }

    [Fact]
    public void ReturnedGroupSurvivesLookupDisposal() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public class Container {
                [LinqIntercept(true)]
                private static IEnumerable<int>[] Query(int[] outer,int[] inner) =>
                    outer.GroupJoin(inner,static x=>x,static _=>1,static (_,group)=>group).ToArray();
                public static IEnumerable<int>[] Run(int[] outer,int[] inner)=>Query(outer,inner);
            }
            """;
        var (compilation,generated) = ExecutableGeneration.Generate(source,"EscapingGroupJoin",1);
        Assert.Contains("BuildPooledAndClear",generated);
        ExecutableGeneration.Run(compilation,"Sample.Container",container => {
            int[] inner=Enumerable.Range(0,40).ToArray();
            var run=container.GetMethod("Run")!;
            var result=(IEnumerable<int>[])run.Invoke(null,[new[]{1,1,2},inner])!;
            _=run.Invoke(null,[new[]{1},Enumerable.Range(100,40).ToArray()]);
            Assert.Same(result[0],result[1]);
            Assert.Equal(inner,result[0]);
            Assert.Empty(result[2]);
            return true;
        });
    }

    [Fact]
    public void InternalAnonymousValuesUseTuplesAndKeepMemberNames() {
        const string source="""
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public class Container {
                [LinqIntercept(true)]
                private static int[] Query(int[] input) =>
                    input.Select(static value=>new { One=new { Value=value },Rest=value+1 })
                         .OrderBy(static item=>item.One.Value)
                         .Select(static item=>item.One.Value*10+item.Rest+nameof(item.Rest).Length).ToArray();
                public static int[] Run(int[] input)=>Query(input);
                [LinqIntercept(true)]
                private static int[] QueryArrays(int[] input)=>
                    input.Select(static value=>new[]{new { Value=value },new { Value=value+1 }})
                         .OrderBy(static values=>values[0].Value)
                         .Select(static values=>values[0].Value+values[1].Value).ToArray();
                public static int[] RunArrays(int[] input)=>QueryArrays(input);
            }
            """;
        var (compilation,generated)=ExecutableGeneration.Generate(source,"TupleCarrierMembers",2);
        Assert.DoesNotContain("__TsavoriteAnonRecords_",generated);
        Assert.Contains("global::System.ValueTuple",generated);
        ExecutableGeneration.Run(compilation,"Sample.Container",container=>{
            var actual=(int[])container.GetMethod("Run")!.Invoke(null,[new[]{3,1,2}])!;
            Assert.Equal(new[]{16,27,38},actual);
            var arrays=(int[])container.GetMethod("RunArrays")!.Invoke(null,[new[]{3,1,2}])!;
            Assert.Equal(new[]{3,5,7},arrays);
            return true;
        });
    }

    [Fact]
    public void WideAnonymousJoinKeyHashesTheFirstMember() {
        const string source="""
            using System.Linq;
            using Seamless.FLinq;
            namespace Sample;
            public class Container {
                [LinqIntercept(true)]
                private static int[] Query(int[] outer,int[] inner)=>outer.Join(inner,
                    static x=>new { Id=x,A=0,B=0,C=0,D=0,E=0,F=0,G=0,H=0 },
                    static x=>new { Id=x,A=0,B=0,C=0,D=0,E=0,F=0,G=0,H=0 },
                    static (left,right)=>left+right).ToArray();
                public static int[] Run(int[] outer,int[] inner)=>Query(outer,inner);
            }
            """;
        var (compilation,generated)=ExecutableGeneration.Generate(source,"WideTupleJoinKey",1);
        Assert.DoesNotContain("__TsavoriteAnonRecords_",generated);
        Assert.Contains("hash.Add(value.Id)",generated);
        ExecutableGeneration.Run(compilation,"Sample.Container",container=>{
            var actual=(int[])container.GetMethod("Run")!.Invoke(null,[new[]{3,1,2},new[]{1,3,3}])!;
            Assert.Equal(new[]{6,6,2},actual);
            return true;
        });
    }
}
