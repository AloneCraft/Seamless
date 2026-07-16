using System.Reflection;

using Seamless.Linq;

namespace Test.System.Linq;

public sealed class ValueEnumerableApiTests {
    [Fact, Trait("Category","Unit")]
    public void PublicApi_CoversSystemLinqOperatorNamesWithoutDirectISetOverloads() {
        var seamlessMethods=typeof(global::Seamless.Linq.Enumerable).GetMethods(BindingFlags.Public|BindingFlags.Static);
        var seamlessNames=seamlessMethods.Select(static method => method.Name).ToHashSet(StringComparer.Ordinal);
        var missingNames=typeof(global::System.Linq.Enumerable)
            .GetMethods(BindingFlags.Public|BindingFlags.Static)
            .Select(static method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .Where(name => name!="AsEnumerable"&&!seamlessNames.Contains(name))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missingNames);
        Assert.DoesNotContain(seamlessMethods,static method =>
            method.GetParameters().FirstOrDefault()?.ParameterType is { IsGenericType: true } sourceType
            &&sourceType.GetGenericTypeDefinition() is { } definition
            &&(definition==typeof(global::System.Collections.Generic.ISet<>)
                ||definition==typeof(global::Seamless.Collections.ISet<>)));

        var providerSourceDefinitions=seamlessMethods
            .Where(static method => method.Name==nameof(global::Seamless.Linq.Enumerable.AsValueEnumerable))
            .Select(static method => method.GetParameters().Single().ParameterType)
            .Where(static type => type.IsGenericType)
            .Select(static type => type.GetGenericTypeDefinition())
            .Where(static type =>
                type==typeof(global::Seamless.Persisted.Collections.Set<>)
                ||type==typeof(global::Seamless.Tsavorite.Collections.HashSet<>)
                ||type==typeof(global::Seamless.Tsavorite.Collections.Set<,>))
            .ToArray();

        Assert.Equal(3,providerSourceDefinitions.Length);
    }

    [Fact, Trait("Category","Unit")]
    public void ArrayPipeline_MatchesSystemLinqAndDoesNotAllocateForCount() {
        var source=global::System.Linq.Enumerable.Range(0,128).ToArray();
        var expected=global::System.Linq.Enumerable.Count(
            global::System.Linq.Enumerable.Select(
                global::System.Linq.Enumerable.Where(source,static value => (value&1)==0),
                static value => value*3),
            static value => value%5!=0);
        var valueSource=source.AsValueEnumerable();

        Assert.Equal(expected,valueSource.Where(static value => (value&1)==0).Select(static value => value*3).Count(static value => value%5!=0));
        Assert.Throws<InvalidOperationException>(static () => Array.Empty<int>().AsValueEnumerable().First());

        Func<int,bool> predicate=static value => (value&1)==0;
        _=valueSource.Where(predicate).Count();
        var before=GC.GetAllocatedBytesForCurrentThread();
        var actual=valueSource.Where(predicate).Count();
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;

        Assert.Equal(64,actual);
        Assert.Equal(0,allocated);
    }

    [Fact, Trait("Category","Unit")]
    public void ProviderAdapters_ReturnExpectedResults() {
        var persistedPath=CreatePersistedPath();
        try {
            using(var persisted=global::Seamless.Persisted.Collections.Store.CreateSet<int>(persistedPath,InitialCapacity: 1,InitialDataCapacity: 128)) {
                foreach(var value in new[] { 1,2,3,4 })
                    persisted.Add(value);
                persisted.Compact();
                Assert.Equal(2,persisted.AsValueEnumerable().Where(static value => (value&1)==0).Count());
                Assert.True(persisted.AsValueEnumerable().Contains(3));
                Assert.True(persisted.AsValueEnumerable().Contains(3,EqualityComparer<int>.Default));
            }

            using(var hashStore=Store.Create().CreateHashSet<int>())
            using(var hashSet=hashStore.Create()) {
                foreach(var value in new[] { 1,2,3,4 })
                    hashSet.Add(value);
                Assert.Equal(2,hashSet.AsValueEnumerable().Where(static value => value>2).Count());
                Assert.True(hashSet.AsValueEnumerable().Contains(2));
            }

            using(var setStore=Store.Create().CreateSet<int,Row>())
            using(var set=setStore.Create()) {
                set.Add(new(1,10));
                set.Add(new(2,20));
                set.Add(new(3,30));
                Assert.Equal(2,set.AsValueEnumerable().Where(static row => row.Value>=20).Count());
                Assert.True(set.AsValueEnumerable().Contains(new Row(2,20)));
            }
        } finally {
            TryDeletePersistedDirectory(persistedPath);
        }
    }

    private static string CreatePersistedPath() {
        var directoryPath=TestPath.Directory<ValueEnumerableApiTests>("provider-value-enumerable");
        return Path.Combine(directoryPath,"items.dat");
    }

    private static void TryDeletePersistedDirectory(string path) {
        var directoryPath=Path.GetDirectoryName(path);
        if(string.IsNullOrEmpty(directoryPath))
            return;
        try {
            Directory.Delete(directoryPath,recursive: true);
        } catch(IOException) {
        } catch(UnauthorizedAccessException) {
        }
    }

    public readonly record struct Row(int Key,int Value):global::Seamless.Collections.IRecord<int>;
}
