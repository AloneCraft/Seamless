using System;
using System.Diagnostics.CodeAnalysis;
// ReSharper disable InvalidXmlDocComment
namespace Seamless.Common
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PipelineAttribute : Attribute
    {
        public readonly string Code;
        public PipelineAttribute(string Code)
        {
            this.Code = Code;
        }
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
    /// 前のチェインメソッドのループ先頭を利用するなら
    /// ループ前処理→前のチェインメソッドループ先頭→ループ内部先頭→次のメソッドに渡す→ループ内部末尾→前のチェインメソッド"}"→return文
    /// 前がチェインメソッドでなかったら
    /// ループ前処理→ループ内部先頭→ループ内部Add→ループ内部末尾→前のチェインメソッド"}"→return文
    /// AggregateAttribute(集約関数)の文字列仕様:
    /// 前のチェインメソッドのループ先頭を利用するなら
    /// ループ前処理→前のチェインメソッドループ先頭→ループ内部→前のチェインメソッド"}"→return文
    /// 前がチェインメソッドでなかったら
    /// ループ前処理→ループ先頭→ループ内部→"}"→return文
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CollectionAttribute : Attribute
    {
        public readonly string Enumerableを返す場合の初期化処理;
        public readonly string ループ前処理;
        public readonly string ループ先頭;
        public readonly string ループ内部先頭;
        public readonly string ループ内部Add;
        public readonly string ループ内部末尾;

        public readonly string 評価式;
        public readonly string Return文;
        public CollectionAttribute(
            string  Enumerableを返す場合の初期化処理,string ループ前処理,string ループ先頭,string ループ内部先頭,string ループ内部Add,string ループ内部末尾,string 評価式,string return文
        )
        {
            this.Enumerableを返す場合の初期化処理=Enumerableを返す場合の初期化処理;
            this.ループ前処理  =ループ前処理;
            this.ループ先頭    =ループ先頭;
            this.ループ内部先頭=ループ内部先頭;
            this.ループ内部Add =ループ内部Add;
            this.ループ内部末尾=ループ内部末尾;
            this.評価式=評価式;
            this.Return文      =return文;
        }

        public CollectionAttribute(
            string Enumerableを返す場合の初期化処理,string ループ前処理,string ループ先頭,string ループ内部先頭,string return文
        ) : this(Enumerableを返す場合の初期化処理,ループ前処理,ループ先頭,ループ内部先頭,"","","",return文)
        {
        }
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AggregateAttribute : Attribute{
        public readonly string ループ前処理;
        public readonly string ループ先頭;
        public readonly string ループ内部;
        public readonly string 評価式;
        public AggregateAttribute(string ループ前処理,string ループ先頭,string ループ内部,string 評価式){
            this.ループ前処理               =ループ前処理;
            this.ループ先頭                =ループ先頭;
            this.ループ内部              =ループ内部;
            this.評価式              =評価式;
        }
        public AggregateAttribute(string 未使用,string ループ前処理,string ループ先頭,string ループ内部,string 評価式){
            this.ループ前処理=ループ前処理;
            this.ループ先頭=ループ先頭;
            this.ループ内部=ループ内部;
            this.評価式=評価式;
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
