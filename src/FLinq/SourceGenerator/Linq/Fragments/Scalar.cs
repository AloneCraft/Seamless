namespace Tsavorite.SourceGenerator.Linq.Fragments;
public abstract class Scalar(int 番号):Fragment(番号){
    // スカラー断片は内部に子 enumerator を持たないことも多いので既定は何もしない。
    protected virtual string GetEnumeratorDisposeBody => string.Empty;
    // bool/int/T など単一結果を返す enumerator struct を共通生成する。
    protected string DefineScalarEnumeratorStruct(string resultType) => this.DefineEnumeratorStructCore(resultType,this.GetEnumeratorDisposeBody);

    // スカラー終端は呼び出し側で 1 回だけ TryGetNext される。単一要素 enumerator struct（Struct{番号}）を
    // 生成すると __yd0 ガード・Current・到達不能な IValueEnumerator 各メンバ等が丸ごと死にコードになる。
    // これを避けるため、結果を destVar へ直接書き込むインライン文を返せる断片はこれを override する。
    // 戻り値 null（既定）なら従来の Struct{番号}+TryGetNext 経路へフォールバックする。
    public virtual string? RenderInlineComputation(string destVar,string destType) => null;
    // RenderInlineComputation がインライン展開を返すとき、補助クラスへ積む補助メンバ群。
    // Struct{番号}（単一要素 enumerator）を含まない点が GetEnumeratorStruct と異なる。
    public virtual string InlineSupportingMembers => this.GetEnumeratorStruct;
}
