namespace Seamless.Linq;

public static class GeneratedCodeHelpers {
    /// <summary>
    /// 生成インターセプターが展開に失敗したときの明示例外。
    /// 静かなフォールバック経路を作らない方針のため、構築時に実行時診断カウンタも加算する。
    /// </summary>
    public static InvalidOperationException LinqInterceptorFailure(string message) {
        LinqCounterScope.Counter.IncrementEnumerableFallback(message);
        return new(message);
    }
    public static InvalidOperationException UnsupportedLinqExpansion(string methodName,string reason) {
        var message=$"FLinq could not expand '{methodName}'. {reason}";
        LinqCounterScope.Counter.IncrementEnumerableFallback(message);
        return new(message);
    }
    public static InvalidOperationException NoElements() =>
        new("Sequence contains no elements");
    public static NotSupportedException UnorderedSourceOperation(string methodName,string sourceType)=>
        new($"'{methodName}' requires sequence order and is not supported directly on unordered source '{sourceType}'. Cast to IEnumerable<T>, call AsEnumerable(), or materialize to Array/List to opt in.");
    [global::System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static T ThrowUnorderedSourceOperation<T>(string methodName,string sourceType)=>
        throw UnorderedSourceOperation(methodName,sourceType);
}
