using System.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Seamless.Common;

namespace Seamless.Linq;

public static class EnumerableSource
{
    public abstract class Chain(string staticメソッド,string BeginGlobal,string BeginLoopOutside,string BeginLoop,string BeginLoopInside,string EndLoopInside,string EndLoop,string EndLoopOutside,string EndGlobal,string result)
    {
        public readonly string staticメソッド=staticメソッド;
        public readonly string BeginGlobal=BeginGlobal;
        public readonly string BeginLoopOutside=BeginLoopOutside;
        public readonly string BeginLoop=BeginLoop;
        public readonly string BeginLoopInside=BeginLoopInside;
        public readonly string EndLoopInside=EndLoopInside;
        public readonly string EndLoop=EndLoop;
        public readonly string EndLoopOutside=EndLoopOutside;
        public readonly string EndGlobal=EndGlobal;
        public readonly string result=result;
        public readonly string[] Requires;     // このコードが実行されるのに必要な変数名（サフィックス付き）
        public readonly string Provides; // このコードが生成する結果の変数名
    }
    private static string Type名<T>() => typeof(T).FullName ?? typeof(T).Name;
    private static string Listへ実体化<TSource>(IEnumerable source,string temp)
    {
        return $$"""
            {{temp}}.Clear();
            {{source.BeginLoopOutside}}
            {{source.BeginLoop}}
                {{source.BeginLoopInside}}
                {{temp}}.Add({{source.Element}});
                {{source.EndLoopInside}}
            {{source.EndLoop}}
            {{source.EndLoopOutside}}
            """;
    }
    private static IEnumerable キャリア化<TSource>(string staticメソッド,string BeginGlobal,string BeginLoopOutside,string sourceResult,ref Info Info,string[]? Requires=null)
    {
        var 番号 = Info.番号++;
        var enumerator = $"e{番号}";
        var Start = $"Start{番号}";
        var End = $"End{番号}";
        var Element = $"item{番号}";
        var MoveNext = $"{enumerator}.MoveNext()";
        return new(
            staticメソッド,
            BeginGlobal,
            BeginLoopOutside,
            $$"""
                var {{enumerator}}={{sourceResult}}.GetEnumerator();
                goto {{Start}};
                """,
            "",
            $"result.Add({Element});",
            Element,
            $$"""
                {{Start}}:;
                if(!{{MoveNext}})goto {{End}};
                var {{Element}}={{enumerator}}.Current;
                """,
            $$"""
                goto {{Start}};
                """,
            $$"""
                {{End}}:;
                """,
            "",
            sourceResult,
            Requires ?? [],
            MoveNext
        );
    }

    public class IEnumerable(string staticメソッド,string BeginGlobal,string BeginLoopOutside,string BeginLoop,string BeginLoopInside,string Add,string Element,string EndLoopInside,string EndLoop,string EndLoopOutside,string EndGlobal,string result,string[]? Requires,string MoveNext)
    {
        public readonly string staticメソッド=staticメソッド;
        public readonly string BeginGlobal=BeginGlobal;
        public readonly string BeginLoopOutside=BeginLoopOutside;
        public readonly string BeginLoop=BeginLoop;
        public readonly string BeginLoopInside=BeginLoopInside;
        public readonly string Add=Add;
        public readonly string EndLoopInside=EndLoopInside;
        public readonly string EndLoop=EndLoop;
        public readonly string EndLoopOutside=EndLoopOutside;
        public readonly string EndGlobal=EndGlobal;
        public readonly string result=result;
        public readonly string[] Requires=Requires;     // このコードが実行されるのに必要な変数名（サフィックス付き）
        public readonly string Provides=Element; // このコードが生成する結果の変数名
        public string MoveNext=MoveNext;
        public string Element=Element;
        public string If_MoveNext_goto(string Label) => $"if({this.MoveNext})goto {Label}";
        public string If_Not_MoveNext_goto(string Label) => $"if(!{this.MoveNext})goto {Label}";

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(this.BeginLoopOutside);
            sb.Append(this.BeginLoop);
            sb.Append(this.BeginLoopInside);
            sb.Append(this.EndLoopInside);
            sb.Append(this.EndLoop);
            sb.Append(this.EndLoopOutside);
            return sb.ToString();
        }
    }

    public class IEnumerable<TSource>(string staticメソッド,string BeginGlobal,string BeginLoopOutside,string BeginLoop,string BeginLoopInside,string Add,string Element,string EndLoopInside,string EndLoop,string EndLoopOutside,string EndGlobal,string result,string[]? Requires = null,string MoveNext = ""):IEnumerable(staticメソッド,BeginGlobal,BeginLoopOutside,BeginLoop,BeginLoopInside,Add,Element,EndLoopInside,EndLoop,EndLoopOutside,EndGlobal,result,Requires,MoveNext);
    public class Scalar(
        string staticメソッド,
        string BeginGlobal,
        string BeginLoopOutside,
        string BeginLoop,
        string BeginLoopInside,
        string EndLoopInside,
        string EndLoop,
        string EndLoopOutside,
        string EndGlobal,
        string result,
        string[]? Requires = null){
        public readonly string staticメソッド=staticメソッド;
        public readonly string BeginGlobal=BeginGlobal;
        public readonly string BeginLoopOutside=BeginLoopOutside;
        public readonly string BeginLoop=BeginLoop;
        public readonly string BeginLoopInside=BeginLoopInside;
        public readonly string EndLoopInside=EndLoopInside;
        public readonly string EndLoop=EndLoop;
        public readonly string EndLoopOutside=EndLoopOutside;
        public readonly string EndGlobal=EndGlobal;
        public readonly string result=result;
        public readonly string[] Requires=Requires ?? [];     // このコードが実行されるのに必要な変数名（サフィックス付き）
        public readonly string Provides=result; // このコードが生成する結果の変数名
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(this.BeginLoopOutside);
            sb.Append(this.BeginLoop);
            sb.Append(this.BeginLoopInside);
            sb.Append(this.EndLoopInside);
            sb.Append(this.EndLoop);
            sb.Append(this.EndLoopOutside);
            sb.Append(this.result);
            return sb.ToString();
        }
    }
    public abstract class Func
    {
        public string DefineLambda;
        public string DefineMethod(string MethodName)
        {
            var Invoke = this.GetType().GetMethod("Invoke");
            var Parameters = Invoke.GetParameters();

            var returnType = Invoke.ReturnType.FullName;
            var parametersList = new StringBuilder();
            var Arguments = this.GetType().GetGenericArguments();
            for (int i = 0; i < Parameters.Length; i++)
            {
                if (i > 0) parametersList.Append(", ");
                var paramType = Arguments[i].FullName ?? "object";
                var paramName = Parameters[i].Name;
                parametersList.Append($"{paramType} {paramName}");
            }

            var result = $"public static {returnType} {MethodName}({parametersList}){this.DefineLambda}";
            return result;
        }
    }

    public class Func<T0,TResult>:Func;

    public class Func<T0,T1,TResult>:Func;
    public class SelectMany_selector:Func{
        public IEnumerable Source;
    }
    public struct Info
    {
        public int 番号;
        public string DefineFields;
    }
    //string staticメソッド,
    //string BeginGlobal,
    //string BeginLoopOutside,
    //string BeginLoop
    //,string BeginLoopInside
    //,string Add
    //,string Element
    //,string EndLoopInside
    //,string EndLoop
    //,string EndLoopOutside
    //,string EndGlobal
    //,string result
    //,string[]? Requires = null,string MoveNext = "")
    public static IEnumerable ループの起点<TSource>(this IEnumerable source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var enumerator = $"e{番号}";
        var Start = $"Start{番号}";
        var MoveNext = $"{enumerator}.MoveNext()";
        return new(
            "",
            "",
            "",
            $$"""
                var {{enumerator}}={{source.result}}.GetEnumerator();
                goto {{Start}};
            """,
            "",
            $"{enumerator}.Current",
            "",
            $$"""
                {{Start}}:;
            """,
            "",
            "",
            "",
            source.result,
            [],
            MoveNext
        );
    }
    public static Scalar Aggregate<TSource>(this IEnumerable source, Func<TSource, TSource, TSource> func,ref Info Info)
    {
        var 番号 = Info.番号++;
        var Struct=$"Struct{番号}";
        var Head = $"Head{番号}";
        var Continue = $"Continue{番号}";
        var Success = $"Success{番号}";
        var hasValue = $"hasValue{番号}";
        var result = $"result{番号}";
        return new(
            staticメソッド: $$"""
                private struct {{Struct}}{
                    {{func.DefineMethod(nameof(func))}}
                }
                """,
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                {{Type名<TSource>()}} {{result}}=default!;
                var {{hasValue}}=false;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                if({{hasValue}})goto {{Head}};
                {{result}}={{source.Element}};
                {{hasValue}}=true;
                goto {{Continue}};
                {{Head}}:
                {{result}}={{Struct}}.func({{result}},{{source.Element}});
                """,
            $$"""
                {{Continue}}:;
                {{source.EndLoopInside}}
                """,
            source.EndLoop,
            $$"""
                if({{hasValue}})goto {{Success}};
                throw NoElements();
                {{Success}}:;
                {{source.EndLoopOutside}}
                """,
            source.EndGlobal,
            result
        );
    }

    public static Scalar Aggregate<TSource, TAccumulate>(this IEnumerable source,string seed,Func<TAccumulate, TSource, TAccumulate> func,ref Info Info) {
        var 番号=Info.番号++;
        var Struct=$"Struct{番号}";
        var Next=$"Next{番号}";
        var Start=$"Start{番号}";
        var Head = $"Head{番号}";
        var result=$"result{番号}";
        return new(
            staticメソッド: $$"""
                private struct {{Struct}}{
                    {{func.DefineMethod(nameof(func))}}
                }
                """,
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{result}}={{seed}};
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                {{result}}={{Struct}}.func({{result}},{{source.Element}});
                """,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            result
        );
    }
    public static Scalar Aggregate<TSource, TAccumulate, TResult>(this IEnumerable source, string seed, Func<TAccumulate, TSource, TAccumulate> func, Func<TAccumulate, TResult> resultSelector,ref Info Info)
    {
        var 番号=Info.番号++;
        var Struct=$"Struct{番号}";
        var result=$"result{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{func.DefineMethod(nameof(func))}}
                    {{resultSelector.DefineMethod(nameof(resultSelector))}}
                }
                """,
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{result}}={{seed}};
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                {{result}}={{Struct}}.func({{result}},{{source.Element}});
                """,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            $"{Struct}.resultSelector({result})"
        );
    }
    public static IEnumerable AggregateBy<TSource, TKey, TAccumulate>(this IEnumerable source, Func<TSource, TKey> keySelector, string seed, Func<TAccumulate, TSource, TAccumulate> func,ref Info Info,string? keyComparer = null) where TKey : notnull
    {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        var Struct=$"Struct{番号}";
        var seed0 = $"seed{番号}";
        var dictionary = $"dictionary{番号}";
        var item= $"item{番号}";
        var Element= $"output{番号}";
        var accumulator= $"accumulator{番号}";
        var Key = $"key{番号}";
        var Acc = $"acc{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{keySelector.DefineMethod(nameof(keySelector))}}
                    {{func.DefineMethod(nameof(func))}}
                }
                """,
            $$"""
                {{source.BeginGlobal}}
                var {{dictionary}} = new Dictionary<{{Type名<TKey>()}},{{Type名<TAccumulate>()}}>({{keyComparer}});
                var {{result}} = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<{{Type名<TKey>()}}, {{Type名<TAccumulate>()}}>>();
                """,
            $$"""
                var {{seed0}}={{seed}};
                {{dictionary}}.Clear();
                {{result}}.Clear();
                {{source.BeginLoopOutside}}
                {{source.BeginLoop}}
                    {{source.BeginLoopInside}}
                    var {{Key}}={{Struct}}.keySelector({{source.Element}});
                    var {{accumulator}} = {{dictionary}}.GetValueOrDefault({{Key}}, {{seed0}});
                    {{dictionary}}[{{Key}}] = {{Struct}}.func({{accumulator}}, {{source.Element}});
                    {{source.EndLoopInside}}
                {{source.EndLoop}}
                {{source.EndLoopOutside}}
                """,
            $$"""
                foreach(var {{Element}} in {{dictionary}}){
                """,
            "",
            $$"""
                    {{result}}.Add({{Element}});
                """,
            Element,
            "",
            $$"""
                }
                """,
            "",
            source.EndGlobal,
            result,
            [],
            ""
        );
    }
    public static IEnumerable AggregateBy<TSource, TKey, TAccumulate>(this IEnumerable source, Func<TSource, TKey> keySelector, Func<TKey, TAccumulate> seedSelector, Func<TAccumulate, TSource, TAccumulate> func,ref Info Info,string? keyComparer = null) where TKey : notnull
    {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        var Struct=$"Struct{番号}";
        var dictionary = $"dictionary{番号}";
        var Element= $"item{番号}";
        var accumulator= $"accumulator{番号}";
        var Key = $"key{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{keySelector.DefineMethod(nameof(keySelector))}}
                    {{seedSelector.DefineMethod(nameof(seedSelector))}}
                    {{func.DefineMethod(nameof(func))}}
                }
                """,
            $$"""
                {{source.BeginGlobal}}
                var {{dictionary}} = new Dictionary<{{Type名<TKey>()}},{{Type名<TAccumulate>()}}>({{keyComparer}});
                var {{result}} = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<{{Type名<TKey>()}}, {{Type名<TAccumulate>()}}>>();
                """,
            $$"""
                {{dictionary}}.Clear();
                {{result}}.Clear();
                {{source.BeginLoopOutside}}
                {{source.BeginLoop}}
                    {{source.BeginLoopInside}}
                    var {{Key}}={{Struct}}.keySelector({{source.Element}});
                    if(!{{dictionary}}.TryGetValue({{Key}},out var {{accumulator}}))
                        {{accumulator}}={{Struct}}.seedSelector({{Key}});
                    {{dictionary}}[{{Key}}]={{Struct}}.func({{accumulator}},{{source.Element}});
                    {{source.EndLoopInside}}
                {{source.EndLoop}}
                {{source.EndLoopOutside}}
                """,
            $$"""
                foreach(var {{Element}} in {{dictionary}}){
                """,
            $$"""
                    {{result}}.Add({{Element}});
                """,
            "",
            Element,
            "",
            $$"""
                }
                """,
            "",
            source.EndGlobal,
            result,
            [],
            ""
        );
    }
    public static Scalar All<TSource>(this IEnumerable source, Func<TSource, bool> predicate,ref Info Info)
    {
        var 番号=Info.番号++;
        var Struct=$"Struct{番号}";
        var Continue=$"Continue{番号}";
        var End=$"End{番号}";
        var result=$"result{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{predicate.DefineMethod(nameof(predicate))}}
                }
                """,
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{result}}=true;
                """,
            source.BeginLoop,
            $$"""
                    {{source.BeginLoopInside}}
                    if({{Struct}}.predicate({{source.Element}}))goto {{Continue}};
                    {{result}}=false;
                    goto {{End}};
                """,
            $$"""
                {{Continue}}:;
                {{source.EndLoopInside}}
                """,
            source.EndLoop,
            $$"""
                {{End}}:;
                {{source.EndLoopOutside}}
                """,
            source.EndGlobal,
            result
        );
    }
    public static Scalar Any<TSource>(this IEnumerable source,ref Info Info)
    {
        var 番号=Info.番号++;
        var End=$"End{番号}";
        var result=$"result{番号}";
        return new(
            "",
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{result}}=false;
                """,
            source.BeginLoop,
            $$"""
                    {{source.BeginLoopInside}}
                    {{result}}=true;
                    goto {{End}};
                """,
            source.EndLoopInside,
            source.EndLoop,
            $$"""
                {{End}}:;
                {{source.EndLoopOutside}}
                """,
            source.EndGlobal,
            result
        );
    }
    public static Scalar Any<TSource>(this IEnumerable source, Func<TSource, bool> predicate,ref Info Info)
    {
        var 番号=Info.番号++;
        var Struct=$"Struct{番号}";
        var Continue=$"Continue{番号}";
        var End=$"End{番号}";
        var result=$"result{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{predicate.DefineMethod(nameof(predicate))}}
                }
                """,
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{result}}=false;
                """,
            source.BeginLoop,
            $$"""
                    {{source.BeginLoopInside}}
                    if(!{{Struct}}.predicate({{source.Element}}))goto {{Continue}};
                    {{result}}=true;
                    goto {{End}};
                """,
            $$"""
                {{Continue}}:;
                {{source.EndLoopInside}}
                """,
            $$"""
                {{source.EndLoop}}
                """,
            $$"""
                {{End}}:;
                {{source.EndLoopOutside}}
                """,
            source.EndGlobal,
            result
        );
    }
    public static IEnumerable Append<TSource>(this IEnumerable source, string element,ref Info Info)
    {
        var 番号=Info.番号++;
        var result=$"result{番号}";
        return new(
            "",
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{result}}=new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                {{result}}.Add({{source.Element}});
                """,
            "",
            source.Element,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            result,
            [],
            source.MoveNext
        );
    }
    public static IEnumerable AsEnumerable<TSource>(this IEnumerable source,ref Info Info)
    {
        return new(
            source.staticメソッド,
            source.BeginGlobal,
            source.BeginLoopOutside,
            source.BeginLoop,
            source.BeginLoopInside,
            source.Add,
            source.Element,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            source.result,
            source.Requires,
            source.MoveNext
        );
    }
    private static Scalar Average(IEnumerable source,string staticメソッド,string sumのType,global::System.Func<string,string,string>BeginLoopInside,string EndLoopInside,global::System.Func<string,string,string>結果式,ref Info Info,string empty式 = "throw NoElements()")
    {
        var 番号=Info.番号++;
        var sum=$"sum{番号}";
        var count=$"count{番号}";
        var Success=$"NoElement{番号}";
        var result=$"result{番号}";
        // string Struct定義;
        // Struct定義=$$"""
        //     private struct {{Struct}}{
        //         {{Func.DefineMethod(staticメソッド名)}}
        //     }
        //     """;
        return new(
            staticメソッド,
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{sum}}=default({{sumのType}});
                var {{count}}=0L;
                """,
            source.BeginLoop,
            $$"""
                    {{source.BeginLoopInside}}
                    {{BeginLoopInside(sum,count)}}
                """,
            EndLoopInside,
            source.EndLoop,
            $$"""
                if({{count}}>0)goto {{Success}};
                {{empty式}};
                {{Success}}:
                var {{result}}={{結果式(sum,count)}};
                {{source.EndLoopOutside}}
                """,
            source.EndGlobal,
            result
        );
    }

    private static Scalar Average(IEnumerable source,string sumのType,global::System.Func<string,string,string> BeginLoopInside,
        string EndLoopInside,global::System.Func<string,string,string> 結果式,ref Info Info) =>
        Average(source,"",sumのType,BeginLoopInside,EndLoopInside,結果式,ref Info);

    public static Scalar Average(this IEnumerable<double> source,ref Info Info)
    {
        return Average(
            source,"double",
            (sum,count) => {
                return $$"""
                    {{sum}}+={{source.Element}};
                    checked{
                        {{count}}++;
                    }
                """;
            },
            source.EndLoopInside,
            (sum,count)=>$"{sum}/{count}",ref Info
        );
    }
    public static Scalar Average(this IEnumerable<int> source,ref Info Info)
    {
        return Average(
            source,"long",
            (sum,count) => {
                return $$"""
                    {{sum}}+={{source.Element}};
                    checked{
                        {{count}}++;
                    }
                """;
            },
            source.EndLoopInside,
            (sum,count)=>$"(double){sum}/{count}",ref Info
        );
    }
    public static Scalar Average(this IEnumerable<long> source,ref Info Info)
    {
        return Average(
            source,"","decimal",
            (sum,count) => {
                return $$"""
                    {{sum}}+={{source.Element}};
                    checked{
                        {{count}}++;
                    }
                """;
            },
            source.EndLoopInside,
            (sum,count)=>$"(double)({sum}/{count})",ref Info
        );
    }
    public static Scalar Average(this IEnumerable<decimal> source,ref Info Info)
    {
        return Average(
            source,"","decimal",
            (sum,count) => {
                return $$"""
                    {{sum}}+={{source.Element}};
                    checked{
                        {{count}}++;
                    }
                """;
            },
            source.EndLoopInside,
            (sum,count)=>$"{sum}/{count}",ref Info
        );
    }
    public static Scalar Average(this IEnumerable<decimal?> source,ref Info Info)
    {
        var Continue=$"Continue{Info.番号}";
        return Average(
            source,"","decimal",
            (sum,count) => {
                return $$"""
                    if(!{{source.Element}}.HasValue)goto {{Continue}};
                    checked{
                        {{sum}}+={{source.Element}}.GetValueOrDefault();
                        {{count}}++;
                    }
                """;
            },
            $"{Continue}:;",
            (sum,count)=>$"{sum}/{count}",ref Info,"return null"
        );
    }
    public static Scalar Average(this IEnumerable<double?> source,ref Info Info)
    {
        var Continue=$"Continue{Info.番号}";
        return Average(
            source,"","double",
            (sum,count) => {
                return $$"""
                    if(!{{source.Element}}.HasValue)goto {{Continue}};
                    {{sum}}+={{source.Element}}.GetValueOrDefault();
                    checked{
                        {{count}}++;
                    }
                """;
            },
            $"{Continue}:;",
            (sum,count)=>$"{sum}/{count}",ref Info,"return null"
        );
    }
    public static Scalar Average(this IEnumerable<int?> source,ref Info Info)
    {
        var Continue=$"Continue{Info.番号}";
        return Average(
            source,"","long",
            (sum,count) => {
                return $$"""
                    if(!{{source.Element}}.HasValue)goto {{Continue}};
                    {{sum}}+={{source.Element}}.GetValueOrDefault();
                    checked{
                        {{count}}++;
                    }
                """;
            },
            $"{Continue}:;",
            (sum,count)=>$"(double){sum}/{count}",ref Info,"return null"
        );
    }
    public static Scalar Average(this IEnumerable<long?> source,ref Info Info)
    {
        var Continue=$"Continue{Info.番号}";
        return Average(
            source,"","decimal",
            (sum,count) => {
                return $$"""
                    if(!{{source.Element}}.HasValue)goto {{Continue}};
                    checked{
                        {{sum}}+={{source.Element}}.GetValueOrDefault();
                        {{count}}++;
                    }
                """;
            },
            $"{Continue}:;",
            (sum,count)=>$"(double)({sum}/{count})",ref Info,"return null"
        );
    }
    public static Scalar Average(this IEnumerable<float?> source,ref Info Info)
    {
        var Continue=$"Continue{Info.番号}";
        return Average(
            source,"","double",
            (sum,count) => {
                return $$"""
                    if(!{{source.Element}}.HasValue)goto {{Continue}};
                    {{sum}}+={{source.Element}}.GetValueOrDefault();
                    checked{
                        {{count}}++;
                    }
                """;
            },
            $"{Continue}:;",
            (sum,count)=>$"(float)({sum}/{count})",ref Info,"return null"
        );
    }
    public static Scalar Average(this IEnumerable<float> source,ref Info Info)
    {
        return Average(
            source,"double",
            (sum,count) => {
                return $$"""
                    {{sum}}+={{source.Element}};
                    checked{
                        {{count}}++;
                    }
                """;
            },
            source.EndLoopInside,
            (sum,count)=>$"(float)({sum}/{count})",ref Info
        );
    }
    private static string Struct定義(string Struct,Func Func,string メソッド名)
    {
        return $$"""
            private struct {{Struct}}{
                {{Func.DefineMethod(メソッド名)}}
            }
            """;
    }
    public static Scalar Average<TSource>(this IEnumerable source, Func<TSource, decimal> selector,ref Info Info)
    {
        var 番号=Info.番号;
        var Struct=$"Struct{番号}";
        return Average(
            source,
            Struct定義(Struct,selector,nameof(selector)),
            "decimal",
            (sum,count) => {
                return $$"""
                    checked{
                        {{sum}}+={{Struct}}.selector({{source.Element}});
                        {{count}}++;
                    }
                """;
            },
            source.EndLoopInside,
            (sum,count)=>$"{sum}/{count}",ref Info
        );
    }
    public static Scalar Average<TSource>(this IEnumerable source, Func<TSource, double> selector,ref Info Info)
    {
        var 番号=Info.番号;
        var Struct=$"Struct{番号}";
        return Average(
            source,
            Struct定義(Struct,selector,nameof(selector)),
            "double",
            (sum,count) => {
                return $$"""
                    {{sum}}+={{Struct}}.selector({{source.Element}});
                    checked{
                        {{count}}++;
                    }
                """;
            },
            source.EndLoopInside,
            (sum,count)=>$"{sum}/{count}",ref Info
        );
    }
    public static Scalar Average<TSource>(this IEnumerable source, Func<TSource, int> selector,ref Info Info)
    {
        var 番号=Info.番号;
        var Struct=$"Struct{番号}";
        return Average(
            source,
            Struct定義(Struct,selector,nameof(selector)),
            "long",
            (sum,count) => {
                return $$"""
                    checked{
                        {{sum}}+={{Struct}}.selector({{source.Element}});
                        {{count}}++;
                    }
                """;
            },
            source.EndLoopInside,
            (sum,count)=>$"(double){sum}/{count}",ref Info
        );
    }
    public static Scalar Average<TSource>(this IEnumerable source, Func<TSource, long> selector,ref Info Info)
    {
        var 番号=Info.番号;
        var Struct=$"Struct{番号}";
        return Average(
            source,
            Struct定義(Struct,selector,nameof(selector)),
            "decimal",
            (sum,count) => {
                return $$"""
                    checked{
                        {{sum}}+={{Struct}}.selector({{source.Element}});
                        {{count}}++;
                    }
                """;
            },
            source.EndLoopInside,
            (sum,count)=>$"(double)({sum}/{count})",ref Info
        );
    }
    public static Scalar Average<TSource>(this IEnumerable source, Func<TSource, decimal?> selector,ref Info Info)
    {
        var 番号=Info.番号;
        var Struct=$"Struct{番号}";
        var value=$"value{番号}";
        var Continue=$"Continue{番号}";
        return Average(
            source,
            Struct定義(Struct,selector,nameof(selector)),
            "decimal",
            (sum,count) => {
                return $$"""
                    var {{value}} = {{Struct}}.selector({{source.Element}});
                    if(!{{value}}.HasValue) goto {{Continue}};
                    checked{
                        {{sum}}+={{value}}.GetValueOrDefault();
                        {{count}}++;
                    }
                """;
            },
            $"{Continue}:;",
            (sum,count)=>$"{sum}/{count}",ref Info,"return null"
        );
    }
    public static Scalar Average<TSource>(this IEnumerable source, Func<TSource, double?> selector,ref Info Info)
    {
        var 番号=Info.番号;
        var Struct=$"Struct{番号}";
        var value=$"value{番号}";
        var Continue=$"Continue{番号}";
        return Average(
            source,
            Struct定義(Struct,selector,nameof(selector)),
            "double",
            (sum,count) => {
                return $$"""
                    var {{value}} = {{Struct}}.selector({{source.Element}});
                    if(!{{value}}.HasValue) goto {{Continue}};
                    {{sum}}+={{value}}.GetValueOrDefault();
                    checked{
                        {{count}}++;
                    }
                """;
            },
            $"{Continue}:;",
            (sum,count)=>$"{sum}/{count}",ref Info,"return null"
        );
    }
    public static Scalar Average<TSource>(this IEnumerable source, Func<TSource, int?> selector,ref Info Info)
    {
        var 番号=Info.番号;
        var Struct=$"Struct{番号}";
        var value=$"value{番号}";
        var Continue=$"Continue{番号}";
        return Average(
            source,
            Struct定義(Struct,selector,nameof(selector)),
            "long",
            (sum,count) => {
                return $$"""
                    var {{value}} = {{Struct}}.selector({{source.Element}});
                    if(!{{value}}.HasValue) goto {{Continue}};
                    checked{
                        {{sum}}+={{value}}.GetValueOrDefault();
                        {{count}}++;
                    }
                """;
            },
            $"{Continue}:;",
            (sum,count)=>$"(double){sum}/{count}",ref Info,"return null"
        );
    }
    public static Scalar Average<TSource>(this IEnumerable source, Func<TSource, long?> selector,ref Info Info)
    {
        var 番号=Info.番号;
        var Struct=$"Struct{番号}";
        var value=$"value{番号}";
        var Continue=$"Continue{番号}";
        return Average(
            source,
            Struct定義(Struct,selector,nameof(selector)),
            "decimal",
            (sum,count) => {
                return $$"""
                    var {{value}} = {{Struct}}.selector({{source.Element}});
                    if(!{{value}}.HasValue) goto {{Continue}};
                    checked{
                        {{sum}}+={{value}}.GetValueOrDefault();
                        {{count}}++;
                    }
                """;
            },
            $"{Continue}:;",
            (sum,count)=>$"(double)({sum}/{count})",ref Info,"return null"
        );
    }
    public static Scalar Average<TSource>(this IEnumerable source, Func<TSource, float?> selector,ref Info Info)
    {
        var 番号=Info.番号;
        var Struct=$"Struct{番号}";
        var value=$"value{番号}";
        var Continue=$"Continue{番号}";
        return Average(
            source,
            Struct定義(Struct,selector,nameof(selector)),
            "double",
            (sum,count) => {
                return $$"""
                    var {{value}} = {{Struct}}.selector({{source.Element}});
                    if(!{{value}}.HasValue) goto {{Continue}};
                    {{sum}}+={{value}}.GetValueOrDefault();
                    checked{
                        {{count}}++;
                    }
                """;
            },
            $"{Continue}:;",
            (sum,count)=>$"(float)({sum}/{count})",ref Info,"return null"
        );
    }
    public static Scalar Average<TSource>(this IEnumerable source, Func<TSource, float> selector,ref Info Info)
    {
        var 番号=Info.番号;
        var Struct=$"Struct{番号}";
        var value=$"value{番号}";
        return Average(
            source,
            Struct定義(Struct,selector,nameof(selector)),
            "double",
            (sum,count) => {
                return $$"""
                    {{sum}}+={{Struct}}.selector({{source.Element}});
                    checked{
                        {{count}}++;
                    }
                """;
            },
            source.EndLoopInside,
            (sum,count)=>$"(float)({sum}/{count})",ref Info
        );
    }
    public static IEnumerable Cast<TResult>(this IEnumerable source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        return new(
            "",
            $$"""
                {{source.BeginGlobal}}
                var {{result}} = new System.Collections.Generic.List<{{Type名<TResult>()}}>();
                """,
            $$"""
                {{source.BeginLoopOutside}}
                {{result}}.Clear();
                """,
            source.BeginLoop,
            $$"""
                    {{source.BeginLoopInside}}
                    {{result}}.Add(({{Type名<TResult>()}}){{source.Element}}!);
                """,
            "",
            $"({Type名<TResult>()}){source.Element}!",
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            result,
            [],
            source.MoveNext
        );
    }
    public static IEnumerable Chunk<TSource>(this IEnumerable source,string size,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        var chunk=$"chunk{番号}";
        var index=$"index{番号}";
        var Continue=$"Continue{番号}";
        var End=$"End{番号}";
        return new(
            "",
            $$"""
                {{source.BeginGlobal}}
                var {{result}} = new System.Collections.Generic.List<{{Type名<TSource>()}}[]>();
                """,
            $$"""
                {{source.BeginLoopOutside}}
                if({{size}}<1)throw new ArgumentOutOfRangeException(nameof(size));
                {{result}}.Clear();
                var {{chunk}} = new {{Type名<TSource>()}}[{{size}}];
                var {{index}} = 0;
                """,
            $$"""
                {{source.BeginLoop}}
                """,
            $$"""
                    {{source.BeginLoopInside}}
                    {{chunk}}[{{index}}++] = {{source.Element}};
                    if({{index}}<{{size}})goto {{Continue}};
                    {{result}}.Add({{chunk}});
                """,
            "",
            chunk,
            $$"""
                    {{chunk}} = new {{Type名<TSource>()}}[{{size}}];
                    {{index}} = 0;
                    {{Continue}}: ;
                    {{source.EndLoopInside}}
                """,
            $$"""
                if({{index}}==0) goto {{End}};
                Array.Resize(ref {{chunk}},{{index}});
                {{result}}.Add(item: {{chunk}});
                {{End}}:
                {{source.EndLoop}}
                """,
            source.EndLoopOutside,
            source.EndGlobal,
            result,
            [],
            ""
        );
    }
    public static IEnumerable Concat<TSource>(this IEnumerable first, IEnumerable second,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        var Element=$"item{番号}";
        var Continue=$"Continue{番号}";
        return new(
            "",
            $$"""
                var {{result}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                {{first.BeginGlobal}}
                {{second.BeginGlobal}}
            """,
            $$"""
                {{result}}.Clear();
                {{first.BeginLoopOutside}}
                {{first.BeginLoop}}
                    {{first.BeginLoopInside}}
                    {{result}}.Add({{first.Element}});
                    {{first.EndLoopInside}}
                {{first.EndLoop}}
                {{first.EndLoopOutside}}
                {{second.BeginLoopOutside}}
                {{second.BeginLoop}}
                    {{second.BeginLoopInside}}
                    {{result}}.Add({{second.Element}});
                    {{second.EndLoopInside}}
                {{second.EndLoop}}
                {{second.EndLoopOutside}}
            """,
            $$"""
                foreach(var {{Element}} in {{result}}){
            """,
            "",
            "",
            Element,
            "",
            $$"""
                }
                """,
            "",
            $$"""
                {{first.EndGlobal}}
                {{second.EndGlobal}}
            """,
            result,
            [],
            ""
        );
    }
    public static Scalar Contains<TSource>(this IEnumerable source,string value,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        var Element=$"item{番号}";
        var End=$"End{番号}";
        var comparer=$"comparer{番号}";
        return new(
            "",
            $$"""
                {{source.BeginGlobal}}
                var {{comparer}}=EqualityComparer<{{Type名<TSource>()}}>.Default;
            """,
            $$"""
                {{source.BeginLoopOutside}}
                var {{result}}=false;
            """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                if({{comparer}}.Equals({{source.Element}},{{value}})){
                    {{result}}=true;
                    goto {{End}};
                }
            """,
            source.EndLoopInside,
            source.EndLoop,
            $$"""
                {{End}}:;
                {{source.EndLoopOutside}}
            """,
            source.EndGlobal,
            result
        );
    }
    public static Scalar Contains<TSource>(this IEnumerable source, string value,string? comparer,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        var End=$"End{番号}";
        var comparer0=$"comparer{番号}";
        var comparerArg = comparer ?? "null";
        return new(
            "",
            $$"""
                {{source.BeginGlobal}}
                var {{comparer0}}={{comparerArg}}??EqualityComparer<{{Type名<TSource>()}}>.Default;
            """,
            $$"""
                {{source.BeginLoopOutside}}
                var {{result}}=false;
            """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                if({{comparer0}}.Equals({{source.Element}},{{value}})){
                    {{result}}=true;
                    goto {{End}};
                }
            """,
            source.EndLoopInside,
            source.EndLoop,
            $$"""
                {{End}}:;
                {{source.EndLoopOutside}}
            """,
            source.EndGlobal,
            result
        );
    }
    public static Scalar Count<TSource>(this IEnumerable source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var count=$"count{番号}";
        return new(
            "",
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{count}}=0;
            """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                checked {
                    {{count}}++;
                }
            """,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            count
        );
    }
    public static Scalar Count<TSource>(this IEnumerable source, Func<TSource, bool> predicate,ref Info Info)
    {
        var 番号 = Info.番号++;
        var Struct=$"Struct{番号}";
        var count=$"count{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{predicate.DefineMethod(nameof(predicate))}}
                }
                """,
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{count}}=0;
            """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                if({{Struct}}.predicate({{source.Element}})){
                    checked {
                        {{count}}++;
                    }
                }
            """,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            count
        );
    }
    public static IEnumerable CountBy<TSource, TKey>(this IEnumerable source, Func<TSource, TKey> keySelector,ref Info Info,string? keyComparer = null)
        where TKey : notnull
    {
        var 番号 = Info.番号++;
        var Struct=$"Struct{番号}";
        var counts=$"counts{番号}";
        var count=$"count{番号}";
        var key=$"key{番号}";
        var result=$"result{番号}";
        var Element=$"pair{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{keySelector.DefineMethod(nameof(keySelector))}}
                }
                """,
            $$"""
                {{source.BeginGlobal}}
                var {{counts}}=new Dictionary<{{Type名<TKey>()}},int>({{keyComparer}});
                var {{result}}=new List<System.Collections.Generic.KeyValuePair<{{Type名<TKey>()}},int>>();
                """,
            $$"""
                {{counts}}.Clear();
                {{result}}.Clear();
                {{source.BeginLoopOutside}}
                {{source.BeginLoop}}
                    {{source.BeginLoopInside}}
                    var {{key}}={{Struct}}.keySelector({{source.Element}});
                    {{counts}}.TryGetValue({{key}},out var {{count}});
                    checked {
                        {{counts}}[{{key}}]={{count}}+1;
                    }
                    {{source.EndLoopInside}}
                {{source.EndLoop}}
                {{source.EndLoopOutside}}
                """,
            $$"""
                foreach(var {{Element}} in {{counts}}){
                """,
            $$"""
                    {{result}}.Add({{Element}});
                """,
            "",
            Element,
            "",
            $$"""
                }
                """,
            "",
            source.EndGlobal,
            result,
            [],
            ""
        );
    }
    public static IEnumerable DefaultIfEmpty<TSource>(this IEnumerable source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        var Element=$"item{番号}";
        return new(
            "",
            $$"""
                {{source.BeginGlobal}}
                var {{result}}=new List<{{Type名<TSource>()}}?>();
                """,
            $$"""
                {{result}}.Clear();
                {{source.BeginLoopOutside}}
                {{source.BeginLoop}}
                    {{source.BeginLoopInside}}
                    {{result}}.Add({{source.Element}});
                    {{source.EndLoopInside}}
                {{source.EndLoop}}
                {{source.EndLoopOutside}}
                if({{result}}.Count==0){{result}}.Add(default);
                """,
            $$"""
                foreach(var {{Element}} in {{result}}){
                """,
            "",
            "",
            Element,
            "",
            $$"""
                }
                """,
            "",
            source.EndGlobal,
            result,
            [],
            ""
        );
    }
    public static IEnumerable DefaultIfEmpty<TSource>(this IEnumerable source,string defaultValue,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        var Element=$"item{番号}";
        return new(
            "",
            $$"""
                {{source.BeginGlobal}}
                var {{result}}=new List<{{Type名<TSource>()}}>();
                """,
            $$"""
                {{result}}.Clear();
                {{source.BeginLoopOutside}}
                {{source.BeginLoop}}
                    {{source.BeginLoopInside}}
                    {{result}}.Add({{source.Element}});
                    {{source.EndLoopInside}}
                {{source.EndLoop}}
                {{source.EndLoopOutside}}
                if({{result}}.Count==0){{result}}.Add({{defaultValue}});
                """,
            $$"""
                foreach(var {{Element}} in {{result}}){
                """,
            "",
            "",
            Element,
            "",
            $$"""
                }
                """,
            "",
            source.EndGlobal,
            result,
            [],
            ""
        );
    }
    public static IEnumerable DistinctBy<TSource, TKey>(this IEnumerable source, Func<TSource, TKey> keySelector,ref Info Info)
    {
        return source.DistinctBy(keySelector,ref Info,null);
    }
    public static IEnumerable DistinctBy<TSource, TKey>(this IEnumerable source, Func<TSource, TKey> keySelector,ref Info Info,string? comparer)
    {
        var 番号 = Info.番号++;
        var Struct=$"Struct{番号}";
        var seen=$"seen{番号}";
        var result=$"result{番号}";
        var Element=$"item{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{keySelector.DefineMethod(nameof(keySelector))}}
                }
                """,
            $$"""
                {{source.BeginGlobal}}
                var {{result}}=new List<{{Type名<TSource>()}}>();
                var {{seen}}=new HashSet<{{Type名<TKey>()}}>({{comparer}});
                """,
            $$"""
                {{result}}.Clear();
                {{seen}}.Clear();
                {{source.BeginLoopOutside}}
                {{source.BeginLoop}}
                    {{source.BeginLoopInside}}
                    if({{seen}}.Add({{Struct}}.keySelector({{source.Element}}))){{result}}.Add({{source.Element}});
                    {{source.EndLoopInside}}
                {{source.EndLoop}}
                {{source.EndLoopOutside}}
                """,
            $$"""
                foreach(var {{Element}} in {{result}}){
                """,
            "",
            "",
            Element,
            "",
            $$"""
                }
                """,
            "",
            source.EndGlobal,
            result,
            [],
            ""
        );
    }
    public static IEnumerable Distinct<TSource>(this IEnumerable source,ref Info Info)
    {
        return source.Distinct<TSource>(ref Info,null);
    }
    public static IEnumerable Distinct<TSource>(this IEnumerable source,ref Info Info,string? comparer)
    {
        var 番号 = Info.番号++;
        var seen=$"seen{番号}";
        var result=$"result{番号}";
        var Element=$"item{番号}";
        return new(
            "",
            $$"""
                {{source.BeginGlobal}}
                var {{result}}=new List<{{Type名<TSource>()}}>();
                var {{seen}}=new HashSet<{{Type名<TSource>()}}>({{comparer}});
                """,
            $$"""
                {{result}}.Clear();
                {{seen}}.Clear();
                {{source.BeginLoopOutside}}
                {{source.BeginLoop}}
                    {{source.BeginLoopInside}}
                    if({{seen}}.Add({{source.Element}})){{result}}.Add({{source.Element}});
                    {{source.EndLoopInside}}
                {{source.EndLoop}}
                {{source.EndLoopOutside}}
                """,
            $$"""
                foreach(var {{Element}} in {{result}}){
                """,
            "",
            "",
            Element,
            "",
            $$"""
                }
                """,
            "",
            source.EndGlobal,
            result,
            [],
            ""
        );
    }
    public static Scalar ElementAtOrDefault<TSource>(this IEnumerable source,string index,ref Info Info)
    {
        var 番号 = Info.番号++;
        var current = $"current{番号}";
        var result = $"result{番号}";
        var Success = $"Success{番号}";
        var found = $"found{番号}";
        return new(
            "",
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{current}}=0;
                var {{found}}=false;
                {{Type名<TSource>()}} {{result}}=default!;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                if({{current}}=={{index}}){
                    {{result}}={{source.Element}};
                    {{found}}=true;
                    goto {{Success}};
                }
                {{current}}++;
                """,
            source.EndLoopInside,
            source.EndLoop,
            $$"""
                {{Success}}:
                {{source.EndLoopOutside}}
                """,
            source.EndGlobal,
            $"{index}<0 || !{found} ? default : {result}"
        );
    }
    public static Scalar ElementAt<TSource>(this IEnumerable source,string index,ref Info Info)
    {
        var 番号 = Info.番号++;
        var current = $"current{番号}";
        var result = $"result{番号}";
        var Success = $"Success{番号}";
        var found = $"found{番号}";
        return new(
            "",
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{current}}=0;
                var {{found}}=false;
                {{Type名<TSource>()}} {{result}}=default!;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                if({{current}}=={{index}}){
                    {{result}}={{source.Element}};
                    {{found}}=true;
                    goto {{Success}};
                }
                {{current}}++;
                """,
            source.EndLoopInside,
            source.EndLoop,
            $$"""
                {{Success}}:
                {{source.EndLoopOutside}}
                """,
            source.EndGlobal,
            $"{index}<0 || !{found} ? throw new ArgumentOutOfRangeException(nameof(index)) : {result}"
        );
    }
    public static IEnumerable Empty<TResult>(ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        return キャリア化<TResult>(
            "",
            $$"""
                var {{result}} = global::Seamless.Linq.Enumerable.Empty<{{Type名<TResult>()}}>();
                """,
            "",
            result,
            ref Info
        );
    }
    public static IEnumerable ExceptBy<TSource, TKey>(this IEnumerable first,string second,Func<TSource, TKey> keySelector,ref Info Info,IEqualityComparer<TKey>? comparer = null) where TKey : notnull
    {
        var 番号 = Info.番号++;
        var Struct = $"Struct{番号}";
        var temp = $"temp{番号}";
        var result = $"result{番号}";
        return キャリア化<TSource>(
            $$"""
                private struct {{Struct}}{
                    {{keySelector.DefineMethod(nameof(keySelector))}}
                }
                """,
            $$"""
                {{first.BeginGlobal}}
                var {{temp}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                global::System.Collections.Generic.IEnumerable<{{Type名<TSource>()}}> {{result}} = default!;
                """,
            $$"""
                {{Listへ実体化<TSource>(first,temp)}}
                {{result}} = global::Seamless.Linq.Enumerable.ExceptBy<{{Type名<TSource>()}},{{Type名<TKey>()}}>({{temp}},{{second}},{{Struct}}.keySelector,{{comparer}});
                """,
            result,
            ref Info
        );
    }
    public static IEnumerable Except<TSource>(this IEnumerable first,string second,ref Info Info,IEqualityComparer<TSource>? comparer = null)
    {
        var 番号 = Info.番号++;
        var temp = $"temp{番号}";
        var result = $"result{番号}";
        return キャリア化<TSource>(
            "",
            $$"""
                {{first.BeginGlobal}}
                var {{temp}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                global::System.Collections.Generic.IEnumerable<{{Type名<TSource>()}}> {{result}} = default!;
                """,
            $$"""
                {{Listへ実体化<TSource>(first,temp)}}
                {{result}} = global::Seamless.Linq.Enumerable.Except<{{Type名<TSource>()}}>({{temp}},{{second}},{{comparer}});
                """,
            result,
            ref Info
        );
    }
    public static Scalar FirstOrDefault<TSource>(this IEnumerable source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        var End = $"End{番号}";
        return new(
            "",
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                {{Type名<TSource>()}} {{result}}=default!;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                {{result}}={{source.Element}};
                goto {{End}};
                """,
            source.EndLoopInside,
            source.EndLoop,
            $$"""
                {{End}}:
                {{source.EndLoopOutside}}
                """,
            source.EndGlobal,
            result
        );
    }
    public static Scalar FirstOrDefault<TSource>(this IEnumerable source,string defaultValue,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        var End = $"End{番号}";
        return new(
            "",
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{result}}={{defaultValue}};
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                {{result}}={{source.Element}};
                goto {{End}};
                """,
            source.EndLoopInside,
            source.EndLoop,
            $$"""
                {{End}}:
                {{source.EndLoopOutside}}
                """,
            source.EndGlobal,
            result
        );
    }
    public static Scalar First<TSource>(this IEnumerable source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        var found = $"found{番号}";
        var End = $"End{番号}";
        return new(
            "",
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{found}}=false;
                {{Type名<TSource>()}} {{result}}=default!;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                {{result}}={{source.Element}};
                {{found}}=true;
                goto {{End}};
                """,
            source.EndLoopInside,
            source.EndLoop,
            $$"""
                {{End}}:
                {{source.EndLoopOutside}}
                """,
            source.EndGlobal,
            $"{found} ? {result} : throw NoElements()"
        );
    }
    public static Scalar First<TSource>(this IEnumerable source, Func<TSource, bool> predicate,ref Info Info)
    {
        var 番号 = Info.番号++;
        var Struct = $"Struct{番号}";
        var result = $"result{番号}";
        var found = $"found{番号}";
        var End = $"End{番号}";
        var Continue = $"Continue{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{predicate.DefineMethod(nameof(predicate))}}
                }
                """,
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{found}}=false;
                {{Type名<TSource>()}} {{result}}=default!;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                if(!{{Struct}}.predicate({{source.Element}}))goto {{Continue}};
                {{result}}={{source.Element}};
                {{found}}=true;
                goto {{End}};
                {{Continue}}:;
                """,
            source.EndLoopInside,
            source.EndLoop,
            $$"""
                {{End}}:
                {{source.EndLoopOutside}}
                """,
            source.EndGlobal,
            $"{found} ? {result} : throw new InvalidOperationException(\"Sequence contains no matching element.\")"
        );
    }
    public static IEnumerable GroupBy<TSource, TKey>(this IEnumerable source, Func<TSource, TKey> keySelector,ref Info Info,IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        var 番号 = Info.番号++;
        var Struct = $"Struct{番号}";
        var lookup = $"lookup{番号}";
        var result = $"result{番号}";
        var item = $"item{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{keySelector.DefineMethod(nameof(keySelector))}}
                }
                """,
            $$"""
                {{source.BeginGlobal}}
                var {{lookup}} = new global::Seamless.Linq.Enumerable.LookupImpl<{{Type名<TKey>()}},{{Type名<TSource>()}}>({{comparer}});
                var {{result}} = new System.Collections.Generic.List<global::System.Linq.IGrouping<{{Type名<TKey>()}},{{Type名<TSource>()}}>>();
                """,
            $$"""
                {{result}}.Clear();
                {{lookup}}.Groups.Clear();
                {{source.BeginLoopOutside}}
                {{source.BeginLoop}}
                    {{source.BeginLoopInside}}
                    {{lookup}}.Add({{Struct}}.keySelector({{source.Element}}),{{source.Element}});
                    {{source.EndLoopInside}}
                {{source.EndLoop}}
                {{source.EndLoopOutside}}
                foreach(var {{item}} in {{lookup}}){{result}}.Add({{item}});
                """,
            $$"""
                foreach(var {{item}} in {{result}}){
                """,
            "",
            "",
            item,
            "",
            $$"""
                }
                """,
            "",
            source.EndGlobal,
            result,
            [],
            ""
        );
    }
    public static IEnumerable GroupBy<TSource, TKey, TElement>(this IEnumerable source, Func<TSource, TKey> keySelector, Func<TSource, TElement> elementSelector,ref Info Info,IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        var 番号 = Info.番号++;
        var Struct = $"Struct{番号}";
        var lookup = $"lookup{番号}";
        var result = $"result{番号}";
        var item = $"item{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{keySelector.DefineMethod(nameof(keySelector))}}
                    {{elementSelector.DefineMethod(nameof(elementSelector))}}
                }
                """,
            $$"""
                {{source.BeginGlobal}}
                var {{lookup}} = new global::Seamless.Linq.Enumerable.LookupImpl<{{Type名<TKey>()}},{{Type名<TElement>()}}>({{comparer}});
                var {{result}} = new System.Collections.Generic.List<global::System.Linq.IGrouping<{{Type名<TKey>()}},{{Type名<TElement>()}}>>();
                """,
            $$"""
                {{result}}.Clear();
                {{lookup}}.Groups.Clear();
                {{source.BeginLoopOutside}}
                {{source.BeginLoop}}
                    {{source.BeginLoopInside}}
                    {{lookup}}.Add({{Struct}}.keySelector({{source.Element}}),{{Struct}}.elementSelector({{source.Element}}));
                    {{source.EndLoopInside}}
                {{source.EndLoop}}
                {{source.EndLoopOutside}}
                foreach(var {{item}} in {{lookup}}){{result}}.Add({{item}});
                """,
            $$"""
                foreach(var {{item}} in {{result}}){
                """,
            "",
            "",
            item,
            "",
            $$"""
                }
                """,
            "",
            source.EndGlobal,
            result,
            [],
            ""
        );
    }
    public static IEnumerable InfiniteSequence<T>(string start,string step,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        return キャリア化<T>(
            "",
            $$"""
                var {{result}} = global::Seamless.Linq.Enumerable.InfiniteSequence<{{Type名<T>()}}>({{start}},{{step}});
                """,
            "",
            result,
            ref Info
        );
    }
    public static IEnumerable IntersectBy<TSource, TKey>(this IEnumerable first,string second,Func<TSource, TKey> keySelector,ref Info Info,IEqualityComparer<TKey>? comparer = null) where TKey : notnull
    {
        var 番号 = Info.番号++;
        var Struct = $"Struct{番号}";
        var temp = $"temp{番号}";
        var result = $"result{番号}";
        return キャリア化<TSource>(
            $$"""
                private struct {{Struct}}{
                    {{keySelector.DefineMethod(nameof(keySelector))}}
                }
                """,
            $$"""
                {{first.BeginGlobal}}
                var {{temp}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                global::System.Collections.Generic.IEnumerable<{{Type名<TSource>()}}> {{result}} = default!;
                """,
            $$"""
                {{Listへ実体化<TSource>(first,temp)}}
                {{result}} = global::Seamless.Linq.Enumerable.IntersectBy<{{Type名<TSource>()}},{{Type名<TKey>()}}>({{temp}},{{second}},{{Struct}}.keySelector,{{comparer}});
                """,
            result,
            ref Info
        );
    }
    public static IEnumerable Intersect<TSource>(this IEnumerable first,string second,ref Info Info,IEqualityComparer<TSource>? comparer = null)
    {
        var 番号 = Info.番号++;
        var temp = $"temp{番号}";
        var result = $"result{番号}";
        return キャリア化<TSource>(
            "",
            $$"""
                {{first.BeginGlobal}}
                var {{temp}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                global::System.Collections.Generic.IEnumerable<{{Type名<TSource>()}}> {{result}} = default!;
                """,
            $$"""
                {{Listへ実体化<TSource>(first,temp)}}
                {{result}} = global::Seamless.Linq.Enumerable.Intersect<{{Type名<TSource>()}}>({{temp}},{{second}},{{comparer}});
                """,
            result,
            ref Info
        );
    }
    public static Scalar LastOrDefault<TSource>(this IEnumerable source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        return new(
            "",
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                {{Type名<TSource>()}} {{result}}=default!;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                {{result}}={{source.Element}};
                """,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            result
        );
    }
    public static Scalar Last<TSource>(this IEnumerable source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        var found = $"found{番号}";
        var Continue = $"Continue{番号}";
        return new(
            "",
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{found}}=false;
                {{Type名<TSource>()}} {{result}}=default!;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                {{result}}={{source.Element}};
                {{found}}=true;
                """,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            $"{found} ? {result} : throw NoElements()"
        );
    }
    public static Scalar LongCount<TSource>(this IEnumerable source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        return new(
            "",
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                long {{result}}=0;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                checked{
                    {{result}}++;
                }
                """,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            result
        );
    }
    public static Scalar Max(this IEnumerable<int> source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        var found = $"found{番号}";
        var Continue = $"Continue{番号}";
        return new(
            "",
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{found}}=false;
                int {{result}}=default;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                if(!{{found}}){
                    {{result}}={{source.Element}};
                    {{found}}=true;
                    goto {{Continue}};
                }
                if({{source.Element}}>{{result}}){{result}}={{source.Element}};
                {{Continue}}:;
                """,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            $"{found} ? {result} : throw NoElements()"
        );
    }
    public static Scalar Min(this IEnumerable<int> source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        var found = $"found{番号}";
        var Continue = $"Continue{番号}";
        return new(
            "",
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{found}}=false;
                int {{result}}=default;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                if(!{{found}}){
                    {{result}}={{source.Element}};
                    {{found}}=true;
                    goto {{Continue}};
                }
                if({{source.Element}}<{{result}}){{result}}={{source.Element}};
                {{Continue}}:;
                """,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            $"{found} ? {result} : throw NoElements()"
        );
    }
    public static IEnumerable OfType<TResult>(this IEnumerable source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var temp = $"temp{番号}";
        var typed = $"typed{番号}";
        return キャリア化<TResult>(
            "",
            $$"""
                {{source.BeginGlobal}}
                var {{temp}} = new System.Collections.Generic.List<{{Type名<TResult>()}}>();
                """,
            $$"""
                {{temp}}.Clear();
                {{source.BeginLoopOutside}}
                {{source.BeginLoop}}
                    {{source.BeginLoopInside}}
                    if({{source.Element}} is {{Type名<TResult>()}} {{typed}}){
                        {{temp}}.Add({{typed}});
                    }
                    {{source.EndLoopInside}}
                {{source.EndLoop}}
                {{source.EndLoopOutside}}
                """,
            temp,
            ref Info
        );
    }
    public static IEnumerable OrderByDescending<TSource, TKey>(this IEnumerable source, Func<TSource, TKey> keySelector,ref Info Info,IComparer<TKey>? comparer = null)
    {
        var 番号 = Info.番号++;
        var Struct = $"Struct{番号}";
        var buffer = $"buffer{番号}";
        var result = $"result{番号}";
        var item = $"item{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{keySelector.DefineMethod(nameof(keySelector))}}
                }
                """,
            $$"""
                {{source.BeginGlobal}}
                var {{buffer}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                global::System.Linq.IOrderedEnumerable<{{Type名<TSource>()}}> {{result}} = default!;
                """,
            $$"""
                {{buffer}}.Clear();
                {{source.BeginLoopOutside}}
                {{source.BeginLoop}}
                    {{source.BeginLoopInside}}
                    {{buffer}}.Add({{source.Element}});
                    {{source.EndLoopInside}}
                {{source.EndLoop}}
                {{source.EndLoopOutside}}
                {{result}} = global::Seamless.Linq.Enumerable.OrderByDescending<{{Type名<TSource>()}},{{Type名<TKey>()}}>({{buffer}},{{Struct}}.keySelector,{{comparer}});
                """,
            $$"""
                foreach(var {{item}} in {{result}}){
                """,
            "",
            "",
            item,
            "",
            $$"""
                }
                """,
            "",
            source.EndGlobal,
            result,
            [],
            ""
        );
    }
    public static IEnumerable OrderBy<TSource, TKey>(this IEnumerable source, Func<TSource, TKey> keySelector,ref Info Info,IComparer<TKey>? comparer = null)
    {
        var 番号 = Info.番号++;
        var Struct = $"Struct{番号}";
        var buffer = $"buffer{番号}";
        var result = $"result{番号}";
        var item = $"item{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{keySelector.DefineMethod(nameof(keySelector))}}
                }
                """,
            $$"""
                {{source.BeginGlobal}}
                var {{buffer}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                global::System.Linq.IOrderedEnumerable<{{Type名<TSource>()}}> {{result}} = default!;
                """,
            $$"""
                {{buffer}}.Clear();
                {{source.BeginLoopOutside}}
                {{source.BeginLoop}}
                    {{source.BeginLoopInside}}
                    {{buffer}}.Add({{source.Element}});
                    {{source.EndLoopInside}}
                {{source.EndLoop}}
                {{source.EndLoopOutside}}
                {{result}} = global::Seamless.Linq.Enumerable.OrderBy<{{Type名<TSource>()}},{{Type名<TKey>()}}>({{buffer}},{{Struct}}.keySelector,{{comparer}});
                """,
            $$"""
                foreach(var {{item}} in {{result}}){
                """,
            "",
            "",
            item,
            "",
            $$"""
                }
                """,
            "",
            source.EndGlobal,
            result,
            [],
            ""
        );
    }
    public static IEnumerable Prepend<TSource>(this IEnumerable source,string element,ref Info Info)
    {
        var 番号 = Info.番号++;
        var temp = $"temp{番号}";
        return キャリア化<TSource>(
            "",
            $$"""
                {{source.BeginGlobal}}
                var {{temp}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                """,
            $$"""
                {{temp}}.Clear();
                {{temp}}.Add({{element}});
                {{source.BeginLoopOutside}}
                {{source.BeginLoop}}
                    {{source.BeginLoopInside}}
                    {{temp}}.Add({{source.Element}});
                    {{source.EndLoopInside}}
                {{source.EndLoop}}
                {{source.EndLoopOutside}}
                """,
            temp,
            ref Info
        );
    }
    public static IEnumerable Range(string start,string count,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        return キャリア化<int>(
            "",
            $$"""
                var {{result}} = global::Seamless.Linq.Enumerable.Range({{start}},{{count}});
                """,
            "",
            result,
            ref Info
        );
    }
    public static IEnumerable Repeat<TResult>(string element,string count,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        return キャリア化<TResult>(
            "",
            $$"""
                var {{result}} = global::Seamless.Linq.Enumerable.Repeat<{{Type名<TResult>()}}>({{element}},{{count}});
                """,
            "",
            result,
            ref Info
        );
    }
    public static IEnumerable Reverse<TSource>(this IEnumerable source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var temp = $"temp{番号}";
        return キャリア化<TSource>(
            "",
            $$"""
                {{source.BeginGlobal}}
                var {{temp}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                """,
            $$"""
                {{Listへ実体化<TSource>(source,temp)}}
                {{temp}}.Reverse();
                """,
            temp,
            ref Info
        );
    }
    public static IEnumerable SelectMany<TSource, TResult>(this IEnumerable<TSource> source,SelectMany_selector selector,Info Info)
    {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        var Struct=$"Struct{番号}";
        var Element= $"output{番号}";
        string staticメソッド,BeginLoopInside,EndLoopInside;
        if(selector.Source is null){
            staticメソッド=$$"""
                private struct {{Struct}}{
                    {{Info.DefineFields}}
                    {{selector.DefineMethod(nameof(selector))}}
                }
                """;
            BeginLoopInside=$$"""
                {{source.BeginLoopInside}}
                foreach(var {{Element}} in {{Struct}}.selector({{source.Element}}){
            """;
            EndLoopInside=$$"""
                }
                {{source.EndLoopInside}}
            """;
        }else{
            staticメソッド="";
            BeginLoopInside=$$"""
                {{source.BeginLoopInside}}
                {{selector.Source.BeginLoopOutside}}
                {{selector.Source.BeginLoop}}
                    {{selector.Source.BeginLoopInside}}
            """;
            EndLoopInside=$$"""
                    {{selector.Source.BeginLoopInside}}
                {{selector.Source.BeginLoop}}
                {{selector.Source.BeginLoopInside}}
                {{source.EndLoopInside}}
            """;
        }
        return new(
            staticメソッド,
            source.BeginGlobal,
            source.BeginLoopOutside,
            source.BeginLoop,
            BeginLoopInside,
            "",
            Element,
            EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            result,
            [],
            source.MoveNext
        );
    }
    public static IEnumerable Select<TSource, TResult>(this IEnumerable source, Func<TSource, TResult> selector,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        var Struct=$"Struct{番号}";
        var Element=$"selectorItem{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{selector.DefineMethod(nameof(selector))}}
                }
                """,
            $$"""
                {{source.BeginGlobal}}
                var {{result}} = new System.Collections.Generic.List<{{Type名<TResult>()}}>();
                """,
            $$"""
                {{source.BeginLoopOutside}}
                {{result}}.Clear();
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                var {{Element}} = {{Struct}}.selector({{source.Element}});
                {{result}}.Add({{Element}});
                """,
            "",
            Element,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            result,
            [],
            source.MoveNext
        );
    }
    public static IEnumerable Select<TSource, TResult>(this IEnumerable source, Func<TSource, int, TResult> selector,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        var Struct=$"Struct{番号}";
        var Element=$"selectorItem{番号}";
        var index=$"index{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{selector.DefineMethod(nameof(selector))}}
                }
                """,
            $$"""
                {{source.BeginGlobal}}
                var {{result}} = new System.Collections.Generic.List<{{Type名<TResult>()}}>();
                """,
            $$"""
                {{source.BeginLoopOutside}}
                {{result}}.Clear();
                var {{index}} = -1;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                checked{
                    {{index}}++;
                }
                var {{Element}} = {{Struct}}.selector({{source.Element}},{{index}});
                """,
            $$"""
                {{result}}.Add({{Element}});
                """,
            Element,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            result,
            [],
            source.MoveNext
        );
    }
    public static IEnumerable Sequence<T>(string start,string endInclusive,string step,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        return キャリア化<T>(
            "",
            $$"""
                var {{result}} = global::Seamless.Linq.Enumerable.Sequence<{{Type名<T>()}}>({{start}},{{endInclusive}},{{step}});
                """,
            "",
            result,
            ref Info
        );
    }
    public static Scalar SequenceEqual<TSource>(this IEnumerable first,string second,ref Info Info,IEqualityComparer<TSource>? comparer = null)
    {
        var 番号 = Info.番号++;
        var temp = $"temp{番号}";
        return new(
            "",
            $$"""
                {{first.BeginGlobal}}
                var {{temp}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                """,
            $$"""
                {{Listへ実体化<TSource>(first,temp)}}
                """,
            "",
            "",
            "",
            "",
            "",
            first.EndGlobal,
            $"global::Seamless.Linq.Enumerable.SequenceEqual<{Type名<TSource>()}>({temp},{second},{comparer})"
        );
    }
    public static IEnumerable SkipWhile<TSource>(this IEnumerable source, Func<TSource, bool> predicate,ref Info Info)
    {
        var 番号 = Info.番号++;
        var Struct = $"Struct{番号}";
        var temp = $"temp{番号}";
        var output = $"output{番号}";
        var skipping = $"skipping{番号}";
        var Start = $"Start{番号}";
        var End = $"End{番号}";
        var enumerator = $"e{番号}";
        return キャリア化<TSource>(
            $$"""
                private struct {{Struct}}{
                    {{predicate.DefineMethod(nameof(predicate))}}
                }
                """,
            $$"""
                {{source.BeginGlobal}}
                var {{temp}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                var {{output}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                """,
            $$"""
                var {{skipping}}=true;
                {{Listへ実体化<TSource>(source,temp)}}
                {{output}}.Clear();
                var {{enumerator}}={{temp}}.GetEnumerator();
                {{Start}}:;
                if(!{{enumerator}}.MoveNext())goto {{End}};
                if({{skipping}}&&{{Struct}}.predicate({{enumerator}}.Current))goto {{Start}};
                {{skipping}}=false;
                {{output}}.Add({{enumerator}}.Current);
                goto {{Start}};
                {{End}}:;
                """,
            output,
            ref Info
        );
    }
    public static IEnumerable Skip<TSource>(this IEnumerable source,string count,ref Info Info)
    {
        var 番号 = Info.番号++;
        var temp = $"temp{番号}";
        var output = $"output{番号}";
        var remaining = $"remaining{番号}";
        var Start = $"Start{番号}";
        var End = $"End{番号}";
        var enumerator = $"e{番号}";
        return キャリア化<TSource>(
            "",
            $$"""
                {{source.BeginGlobal}}
                var {{temp}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                var {{output}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                """,
            $$"""
                var {{remaining}}={{count}};
                if({{remaining}}<0){{remaining}}=0;
                {{Listへ実体化<TSource>(source,temp)}}
                {{output}}.Clear();
                var {{enumerator}}={{temp}}.GetEnumerator();
                {{Start}}:;
                if(!{{enumerator}}.MoveNext())goto {{End}};
                if({{remaining}}>0){
                    {{remaining}}--;
                    goto {{Start}};
                }
                {{output}}.Add({{enumerator}}.Current);
                goto {{Start}};
                {{End}}:;
                """,
            output,
            ref Info
        );
    }
    public static Scalar Sum(this IEnumerable<int> source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        return new(
            "",
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                int {{result}}=0;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                checked{
                    {{result}}+={{source.Element}};
                }
                """,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            result
        );
    }
    public static Scalar Sum<TSource>(this IEnumerable source,Func<TSource,int> selector,ref Info Info)
    {
        var 番号 = Info.番号++;
        var Struct = $"Struct{番号}";
        var result = $"result{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{selector.DefineMethod(nameof(selector))}}
                }
                """,
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                int {{result}}=0;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                checked{
                    {{result}}+={{Struct}}.selector({{source.Element}});
                }
                """,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            result
        );
    }
    public static IEnumerable TakeWhile<TSource>(this IEnumerable source, Func<TSource, bool> predicate,ref Info Info)
    {
        var 番号 = Info.番号++;
        var Struct = $"Struct{番号}";
        var temp = $"temp{番号}";
        var output = $"output{番号}";
        var End = $"End{番号}";
        var Start = $"Start{番号}";
        var enumerator = $"e{番号}";
        return キャリア化<TSource>(
            $$"""
                private struct {{Struct}}{
                    {{predicate.DefineMethod(nameof(predicate))}}
                }
                """,
            $$"""
                {{source.BeginGlobal}}
                var {{temp}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                var {{output}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                """,
            $$"""
                {{Listへ実体化<TSource>(source,temp)}}
                {{output}}.Clear();
                var {{enumerator}}={{temp}}.GetEnumerator();
                {{Start}}:;
                if(!{{enumerator}}.MoveNext())goto {{End}};
                if(!{{Struct}}.predicate({{enumerator}}.Current))goto {{End}};
                {{output}}.Add({{enumerator}}.Current);
                goto {{Start}};
                {{End}}:;
                """,
            output,
            ref Info
        );
    }
    public static IEnumerable Take<TSource>(this IEnumerable source,string count,ref Info Info)
    {
        var 番号 = Info.番号++;
        var temp = $"temp{番号}";
        var output = $"output{番号}";
        var remaining = $"remaining{番号}";
        var End = $"End{番号}";
        var Start = $"Start{番号}";
        var enumerator = $"e{番号}";
        return キャリア化<TSource>(
            "",
            $$"""
                {{source.BeginGlobal}}
                var {{temp}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                var {{output}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                """,
            $$"""
                var {{remaining}}={{count}};
                {{Listへ実体化<TSource>(source,temp)}}
                {{output}}.Clear();
                if({{remaining}}<=0)goto {{End}};
                var {{enumerator}}={{temp}}.GetEnumerator();
                {{Start}}:;
                if(!{{enumerator}}.MoveNext())goto {{End}};
                {{output}}.Add({{enumerator}}.Current);
                {{remaining}}--;
                if({{remaining}}<=0)goto {{End}};
                goto {{Start}};
                {{End}}:;
                """,
            output,
            ref Info
        );
    }
    public static IEnumerable ThenByDescending<TSource, TKey>(this IEnumerable source, Func<TSource, TKey> keySelector,ref Info Info,IComparer<TKey>? comparer = null)
    {
        var 番号 = Info.番号++;
        var Struct = $"Struct{番号}";
        var result = $"result{番号}";
        var item = $"item{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{keySelector.DefineMethod(nameof(keySelector))}}
                }
                """,
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{result}} = global::Seamless.Linq.Enumerable.ThenByDescending<{{Type名<TSource>()}},{{Type名<TKey>()}}>((global::System.Linq.IOrderedEnumerable<{{Type名<TSource>()}}>){{source.result}},{{Struct}}.keySelector,{{comparer}});
                """,
            $$"""
                foreach(var {{item}} in {{result}}){
                """,
            "",
            "",
            item,
            "",
            $$"""
                }
                """,
            "",
            source.EndGlobal,
            result,
            [],
            ""
        );
    }
    public static IEnumerable ThenBy<TSource, TKey>(this IEnumerable source, Func<TSource, TKey> keySelector,ref Info Info,IComparer<TKey>? comparer = null)
    {
        var 番号 = Info.番号++;
        var Struct = $"Struct{番号}";
        var result = $"result{番号}";
        var item = $"item{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{keySelector.DefineMethod(nameof(keySelector))}}
                }
                """,
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{result}} = global::Seamless.Linq.Enumerable.ThenBy<{{Type名<TSource>()}},{{Type名<TKey>()}}>((global::System.Linq.IOrderedEnumerable<{{Type名<TSource>()}}>){{source.result}},{{Struct}}.keySelector,{{comparer}});
                """,
            $$"""
                foreach(var {{item}} in {{result}}){
                """,
            "",
            "",
            item,
            "",
            $$"""
                }
                """,
            "",
            source.EndGlobal,
            result,
            [],
            ""
        );
    }
    public static Scalar ToArray<TSource>(this IEnumerable source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var buffer = $"buffer{番号}";
        return new(
            "",
            $$"""
                {{source.BeginGlobal}}
                var {{buffer}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                """,
            $$"""
                {{source.BeginLoopOutside}}
                {{buffer}}.Clear();
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                {{buffer}}.Add({{source.Element}});
                """,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            $"{buffer}.ToArray()"
        );
    }

    public static Scalar ToList<TSource>(this IEnumerable source,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        return new(
            "",
            $$"""
                {{source.BeginGlobal}}
                var {{result}} = new System.Collections.Generic.List<{{Type名<TSource>()}}>();
                """,
            $$"""
                {{source.BeginLoopOutside}}
                {{result}}.Clear();
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                {{result}}.Add({{source.Element}});
                """,
            source.EndLoopInside,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            result
        );
    }
    public static IEnumerable UnionBy<TSource, TKey>(this IEnumerable first, IEnumerable second, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer,ref Info Info)
    {
        var 番号 = Info.番号++;
        var Struct=$"Struct{番号}";
        var seen=$"seen{番号}";
        var result=$"result{番号}";
        var item=$"item{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{keySelector.DefineMethod(nameof(keySelector))}}
                }
                """,
            $$"""
                var {{seen}} = new System.Collections.Generic.HashSet<{{typeof(TKey).FullName}}>({{comparer}});
                var {{result}} = new System.Collections.Generic.HashSet<{{typeof(TSource).FullName}}>();
                """,
            $$"""
                    foreach({{item}} in {{first }})_={{seen}}.Add({{Struct}}.keySelector({{item}}))&&{{result}}.Add({{item}});
                    foreach({{item}} in {{second}})_={{seen}}.Add({{Struct}}.keySelector({{item}}))&&{{result}}.Add({{item}});
                """,
            $$"""
                foreach(var {{item}} in {{result}}){
                """,
            "",
            "",
            item,
            "",
            $$"""
                }
                """,
            "",
            "",
            result,
            [],
            ""
        );
    }
    public static IEnumerable Union<TSource>(this IEnumerable first, IEnumerable second,ref Info Info)
    {
        var 番号 = Info.番号++;
        var temp=$"temp{番号}";
        var result=$"result{番号}";
        var item=$"item{番号}";
        return new(
            "",
            $$"""
                var {{temp}} = new System.Collections.Generic.HashSet<{{typeof(TSource).FullName}}>();
                var {{result}} = new System.Collections.Generic.HashSet<{{typeof(TSource).FullName}}>();
                """,
            $$"""
                {{temp}}.Clear();
                {{result}}.Clear();
                foreach(var {{item}} in {{first}}){{temp}}.Add({{item}});
                foreach(var {{item}} in {{second}}){{temp}}.Add({{item}});
                """,
            $$"""
                foreach(var {{item}} in {{temp}}){
                """,
            "",
            $$"""
                {{result}}.Add({{item}});
                """,
            item,
            "",
            $$"""
                }
                """,
            "",
            "",
            result,
            [],
            ""
        );
    }
    public static IEnumerable Union<TSource>(this IEnumerable first, IEnumerable second, IEqualityComparer<TSource>? comparer,ref Info Info) {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        var e=$"item{番号}";
        var MoveNext = $"{e}.MoveNext()";
        var Head= $"Head{番号}";
        var Start= $"Start{番号}";
        return new(
            "",
            $$"""
                var {{result}} = new System.Collections.Generic.HashSet<{{typeof(TSource).FullName}}>({{comparer}});
                """,
            $$"""
                {{result}}.Clear();
                {{first.BeginLoopOutside}}
                {{first.BeginLoop}}
                    {{first.BeginLoopInside}}
                    {{result}}.Add({{first.Element}});
                    {{first.EndLoopInside}}
                {{first.EndLoop}}
                {{first.EndLoopOutside}}
                {{second.BeginLoopOutside}}
                {{second.BeginLoop}}
                    {{second.BeginLoopInside}}
                    {{result}}.Add({{second.Element}});
                    {{second.EndLoopInside}}
                {{second.EndLoop}}
                {{second.EndLoopOutside}}
                """,
            $$"""
                var {{e}}={{result}}.GetEnumerator();
                goto {{Start}};
                {{Head}}:
                """,
            "",
            $$"""
                {{result}}.Add({{e}}.Current);
            """,
            $"{e}.Current",
            "",
            $$"""
                if({{MoveNext}})goto {{Head}};
                """,
            "",
            "",
            result,
            [],
            MoveNext
        );
    }
    public static IEnumerable Where<TSource>(this IEnumerable source, Func<TSource, bool> predicate,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        var Struct=$"Struct{番号}";
        var Continue = $"Continue{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{predicate.DefineMethod(nameof(predicate))}}
                }
                """,
            source.BeginGlobal,
            source.BeginLoopOutside,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                if(!{{Struct}}.predicate({{source.Element}}))goto {{Continue}};
            """,
            $$"""
                {{result}}.Add({{source.Element}});
            """,
            source.Element,
            $$"""
                {{Continue}}:;
                """,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            result,
            [],
            source.MoveNext
        );
    }
    public static IEnumerable Where<TSource>(this IEnumerable source, Func<TSource, int, bool> predicate,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result=$"result{番号}";
        var Struct=$"Struct{番号}";
        var Continue = $"Continue{番号}";
        var index=$"Index{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{predicate.DefineMethod(nameof(predicate))}}
                }
                """,
            source.BeginGlobal,
            $$"""
                {{source.BeginLoopOutside}}
                var {{index}}=-1;
                """,
            source.BeginLoop,
            $$"""
                {{source.BeginLoopInside}}
                checked{
                    {{index}}++;
                }
                if(!{{Struct}}.predicate({{source.Element}},{{index}}))goto {{Continue}};
                {{result}}.Add({{source.Element}});
                """,
            "",
            source.Element,
            $$"""
                {{Continue}}:;
                {{source.EndLoopInside}}
                """,
            source.EndLoop,
            source.EndLoopOutside,
            source.EndGlobal,
            result,
            [],
            source.MoveNext
        );
    }
    public static IEnumerable Zip<TFirst, TSecond>(this IEnumerable first, IEnumerable second,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        var Head = $"Head{番号}";
        var End = $"End{番号}";
        return new(
            "",
            "",
            $$"""
                var {{result}}= new List<({{typeof(TFirst).FullName}},{{typeof(TSecond).FullName}})>();
                """,
            $$"""
                {{first.BeginLoopOutside}}
                {{second.BeginLoopOutside}}
                {{Head}}:
                {{first.BeginLoopInside}}
                {{first.If_Not_MoveNext_goto(End)}}
                {{second.BeginLoopInside}}
                {{second.If_Not_MoveNext_goto(End)}}
                """,
            $$"""
                {{result}}.Add(({{first.Element}}, {{second.Element}}));
                """,
            "",
            $"({first.Element}, {second.Element})",
            "",
            $$"""
                {{first.EndLoopInside}}
                {{second.EndLoopInside}}
                goto {{Head}};
                {{End}}:
                {{first.EndLoopOutside}}
                {{second.EndLoopOutside}}
                """,
            "",
            "",
            result,
            [],
            ""
        );
    }
    public static IEnumerable Zip<TFirst, TSecond, TThird>(this IEnumerable first, IEnumerable second, IEnumerable third,ref Info Info)
    {
        var 番号 = Info.番号++;
        var result = $"result{番号}";
        var Head = $"Head{番号}";
        var End = $"End{番号}";
        return new(
            "",
            "",
            $$"""
                var {{result}}= new List<({{typeof(TFirst).FullName}},{{typeof(TSecond).FullName}},{{typeof(TThird).FullName}})>();
                """,
            $$"""
                {{first.BeginLoopOutside}}
                {{second.BeginLoopOutside}}
                {{third.BeginLoopOutside}}
                {{Head}}:
                {{first.BeginLoopInside}}
                {{first.If_Not_MoveNext_goto(End)}}
                {{second.BeginLoopInside}}
                {{second.If_Not_MoveNext_goto(End)}}
                {{third.BeginLoopInside}}
                {{third.If_Not_MoveNext_goto(End)}}
                """,
            $$"""
                {{result}}.Add(({{first.Element}}, {{second.Element}}, {{third.Element}}));
                """,
            "",
            $"({first.Element}, {second.Element}, {third.Element})",
            "",
            $$"""
                {{first.EndLoopInside}}
                {{second.EndLoopInside}}
                {{third.EndLoopInside}}
                goto {{Head}};
                {{End}}:
                {{first.EndLoopOutside}}
                {{second.EndLoopOutside}}
                {{third.EndLoopOutside}}
                """,
            "",
            "",
            result,
            [],
            ""
        );
    }
    public static IEnumerable Zip<TFirst, TSecond, TResult>(this IEnumerable first, IEnumerable second, Func<TFirst, TSecond, TResult> resultSelector,ref Info Info)
    {
        var 番号 = Info.番号++;
        var Struct = $"Struct{番号}";
        var result = $"result{番号}";
        var Head = $"Head{番号}";
        var End = $"End{番号}";
        return new(
            $$"""
                private struct {{Struct}}{
                    {{resultSelector.DefineMethod(nameof(resultSelector))}}
                }
                """,
            "",
            $$"""
                var {{result}}= new List<{{typeof(TResult).FullName}}>();
                """,
            $$"""
                {{first.BeginLoopOutside}}
                {{second.BeginLoopOutside}}
                {{Head}}:
                {{first.BeginLoopInside}}
                {{first.If_Not_MoveNext_goto(End)}}
                {{second.BeginLoopInside}}
                {{second.If_Not_MoveNext_goto(End)}}
                """,
            $$"""
                {{result}}.Add({{Struct}}.resultSelector({{first.Element}}, {{second.Element}}));
                """,
            "",
            $"{Struct}.resultSelector({first.Element}, {second.Element})",
            "",
            $$"""
                {{first.EndLoopInside}}
                {{second.EndLoopInside}}
                goto {{Head}};
                {{End}}:
                {{first.EndLoopOutside}}
                {{second.EndLoopOutside}}
                """,
            "",
            "",
            result,
            [],
            ""
        );
    }
    // public static Enumerable Union(this Enumerable first, Enumerable second,ref Info Info)
    // {
    //     var 番号 = Info.番号++;
    //     var result = $"result{番号}";
    //     var e = $"e{番号}";
    //     return new Enumerable
    //     {
    //         while前 = $$"""
    //             {{source.while前}}
    //             var {{result}}=new HashSet<{{first.T.FullName}}>();
    //             {{first.while前}}
    //             {{first.while先頭}}
    //                 {{result}}.Add({{first.Element}});
    //             {{first.while末尾}}
    //             {{first.while後}}
    //             {{second.while前}}
    //             {{second.while先頭}}
    //                 {{result}}.Add({{second.Element}});
    //             {{second.while末尾}}
    //             {{second.while後}}
    //             var {{e}}={{result}}.GetEnumerator();
    //             """,
    //         while先頭 = $$"""
    //             while({{e}}.MoveNext()){
    //             """,
    //         Element = $"{e}.Current",
    //         while末尾 = $$"""
    //             }
    //             """,
    //         while後 = source.while後
    //     };
    // }
    // public static Enumerable Join(this Enumerable outer, Enumerable inner, Lambda outerKeySelector, Lambda innerKeySelector, Lambda resultSelector)
    // {
    //     var 番号 = Info.番号++;
    //     var lookup = $"lookup{番号}";
    //     //var outer = $"outer{番号}";
    //     var innerEnumerator = $"innerEnumerator{番号}";
    //     var outerKey = $"outerKey{番号}";
    //     var innerKey = $"innerKey{番号}";
    //     var innerElement = $"innerElement{番号}";

    //     return new Enumerable
    //     {
    //         while前 = $$"""
    //             {{outer.while前}}
    //             var {{lookup}} = new LookupImpl<object, {{inner.T.FullName}}>(EqualityComparer<object>.Default);
    //             {{inner.while前}}
    //             {{inner.while先頭}}
    //                 {{lookup}}.Add(struct{{番号}}.innerKeySelector({{inner.Element}})!, {{inner.Element}});
    //             {{inner.while末尾}}
    //             {{inner.while後}}
    //             var {{outer}} = {{outer.while前}};
    //             {{outer.while先頭}}
    //             var {{outerKey}} = struct{{番号}}.outerKeySelector({{outer.Element}})!;
    //             var {{innerEnumerator}} = {{lookup}}[{{outerKey}}].GetEnumerator();
    //             while({{innerEnumerator}}.MoveNext()) {
    //                 var {{innerElement}} = {{innerEnumerator}}.Current;
    //                 var result = struct{{番号}}.resultSelector({{outer.Element}}, {{innerElement}});
    //                 return result;
    //             }
    //             {{outer.末尾}}
    //             {{outer.while末尾}}
    //             {{outer.while後}}
    //             """,
    //         while先頭 = "",
    //         Element = "result",
    //         末尾 = "",
    //         while後 = ""
    //     };
    // }
}
