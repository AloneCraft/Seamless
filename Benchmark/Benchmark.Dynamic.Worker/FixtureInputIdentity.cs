using Benchmark.Dynamic.Core;
using Benchmark.Infrastructure;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Benchmark.Dynamic.Worker;

/// <summary>シードで代用せず、実際の入力値と型を識別する。</summary>
public static class FixtureInputIdentity{
    private static readonly JsonSerializerOptions JSON=new(){IncludeFields=true,NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};
    public static string Compute(SourceFixture fixture,QueryElementType elementType){
        object primary=fixture.PrimaryValues,secondary=fixture.SecondaryValues;
        if(elementType!=QueryElementType.Int){
            primary=((IEnumerable)fixture.Primary).Cast<object?>().ToArray();
            secondary=((IEnumerable)fixture.Secondary).Cast<object?>().ToArray();
        }
        var json=JsonSerializer.Serialize(new{Version=2,ElementType=elementType,Primary=primary,Secondary=secondary},JSON);
        return Fingerprint.Of(Fingerprint.CanonicalJSON(json));
    }
}
