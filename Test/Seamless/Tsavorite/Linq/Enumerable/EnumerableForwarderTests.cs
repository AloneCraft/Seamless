using System.Reflection;

using Seamless.FLinq;
using TsavoriteEnumerable = Seamless.Tsavorite.Linq.Enumerable;

namespace Test.Seamless.Tsavorite.Linq.Enumerable;

[LinqIntercept(false)]
public class EnumerableForwarderTests {
    [Fact, Trait("Category","Unit")]
    public void GeneratedEnumerable_ExposesAllApplicableSystemEnumerableOverloads() {
        var expected = GetSystemApplicableSignatures().Order(StringComparer.Ordinal).ToArray();
        var actual = GetBackendSignatures(typeof(TsavoriteEnumerable)).Order(StringComparer.Ordinal).ToArray();

        Assert.Empty(expected.Except(actual,StringComparer.Ordinal));
    }

    [Fact, Trait("Category","Unit")]
    public void GeneratedEnumerable_RepresentativeResults_MatchSystemEnumerable() {
        using var store = global::Seamless.Tsavorite.Collections.Store.Create().CreateHashSet<int>();
        using var source = store.Create();
        foreach(var value in new[] { 1,2,3,4 })
            source.Add(value);

        var orderedSource = source.Order().ToArray();

        Assert.Equal(orderedSource.Where(static value => value%2==0).ToArray(),
            TsavoriteEnumerable.Where(source,static value => value%2==0).Order().ToArray());
        Assert.Equal(orderedSource.Select(static value => value*10).ToArray(),
            TsavoriteEnumerable.Select(source,static value => value*10).Order().ToArray());
        Assert.Equal(orderedSource.Count(static value => value>2),
            TsavoriteEnumerable.Count(source,static value => value>2));
        Assert.Equal(orderedSource.Any(static value => value==3),
            TsavoriteEnumerable.Any(source,static value => value==3));
        Assert.Equal(orderedSource.Sum(),
            TsavoriteEnumerable.Sum(source));
        Assert.Equal(orderedSource.Union([3,4,5]).Order().ToArray(),
            TsavoriteEnumerable.Union(source,[3,4,5]).Order().ToArray());
    }

    private static IEnumerable<string> GetSystemApplicableSignatures() =>
        typeof(global::System.Linq.Enumerable)
            .GetMethods(BindingFlags.Public|BindingFlags.Static)
            .Where(IsApplicableSystemMethod)
            .Select(CreateExpectedSignature);

    private static IEnumerable<string> GetBackendSignatures(Type backendType) =>
        backendType
            .GetMethods(BindingFlags.Public|BindingFlags.Static)
            .Where(static method => method.GetParameters().Length>0)
            .Select(CreateBackendSignature);

    private static bool IsApplicableSystemMethod(MethodInfo method) {
        var parameters = method.GetParameters();
        if(parameters.Length==0)
            return false;

        var sourceType = parameters[0].ParameterType;
        return sourceType.IsGenericType
            &&sourceType.GetGenericTypeDefinition()==typeof(IEnumerable<>);
    }

    private static string CreateExpectedSignature(MethodInfo method) =>
        CreateSignature(method,replaceFirstParameter: true);

    private static string CreateBackendSignature(MethodInfo method) =>
        CreateSignature(method,replaceFirstParameter: false);

    private static string CreateSignature(MethodInfo method,bool replaceFirstParameter) {
        var parameters = method.GetParameters()
            .Select((parameter,index) => replaceFirstParameter&&index==0
                ? FormatSetType(parameter.ParameterType)
                : FormatType(parameter.ParameterType));

        return string.Join("|",
            method.Name,
            method.IsGenericMethodDefinition ? method.GetGenericArguments().Length.ToString() : "0",
            FormatType(method.ReturnType),
            string.Join(",",parameters));
    }

    private static string FormatSetType(Type enumerableType) {
        var elementType = enumerableType.GetGenericArguments()[0];
        return $"Seamless.Collections.ISet<{FormatType(elementType)}>";
    }

    private static string FormatType(Type type) {
        if(type.IsByRef)
            return (type.IsByRefLike ? "ref " : string.Empty)+FormatType(type.GetElementType()!);
        if(type.IsGenericParameter)
            return type.Name;
        if(type.IsArray)
            return $"{FormatType(type.GetElementType()!)}[]";
        if(!type.IsGenericType)
            return type.FullName??type.Name;

        var genericTypeDefinition = type.GetGenericTypeDefinition();
        var genericTypeName = genericTypeDefinition.FullName??genericTypeDefinition.Name;
        var tickIndex = genericTypeName.IndexOf('`');
        if(tickIndex>=0)
            genericTypeName=genericTypeName[..tickIndex];

        return $"{genericTypeName}<{string.Join(",",type.GetGenericArguments().Select(FormatType))}>";
    }
}
