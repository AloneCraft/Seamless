namespace Tsavorite.SourceGenerator.Linq.Fragments;
public abstract class Enumerable(int 番号):Fragment(番号),ConsumeDirectExecutionFragment{
    // この enumerable 断片が流す要素型。
    public abstract string ElementType{ get; }
    // この enumerable の enumerator が TryGetSpan で連続メモリの Span を返せるか。
    // ソース生成時に確定する（ArrayRoot/ListRoot のみ true）。span fast path を分岐なしで生成するために使う。
    public virtual bool CanProvideSpan => false;
    // interceptor が IEnumerable<T> を返すときに使う ValueEnumerable 構築式。
    public virtual string GetEnumerableExpression => $"new global::Tsavorite.Carrier.ValueEnumerable<{this.EnumeratorTypeName}, {this.ElementType}>({this.GetEnumeratorExpression})";
    // 既定では下流の enumerator を Dispose する。
    protected virtual string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
    // enumerable 系は要素型を Current とする enumerator struct を共通生成する。
    public override string GetEnumeratorStruct => this.DefineEnumeratorStructCore(this.ElementType,this.GetEnumeratorDisposeBody);

    // expr.Consume(consumer) 文に対する直接実行コード片。
    // この enumerable を列挙し各要素を consumer へ渡す。各 LINQ 演算子の意味は自分の enumerator が表現するため、
    // interceptor 側に演算子別の特殊処理を置かずここで一様に展開できる（コード片は Fragment が持つ原則）。
    // TraversalSupport.AppendTraversalLoop が span 可能ソース（ArrayRoot/ListRoot）では TryGetSpan→for(span)、
    // それ以外は while(TryGetNext) を生成するため span fast path は維持される。
    // span 直走査やキー集合の事前構築など演算子固有の最適化が必要な断片は本メソッドを override する（例: Distinct/Set 系）。
    public virtual string GetConsumeExecutionStatement(string consumerName){
        var builder=new StringBuilder();
        var enumeratorName=$"__ce{番号}";
        var valueName=$"__cv{番号}";
        builder.AppendLine($"using var {enumeratorName}={this.GetEnumeratorExpression};");
        TraversalSupport.AppendTraversalLoop(builder,enumeratorName,this.ElementType,valueName,$"{consumerName}.Consume({valueName});");
        return builder.ToString().TrimEnd();
    }
}
