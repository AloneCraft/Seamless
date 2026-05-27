namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class Scalar(int 番号):Fragment(番号){
    // スカラー断片は内部に子 enumerator を持たないことも多いので既定は何もしない。
    protected virtual string GetEnumeratorDisposeBody => string.Empty;
    // bool/int/T など単一結果を返す enumerator struct を共通生成する。
    protected string DefineScalarEnumeratorStruct(string resultType) => DefineEnumeratorStructCore(resultType,GetEnumeratorDisposeBody);
}
