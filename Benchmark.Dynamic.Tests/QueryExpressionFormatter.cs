using Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Tests;

public sealed class QueryExpressionFormatter{
    [Fact,Trait("Category","Unit")]
    public void Format(){
        {
            var query=Generate(
                [QueryOperatorIds.SelectIndexed,QueryOperatorIds.Count],
                enableBinary:false,
                enableNested:false);
            Assert.Equal(
                "source.Select((p,i)=>p^i).Count()",
                global::Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query));
        }
        {
            var query=Generate(
                [QueryOperatorIds.UnionByIdentity,QueryOperatorIds.Count],
                enableBinary:true,
                enableNested:false);
            Assert.Equal(
                "source.UnionBy(inner,p=>p).Count()",
                global::Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query));
        }
        {
            var query=Generate(
                [QueryOperatorIds.GroupJoinXorCount,QueryOperatorIds.ToArray],
                enableBinary:false,
                enableNested:true);
            Assert.Equal(
                "source.GroupJoin(inner,p=>(p%8+8)%8,i=>(i%8+8)%8,(p,g)=>p^g.Count()).ToArray()",
                global::Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(query));
        }
        {
            var query=Generate(
                [QueryOperatorIds.SelectIndexed,QueryOperatorIds.Count],
                enableBinary:false,
                enableNested:false);
            var invalid=query with{RequestedOperatorCount=3};
            Assert.Throws<QueryIrValidationException>(
                ()=>global::Benchmark.Dynamic.Core.QueryExpressionFormatter.Format(invalid));
        }
    }

    private static QueryIrDocument Generate(
        IReadOnlyList<string> operatorIDs,
        bool enableBinary,
        bool enableNested){
        return new RandomQueryGenerator().Generate(new RandomQueryGenerationOptions{
            MasterSeed=Seed256.Parse("0xF04A7"),
            QueryIndex=0,
            QueryId="formatter-q000000",
            SourceTarget=SourceTarget.SystemArray,
            OperatorCount=2,
            MaximumDepth=2,
            InputElementCount=8,
            EnableBinaryOperators=enableBinary,
            EnableNestedOperators=enableNested,
            AllowedOperatorIds=operatorIDs,
        });
    }
}
