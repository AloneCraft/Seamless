using Seamless.FLinq;

using System.Reflection;

using SeamlessEnumerable = Seamless.Linq.Enumerable;

namespace Test.Seamless.Tsavorite.Linq.Enumerable;

[LinqIntercept(false)]
public class EnumerableForwarderTests {
    [Fact, Trait("Category","Unit")]
    public void GeneratedEnumerable_ExposesAllApplicableSystemEnumerableOverloads() {
        var expected = GetSystemApplicableSignatures().Order(StringComparer.Ordinal).ToArray();
        var actual = GetBackendSignatures(typeof(SeamlessEnumerable)).Order(StringComparer.Ordinal).ToArray();

        Assert.Empty(expected.Except(actual,StringComparer.Ordinal));
    }

    [Fact, Trait("Category","Unit")]
    public void GeneratedEnumerable_RepresentativeResults_MatchSystemEnumerable() {
        using var store = Store.Create().CreateHashSet<int>();
        using var source = store.Create();
        foreach(var value in new[] { 1,2,3,4 })
            source.Add(value);

        var orderedSource = SeamlessEnumerable.AsEnumerable(source).Order().ToArray();
        var filtered = SeamlessEnumerable.Where(source,static value => value%2==0);

        Assert.IsAssignableFrom<global::Seamless.Collections.IUnorderedEnumerable<int>>(filtered);
#pragma warning disable CS0618 // 順序なし集合への禁止APIが例外を投げる契約を検証する。
        Assert.Throws<NotSupportedException>(() => SeamlessEnumerable.First(source));
#pragma warning restore CS0618
        Assert.Equal(1,SeamlessEnumerable.ToArray(source).Order().First());

        Assert.Equal(orderedSource.Where(static value => value%2==0).ToArray(),
            SeamlessEnumerable.Where(source,static value => value%2==0).Order().ToArray());
        Assert.Equal(orderedSource.Select(static value => value*10).ToArray(),
            SeamlessEnumerable.Select(source,static value => value*10).Order().ToArray());
        Assert.Equal(orderedSource.Count(static value => value>2),
            SeamlessEnumerable.Count(source,static value => value>2));
        Assert.Equal(orderedSource.Any(static value => value==3),
            SeamlessEnumerable.Any(source,static value => value==3));
        Assert.Equal(orderedSource.Sum(),
            SeamlessEnumerable.Sum(source));
        Assert.Equal(orderedSource.Union([3,4,5]).Order().ToArray(),
            SeamlessEnumerable.Union(source,[3,4,5]).Order().ToArray());
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
            replaceFirstParameter ? FormatExpectedReturnType(method) : FormatType(method.ReturnType),
            string.Join(",",parameters));
    }

    private static string FormatExpectedReturnType(MethodInfo method) {
        if(method.Name!="AsEnumerable"
            &&!IsRestricted(method)
            &&method.ReturnType.IsGenericType
            &&method.ReturnType.GetGenericTypeDefinition()==typeof(IEnumerable<>)) {
            return $"Seamless.Collections.IUnorderedEnumerable<{FormatType(method.ReturnType.GetGenericArguments()[0])}>";
        }
        return FormatType(method.ReturnType);
    }

    private static bool IsRestricted(MethodInfo method) {
        var name = method.Name;
        if(name.StartsWith("Order",StringComparison.Ordinal)
            ||name.StartsWith("ThenBy",StringComparison.Ordinal)
            ||name.StartsWith("First",StringComparison.Ordinal)
            ||name.StartsWith("Last",StringComparison.Ordinal)
            ||name.StartsWith("ElementAt",StringComparison.Ordinal)
            ||name.StartsWith("Skip",StringComparison.Ordinal)
            ||name.StartsWith("Take",StringComparison.Ordinal))
            return true;
        if(name is "Reverse" or "Shuffle" or "Index" or "Zip" or "SequenceEqual" or "Chunk" or "Append" or "Prepend" or "Concat")
            return true;
        return (name is "Where" or "Select" or "SelectMany")
            &&method.GetParameters().Skip(1).Any(static parameter => parameter.ParameterType.GetMethod("Invoke") is { } invoke
                &&invoke.GetParameters() is { Length:>=2 } invokeParameters
                &&invokeParameters[1].ParameterType==typeof(int));
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
