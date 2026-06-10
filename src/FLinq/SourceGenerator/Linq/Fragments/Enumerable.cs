namespace Tsavorite.SourceGenerator.Linq.Fragments;
public abstract class Enumerable(int 番号):Fragment(番号){
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

}
