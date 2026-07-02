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
}
