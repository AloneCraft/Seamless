// ReSharper disable InvalidXmlDocComment
namespace Seamless.Common
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PipelineAttribute(string Code): Attribute
    {
        public readonly string Code = Code;
    }
    /// <summary>
    /// 例,Whereの$はSelect.selectorに置換する
    /// {!a!}/{!!a!!}はSGで具体的な変数名になる。例えばa0
    /// {&a&}/{&&a&&}は変数参照を表す。{!a!}で作られた変数名を使う
    /// {*a*}/{**a**}は結果の変数を表す。これを下位のメソッドのコードに置換する。例えばWhereの結果はSelectのsourceになるからSelectのコードにWhereのコードを置換する。
    /// {--}/{--a--}は前段パイプラインの入力参照を表す。
    /// {&selector&}は変数名を決めたデリゲート参照。例えばselector0
    /// Select(static p=>p+p)だとすると
    /// struct selector0{
    ///     public static int Invoke(int p)=>p+p;
    /// }
    /// Select(p=>p+x)だとすると
    /// struct selector1(int x){
    ///     public int Invoke(int        p)=>p+x;
    /// }
    /// {#selector#}はstructメソッドを表す。例えばWhereのラムダ式はSelectのラムダ式になるからSelectのコードにWhereのラムダ式を置換する。
    /// CollectionAttribute(コレクション関数)の文字列仕様:
    /// ループ文には foreach(...) / while(...) など、ループ開始文そのものを書く。
    /// ループ前処理→ループ文→ループ内部先頭→ループ内部Add→ループ内部末尾→評価式 の順で組み立てる。
    /// 出力要素式は、後続のチェインメソッドへ渡す1要素分の式を表す。
    /// AggregateAttribute(集約関数)の文字列仕様:
    /// 集約関数はスカラ値を返す。前のチェインメソッドから受け取るループ変数はinput$にする。
    /// 前のチェインメソッドのループ先頭を利用するなら
    /// ループ前処理→前のチェインメソッドループ先頭(input$)→ループ内部→前のチェインメソッド"}"→return文
    /// 前がチェインメソッドでなかったら
    /// ループ前処理→ループ先頭(input$)→ループ内部→"}"→return文
    /// ifが1ステートメントなら{}を付けず同じ行に書く。forが1ステートメントなら{}を付けず次行にインデントして書く。
    /// </summary>
    public enum CollectionLoopKind {
        Single,
        Sequential,
        LockStep,
        Nested,
        BuildLookupThenProbe
    }
    public enum CollectionStopMode {
        None,
        Shortest,
        ExhaustFirstThenSecond,
        AllMatches
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CollectionAttribute(
        string 初期化,
        string ループ前処理0,
        string ループ文0,
        string ループ内部先頭0,
        string ループ内部Add0,
        string ループ内部末尾0,
        string ループ前処理1,
        string ループ文1,
        string ループ内部先頭1,
        string ループ内部Add1,
        string ループ内部末尾1,
        string ループ前処理2,
        string ループ文2,
        string ループ内部先頭2,
        string ループ内部Add2,
        string ループ内部末尾2,
        string 出力要素式,
        string 評価式
        ): Attribute {
        public readonly string 初期化 = 初期化;
        public readonly string ループ前処理0 = ループ前処理0;
        public readonly string ループ文0 = ループ文0;
        public readonly string ループ内部先頭0 = ループ内部先頭0;
        public readonly string ループ内部Add0 = ループ内部Add0;
        public readonly string ループ内部末尾0 = ループ内部末尾0;
        public readonly string ループ前処理1 = ループ前処理1;
        public readonly string ループ文1 = ループ文1;
        public readonly string ループ内部先頭1 = ループ内部先頭1;
        public readonly string ループ内部Add1 = ループ内部Add1;
        public readonly string ループ内部末尾1 = ループ内部末尾1;
        public readonly string ループ前処理2 = ループ前処理2;
        public readonly string ループ文2 = ループ文2;
        public readonly string ループ内部先頭2 = ループ内部先頭2;
        public readonly string ループ内部Add2 = ループ内部Add2;
        public readonly string ループ内部末尾2 = ループ内部末尾2;
        public readonly string 出力要素式 = 出力要素式;
        public readonly string 評価式 = 評価式;
        public CollectionLoopKind LoopKind { get; set; }=CollectionLoopKind.Single;
        public string[] Sources { get; set; }=[];
        public string[] EnumeratorNames { get; set; }=[];
        public string[] CurrentNames { get; set; }=[];
        public string MoveNextCondition { get; set; }="";
        public string Build { get; set; }="";
        public string Emit { get; set; }="";
        public string EmitValue { get; set; }="";
        public CollectionStopMode StopMode { get; set; }=CollectionStopMode.None;

        public CollectionAttribute(
            string  初期化,
            string ループ前処理,
            string ループ文,
            string ループ内部先頭,
            string ループ内部Add,
            string ループ内部末尾,
            string 出力要素式,
            string 評価式
        ) : this(初期化,ループ前処理,ループ文,ループ内部先頭,ループ内部Add,ループ内部末尾,"","","","","","","","","","",出力要素式,評価式){
        }

        public CollectionAttribute(
            string 初期化,string ループ前処理0,string ループ文0,string ループ内部先頭0,string 評価式
        ) : this(初期化,ループ前処理0,ループ文0,ループ内部先頭0,"","","",評価式)
        {
        }
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AggregateAttribute(string 初期化,string foreach前処理,string foreach文,string foreach内部,string 評価式): Attribute{
        public readonly string 初期化 = 初期化;
        public readonly string foreach前処理 = foreach前処理;
        public readonly string foreach文 = foreach文;
        public readonly string foreach内部 = foreach内部;
        public readonly string 評価式 = 評価式;
        public AggregateAttribute(string foreach前処理,string foreach文,string foreach内部,string 評価式):this("",foreach前処理,foreach文,foreach内部,評価式){
        }
    }
    /// <summary>
    /// デリゲートパラメーターにつける属性
    /// {&selector&}は変数名を決めたデリゲート参照。例えばselector0
    /// {#selector#}とするとインライン展開される
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class LambdaAttribute : Attribute
    {
    }
    //[AttributeUsage(AttributeTargets.)]
    //public sealed class ResultCollectionAttribute:Attribute {
    //}
}
