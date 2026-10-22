using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

[Collection(DynamicWorkerTestCollection.Name)]
public sealed class DynamicRouteShapeTests{
    [Fact,Trait("Category","Integration")]
    public async Task 全SourceTargetを期待経路でコンパイルし実行できる(){
        var seed=Seed256.Parse("0xD1A0");
        var runner=new DynamicWorkerExecutionRunner();
        var failures=new List<string>();
        var targets=Enum.GetValues<SourceTarget>();
        for(var sourceOrdinal=0;sourceOrdinal<targets.Length;sourceOrdinal++){
            var target=targets[sourceOrdinal];
            var query=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
                MasterSeed=seed,
                QueryIndex=0,
                QueryId=$"route-{target}",
                SourceTarget=target,
                OperatorCount=1,
                MaximumDepth=1,
                InputElementCount=16,
                EnableBinaryOperators=false,
                EnableNestedOperators=false,
                AllowedOperatorIds=[QueryOperatorIds.Count],
            });
            var request=new WorkerRunRequest(
                $"route-{target}",
                seed,
                1,
                1,
                1,
                16,
                OperatorSetMode.Maximum,
                [target],
                new WorkerMeasurementProfile("Route-v1",0,1,0,1),
                RandomQueryGenerator.GeneratorVersion,
                QueryCapabilityProfile.CurrentProfileVersion.ToString(),
                BuiltInOperatorCatalog.OperatorSetVersion.ToString());

            var result=await runner.ExecuteAsync(
                request,
                query,
                0,
                target,
                sourceOrdinal,
                CancellationToken.None);

            if(result.Status!=WorkerExecutionStatus.Succeeded||!result.AssemblyUnloaded){
                failures.Add(
                    $"{target}: Status={result.Status}, ALC={result.AssemblyUnloaded}{Environment.NewLine}"+
                    $"{result.DiagnosticsJson}{Environment.NewLine}{result.SourceText}");
            }
        }

        Assert.True(failures.Count==0,string.Join(Environment.NewLine+Environment.NewLine,failures));
    }

    [Fact,Trait("Category","Integration")]
    public async Task Containsの実経路とCapabilityが一致する(){
        var failures=new List<string>();
        foreach(var target in Enum.GetValues<SourceTarget>()){
            var result=await ExecuteAsync(target,1,[QueryOperatorIds.Contains]);
            var expected=QueryCapabilityProfiles.Get(target).SupportsOnAllPaths(QueryOperatorIds.Contains);
            var actual=result.Status==WorkerExecutionStatus.Succeeded;
            if(actual!=expected||!result.AssemblyUnloaded){
                failures.Add($"{target}: expected={expected}{Environment.NewLine}{FormatFailure(target,result)}");
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine+Environment.NewLine,failures));
    }

    [Fact,Trait("Category","Integration")]
    public async Task SelectManyの実経路とCapabilityが一致する(){
        var failures=new List<string>();
        foreach(var target in Enum.GetValues<SourceTarget>()){
            var result=await ExecuteAsync(
                target,
                2,
                [QueryOperatorIds.SelectManyXor,QueryOperatorIds.Count],
                enableNested:true);
            var expected=QueryCapabilityProfiles.Get(target).SupportsOnAllPaths(QueryOperatorIds.SelectManyXor);
            var actual=result.Status==WorkerExecutionStatus.Succeeded;
            if(actual!=expected||!result.AssemblyUnloaded){
                failures.Add($"{target}: expected={expected}{Environment.NewLine}{FormatFailure(target,result)}");
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine+Environment.NewLine,failures));
    }

    [Theory,Trait("Category","Integration")]
    [InlineData(QueryOperatorIds.Concat,false,true)]
    [InlineData(QueryOperatorIds.Except,false,true)]
    [InlineData(QueryOperatorIds.Intersect,false,true)]
    [InlineData(QueryOperatorIds.UnionByIdentity,false,true)]
    [InlineData(QueryOperatorIds.ExceptByIdentity,false,true)]
    [InlineData(QueryOperatorIds.IntersectByIdentity,false,true)]
    [InlineData(QueryOperatorIds.JoinXor,true,false)]
    [InlineData(QueryOperatorIds.GroupJoinXorCount,true,false)]
    public async Task 二項およびJoinの実経路とCapabilityが一致する(
        string operatorID,
        bool enableNested,
        bool enableBinary){
        var failures=new List<string>();
        foreach(var target in Enum.GetValues<SourceTarget>()){
            var result=await ExecuteAsync(
                target,
                2,
                [operatorID,QueryOperatorIds.Count],
                enableNested,
                enableBinary);
            var expected=QueryCapabilityProfiles.Get(target).SupportsOnAllPaths(operatorID);
            var actual=result.Status==WorkerExecutionStatus.Succeeded;
            if(actual!=expected||!result.AssemblyUnloaded){
                failures.Add($"{target}: expected={expected}{Environment.NewLine}{FormatFailure(target,result)}");
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine+Environment.NewLine,failures));
    }

    [Theory,Trait("Category","Integration")]
    [InlineData(QueryOperatorIds.Skip)]
    [InlineData(QueryOperatorIds.Reverse)]
    [InlineData(QueryOperatorIds.WhereIndexed)]
    [InlineData(QueryOperatorIds.SelectIndexed)]
    public async Task 位置依存演算子の実経路とCapabilityが一致する(string operatorID){
        var failures=new List<string>();
        foreach(var target in Enum.GetValues<SourceTarget>()){
            var expected=QueryCapabilityProfiles.Get(target).SupportsOnAllPaths(operatorID);
            if(!expected){
                continue;
            }
            var result=await ExecuteAsync(target,2,[operatorID,QueryOperatorIds.Count]);
            var actual=result.Status==WorkerExecutionStatus.Succeeded;
            if(expected&&!actual||!result.AssemblyUnloaded){
                failures.Add($"{target}: expected={expected}{Environment.NewLine}{FormatFailure(target,result)}");
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine+Environment.NewLine,failures));
    }

    [Theory,Trait("Category","Integration")]
    [InlineData(QueryOperatorIds.First)]
    [InlineData(QueryOperatorIds.FirstOrDefault)]
    [InlineData(QueryOperatorIds.Last)]
    [InlineData(QueryOperatorIds.LastOrDefault)]
    [InlineData(QueryOperatorIds.ElementAt)]
    [InlineData(QueryOperatorIds.ElementAtOrDefault)]
    public async Task 位置依存Terminalの実経路とCapabilityが一致する(string operatorID){
        var failures=new List<string>();
        foreach(var target in Enum.GetValues<SourceTarget>()){
            var expected=QueryCapabilityProfiles.Get(target).SupportsOnAllPaths(operatorID);
            if(!expected){
                continue;
            }
            var result=await ExecuteAsync(target,1,[operatorID]);
            if(result.Status!=WorkerExecutionStatus.Succeeded||!result.AssemblyUnloaded){
                failures.Add($"{target}: expected={expected}{Environment.NewLine}{FormatFailure(target,result)}");
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine+Environment.NewLine,failures));
    }

    [Fact,Trait("Category","Integration")]
    public async Task ToHashSetの実経路とCapabilityが一致する(){
        var failures=new List<string>();
        foreach(var target in Enum.GetValues<SourceTarget>()){
            var result=await ExecuteAsync(target,1,[QueryOperatorIds.ToHashSet]);
            var expected=QueryCapabilityProfiles.Get(target).SupportsOnAllPaths(QueryOperatorIds.ToHashSet);
            var actual=result.Status==WorkerExecutionStatus.Succeeded;
            if(actual!=expected||!result.AssemblyUnloaded){
                failures.Add($"{target}: expected={expected}{Environment.NewLine}{FormatFailure(target,result)}");
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine+Environment.NewLine,failures));
    }

    [Fact,Trait("Category","Integration")]
    public async Task Sumはlongでoverflowせず実経路とCapabilityが一致する(){
        var failures=new List<string>();
        foreach(var target in Enum.GetValues<SourceTarget>()){
            var result=await ExecuteAsync(target,1,[QueryOperatorIds.Sum]);
            var expected=QueryCapabilityProfiles.Get(target).SupportsOnAllPaths(QueryOperatorIds.Sum);
            var actual=result.Status==WorkerExecutionStatus.Succeeded;
            if(actual!=expected||!result.AssemblyUnloaded){
                failures.Add($"{target}: expected={expected}{Environment.NewLine}{FormatFailure(target,result)}");
            }
            if(actual&&result.ResultJson?.Contains("\"valueType\":\"long\"",StringComparison.Ordinal)!=true){
                failures.Add($"{target}: Sum result was not materialized as Int64.{Environment.NewLine}{result.ResultJson}");
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine+Environment.NewLine,failures));
    }

    [Theory,Trait("Category","Integration")]
    [InlineData(QueryOperatorIds.MinBy)]
    [InlineData(QueryOperatorIds.MaxBy)]
    [InlineData(QueryOperatorIds.ToDictionary)]
    [InlineData(QueryOperatorIds.ToLookup)]
    public async Task ByTerminalと辞書系Materializerの実経路とCapabilityが一致する(string operatorID){
        var failures=new List<string>();
        foreach(var target in Enum.GetValues<SourceTarget>()){
            var result=await ExecuteAsync(target,1,[operatorID]);
            var expected=QueryCapabilityProfiles.Get(target).SupportsOnAllPaths(operatorID);
            var actual=result.Status==WorkerExecutionStatus.Succeeded;
            if(actual!=expected||!result.AssemblyUnloaded){
                failures.Add($"{target}: expected={expected}{Environment.NewLine}{FormatFailure(target,result)}");
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine+Environment.NewLine,failures));
    }

    [Theory,Trait("Category","Integration")]
    [InlineData(QueryOperatorIds.DistinctBy,2)]
    [InlineData(QueryOperatorIds.AggregateXor,1)]
    [InlineData(QueryOperatorIds.ToIntPair,1)]
    [InlineData(QueryOperatorIds.ToIntGroup,1)]
    public async Task By系とAggregateの実経路とCapabilityが一致する(string operatorID,int operatorCount){
        var failures=new List<string>();
        foreach(var target in Enum.GetValues<SourceTarget>()){
            var allowed=operatorCount==1
                ?new[]{operatorID}
                :new[]{operatorID,QueryOperatorIds.Count};
            var result=await ExecuteAsync(target,operatorCount,allowed);
            var expected=QueryCapabilityProfiles.Get(target).SupportsOnAllPaths(operatorID);
            var actual=result.Status==WorkerExecutionStatus.Succeeded;
            if(actual!=expected||!result.AssemblyUnloaded){
                failures.Add($"{target}: expected={expected}{Environment.NewLine}{FormatFailure(target,result)}");
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine+Environment.NewLine,failures));
    }

    [Fact,Trait("Category","Integration")]
    public async Task OrderBy単独の実行経路とCapabilityが一致する(){
        var failures=new List<string>();
        foreach(var target in Enum.GetValues<SourceTarget>()){
            var query=CreateOrderQuery(target,false);
            var result=await ExecuteAsync(target,query,2);
            var expected=QueryCapabilityProfiles.Get(target).SupportsOnAllPaths(QueryOperatorIds.OrderBy);
            var actual=result.Status==WorkerExecutionStatus.Succeeded;
            if(expected&&!actual||!result.AssemblyUnloaded){
                failures.Add($"{target}: expected={expected}{Environment.NewLine}{FormatFailure(target,result)}");
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine+Environment.NewLine,failures));
    }

    [Fact,Trait("Category","Integration")]
    public async Task OrderByからThenByの実経路がCapabilityの保証範囲を満たす(){
        var failures=new List<string>();
        foreach(var target in Enum.GetValues<SourceTarget>()){
            var query=CreateOrderQuery(target,true);
            var result=await ExecuteAsync(target,query,3);
            var expected=QueryCapabilityProfiles.Get(target).SupportsOnAllPaths(QueryOperatorIds.OrderBy)
                &&QueryCapabilityProfiles.Get(target).SupportsOnAllPaths(QueryOperatorIds.ThenBy);
            var actual=result.Status==WorkerExecutionStatus.Succeeded;
            if(expected&&!actual||!result.AssemblyUnloaded){
                failures.Add($"{target}: expected={expected}{Environment.NewLine}{FormatFailure(target,result)}");
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine+Environment.NewLine,failures));
    }

    [Theory,Trait("Category","Integration")]
    [InlineData(QueryOperatorIds.TakeLast)]
    [InlineData(QueryOperatorIds.SkipLast)]
    [InlineData(QueryOperatorIds.TakeWhile)]
    [InlineData(QueryOperatorIds.SkipWhile)]
    [InlineData(QueryOperatorIds.Append)]
    [InlineData(QueryOperatorIds.Prepend)]
    [InlineData(QueryOperatorIds.DefaultIfEmpty)]
    [InlineData(QueryOperatorIds.GroupBySum)]
    [InlineData(QueryOperatorIds.GroupByCount)]
    [InlineData(QueryOperatorIds.GroupByKeyOnly)]
    public async Task 追加した単項演算子はCapabilityの保証範囲で4経路が一致して実行できる(string operatorID){
        var failures=new List<string>();
        foreach(var target in Enum.GetValues<SourceTarget>()){
            if(!QueryCapabilityProfiles.Get(target).SupportsOnAllPaths(operatorID)){
                continue;
            }
            for(var queryIndex=0;queryIndex<4;queryIndex++){
                var query=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
                    MasterSeed=Seed256.Parse("0xD1A0"),
                    QueryIndex=queryIndex,
                    QueryId=$"route-ext-{target}-{queryIndex}",
                    SourceTarget=target,
                    OperatorCount=2,
                    MaximumDepth=2,
                    InputElementCount=4,
                    EnableBinaryOperators=false,
                    EnableNestedOperators=false,
                    AllowedOperatorIds=[operatorID,QueryOperatorIds.ToArray],
                });
                var result=await ExecuteAsync(target,query,2);
                if(result.Status!=WorkerExecutionStatus.Succeeded||!result.AssemblyUnloaded){
                    failures.Add($"{operatorID} #{queryIndex}: {FormatFailure(target,result)}");
                }
            }
        }
        Assert.True(failures.Count==0,string.Join(Environment.NewLine+Environment.NewLine,failures));
    }

    private static QueryIrDocument CreateOrderQuery(SourceTarget target,bool includeThenBy){
        var source=new SourceNode(
            "n0000",
            target,
            SourceSlot.Primary,
            QueryCardinalityRange.Exact(4),
            target.GetInitialOrder());
        QueryNode input=new UnaryQueryNode(
            "n0001",
            QueryOperatorIds.OrderBy,
            source,
            new OrderByOperatorArguments(false),
            QueryValueType.Int,
            source.Cardinality,
            QueryOrderSemantics.TotalOrdered);
        if(includeThenBy){
            input=new UnaryQueryNode(
                "n0002",
                QueryOperatorIds.ThenBy,
                input,
                new ThenByOperatorArguments(false),
                QueryValueType.Int,
                source.Cardinality,
                QueryOrderSemantics.TotalOrdered);
        }
        var operatorCount=includeThenBy?3:2;
        return new QueryIrDocument(
            QueryIrDocument.CurrentSchemaVersion,
            $"route-order-{target}-{operatorCount}",
            Seed256.Parse("0xD1A0"),
            operatorCount,
            operatorCount,
            target,
            new TerminalQueryNode(
                $"n{operatorCount:D4}",
                QueryOperatorIds.ToArray,
                input,
                new TerminalOperatorArguments(QueryTerminalKind.ToArray),
                QueryValueType.Int,
                QueryCardinality.Sequence,
                source.Cardinality,
                QueryOrderSemantics.TotalOrdered,
                QueryEmptySafety.Safe));
    }

    private static async Task<WorkerSourceExecutionCompletedEvent> ExecuteAsync(
        SourceTarget target,
        int operatorCount,
        IReadOnlyCollection<string> allowedOperatorIds,
        bool enableNested=false,
        bool enableBinary=false){
        var seed=Seed256.Parse("0xD1A0");
        var query=new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=seed,
            QueryIndex=0,
            QueryId=$"route-{target}-{operatorCount}",
            SourceTarget=target,
            OperatorCount=operatorCount,
            MaximumDepth=operatorCount,
            InputElementCount=4,
            EnableBinaryOperators=enableBinary,
            EnableNestedOperators=enableNested,
            AllowedOperatorIds=allowedOperatorIds,
        });
        return await ExecuteAsync(target,query,operatorCount);
    }

    private static async Task<WorkerSourceExecutionCompletedEvent> ExecuteAsync(
        SourceTarget target,
        QueryIrDocument query,
        int operatorCount){
        var seed=Seed256.Parse("0xD1A0");
        var request=new WorkerRunRequest(
            $"route-{target}-{operatorCount}",
            seed,
            1,
            operatorCount,
            operatorCount,
            4,
            OperatorSetMode.Maximum,
            [target],
            new WorkerMeasurementProfile("Route-v1",0,1,0,1),
            RandomQueryGenerator.GeneratorVersion,
            QueryCapabilityProfile.CurrentProfileVersion.ToString(),
            BuiltInOperatorCatalog.OperatorSetVersion.ToString());
        return await new DynamicWorkerExecutionRunner().ExecuteAsync(
            request,
            query,
            0,
            target,
            0,
            CancellationToken.None);
    }

    private static string FormatFailure(
        SourceTarget target,
        WorkerSourceExecutionCompletedEvent result){
        return $"{target}: Status={result.Status}, ALC={result.AssemblyUnloaded}{Environment.NewLine}"+
            $"{result.DiagnosticsJson}{Environment.NewLine}{result.ResultJson}{Environment.NewLine}{result.SourceText}";
    }
}
