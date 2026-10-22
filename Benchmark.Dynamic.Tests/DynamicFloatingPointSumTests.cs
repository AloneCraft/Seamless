using System.Numerics;
using System.Reflection;
using System.Text;
using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Worker;

namespace Benchmark.Dynamic.Tests;

/// <summary>
/// double / float の Sum・Average が System.Linq と FLinq(最適化なし・あり)でビット単位で一致するかを、
/// 実際に FLinq の生成器を通して確かめる。2026-10-22 の実測では、次の2つが System.Linq とずれる(直さず記録している)。
///  1. double の Sum(選択子なし、配列・List): FLinq は SumSpan(SIMD、4並列の部分和を最後に足す)で、
///     System.Linq は順に足す。足す順が違うので最下位ビットがずれる。
///  2. float の Sum: System.Linq は double で足してから float へ戻すが、FLinq は float のまま足す。
/// 選択子つきの Sum と Average は一致する。整数値だけの入力は(厳密に足せるので)どちらも一致する。
/// 直ったら、ずれる側の期待を反転する。
/// 精度の違い(80ビットと64ビット)ではない: 64ビットの x64 では double の計算はどの経路も64ビットで、差は足す順と蓄積の型から来る。
/// </summary>
public sealed class DynamicFloatingPointSumTests{
    private const string Source="""
#nullable enable
using System.Collections.Generic;
using System.Linq;
namespace Exp;

public sealed class Probe{
    public static global::Benchmark.Dynamic.Core.DynamicQueryValue RunSystem(object? p,object? s)=>global::Benchmark.Dynamic.Core.DynamicQueryValue.Scalar(new global::Benchmark.Dynamic.Core.DoubleQueryAtom(global::System.Linq.Enumerable.Sum((double[])p!)));
    public static global::Benchmark.Dynamic.Core.DynamicQueryValue RunFLinqUnoptimized(object? p,object? s)=>RunFLinqUnoptimizedCore(p,s);
    [global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.None)]
    private static global::Benchmark.Dynamic.Core.DynamicQueryValue RunFLinqUnoptimizedCore(object? p,object? s)=>global::Benchmark.Dynamic.Core.DynamicQueryValue.Scalar(new global::Benchmark.Dynamic.Core.DoubleQueryAtom(((double[])p!).Sum()));
    public static global::Benchmark.Dynamic.Core.DynamicQueryValue RunFLinq(object? p,object? s)=>RunFLinqCore(p,s);
    [global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.All,ConvergenceMode=global::Seamless.FLinq.LinqConvergenceMode.GlobalFixedPoint,EmitOptimizationTrace=true)]
    private static global::Benchmark.Dynamic.Core.DynamicQueryValue RunFLinqCore(object? p,object? s)=>global::Benchmark.Dynamic.Core.DynamicQueryValue.Scalar(new global::Benchmark.Dynamic.Core.DoubleQueryAtom(((double[])p!).Sum()));

    // ---- 実測用 (System / 最適化なし / 最適化あり) ----
    public static double SysDoubleSum(double[] a)=>global::System.Linq.Enumerable.Sum(a);
    public static double UnoDoubleSum(double[] a)=>UnoDoubleSumCore(a);
    [global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.None)]
    private static double UnoDoubleSumCore(double[] a)=>a.Sum();
    public static double OptDoubleSum(double[] a)=>OptDoubleSumCore(a);
    [global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.All)]
    private static double OptDoubleSumCore(double[] a)=>a.Sum();

    public static double SysDoubleSumSel(double[] a)=>global::System.Linq.Enumerable.Sum(a,static p=>p*1.0000001);
    public static double UnoDoubleSumSel(double[] a)=>UnoDoubleSumSelCore(a);
    [global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.None)]
    private static double UnoDoubleSumSelCore(double[] a)=>a.Sum(static p=>p*1.0000001);
    public static double OptDoubleSumSel(double[] a)=>OptDoubleSumSelCore(a);
    [global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.All)]
    private static double OptDoubleSumSelCore(double[] a)=>a.Sum(static p=>p*1.0000001);

    public static double SysListSum(List<double> a)=>global::System.Linq.Enumerable.Sum(a);
    public static double UnoListSum(List<double> a)=>UnoListSumCore(a);
    [global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.None)]
    private static double UnoListSumCore(List<double> a)=>a.Sum();
    public static double OptListSum(List<double> a)=>OptListSumCore(a);
    [global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.All)]
    private static double OptListSumCore(List<double> a)=>a.Sum();

    public static double SysDoubleAvg(double[] a)=>global::System.Linq.Enumerable.Average(a);
    public static double UnoDoubleAvg(double[] a)=>UnoDoubleAvgCore(a);
    [global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.None)]
    private static double UnoDoubleAvgCore(double[] a)=>a.Average();
    public static double OptDoubleAvg(double[] a)=>OptDoubleAvgCore(a);
    [global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.All)]
    private static double OptDoubleAvgCore(double[] a)=>a.Average();

    public static float SysFloatSum(float[] a)=>global::System.Linq.Enumerable.Sum(a);
    public static float UnoFloatSum(float[] a)=>UnoFloatSumCore(a);
    [global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.None)]
    private static float UnoFloatSumCore(float[] a)=>a.Sum();
    public static float OptFloatSum(float[] a)=>OptFloatSumCore(a);
    [global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.All)]
    private static float OptFloatSumCore(float[] a)=>a.Sum();

    public static double SysFloatAvg(float[] a)=>global::System.Linq.Enumerable.Average(a);
    public static double UnoFloatAvg(float[] a)=>UnoFloatAvgCore(a);
    [global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.None)]
    private static double UnoFloatAvgCore(float[] a)=>a.Average();
    public static double OptFloatAvg(float[] a)=>OptFloatAvgCore(a);
    [global::Seamless.FLinq.LinqIntercept(true,Optimizations=global::Seamless.FLinq.LinqOptimizations.All)]
    private static double OptFloatAvgCore(float[] a)=>a.Average();
}
""";

    [Fact,Trait("Category","Integration")]
    public void 浮動小数点のSumとAverageのずれる範囲が記録どおり(){
        var result=new DynamicQueryCompiler().Compile(new DynamicCompilationRequest(
            "Exp."+Guid.NewGuid().ToString("N"),
            "Exp.Probe",
            Source,
            "Exp.g.cs"));
        var report=new StringBuilder();
        report.AppendLine($"Vector.IsHardwareAccelerated={Vector.IsHardwareAccelerated} Vector<double>.Count={Vector<double>.Count} Vector<float>.Count={Vector<float>.Count}");
        if(!result.Success||result.LoadedQuery is null){
            report.AppendLine("COMPILE FAILED");
            foreach(var d in result.Diagnostics.Where(static d=>d.Severity=="Error")){
                report.AppendLine($"{d.ID}: {d.Message}");
            }
            Assert.Fail(report.ToString());
        }
        var type=result.LoadedQuery!.DynamicAssembly!.GetType("Exp.Probe",true)!;
        object? Call(string name,object arg)=>type.GetMethod(name,BindingFlags.Public|BindingFlags.Static)!.Invoke(null,[arg]);

        var sizes=new[]{1,2,3,4,5,7,8,9,15,16,17,31,32,33,100,1000};
        var datasets=new Dictionary<string,Func<int,Random,double>>{
            ["0.1の倍数"]=static(i,r)=>(r.Next(-1000,1000))*0.1,
            ["乱数(-1..1)"]=static(i,r)=>r.NextDouble()*2-1,
            ["大小混在"]=static(i,r)=>r.NextDouble()*Math.Pow(10,r.Next(-8,9))*(r.Next(2)==0?1:-1),
            ["整数値(厳密)"]=static(i,r)=>r.Next(-1000,1000),
        };
        string[] cases=["DoubleSum","DoubleSumSel","ListSum","DoubleAvg","FloatSum","FloatAvg"];
        var divergent=new Dictionary<string,int>();
        foreach(var datasetName in datasets.Keys){
            report.AppendLine($"== データ: {datasetName}");
            foreach(var caseName in cases){
                var uno=0;
                var opt=0;
                var firstUno=string.Empty;
                var firstOpt=string.Empty;
                foreach(var n in sizes){
                    for(var trial=0;trial<20;trial++){
                        var random=new Random(n*1000+trial);
                        var raw=Enumerable.Range(0,n).Select(i=>datasets[datasetName](i,random)).ToArray();
                        object input=caseName switch{
                            "ListSum"=>raw.ToList(),
                            "FloatSum" or "FloatAvg"=>raw.Select(static v=>(float)v).ToArray(),
                            _=>raw,
                        };
                        var expected=Call("Sys"+caseName,input);
                        var unoResult=Call("Uno"+caseName,input);
                        var optResult=Call("Opt"+caseName,input);
                        if(!Same(expected,unoResult)){
                            uno++;
                            firstUno=firstUno.Length==0?$"n={n}: System={Show(expected)} 最適化なし={Show(unoResult)}":firstUno;
                        }
                        if(!Same(expected,optResult)){
                            opt++;
                            firstOpt=firstOpt.Length==0?$"n={n}: System={Show(expected)} 最適化あり={Show(optResult)}":firstOpt;
                        }
                    }
                }
                divergent[$"{datasetName}|{caseName}|なし"]=uno;
                divergent[$"{datasetName}|{caseName}|あり"]=opt;
                report.AppendLine($"  {caseName,-13} 最適化なし: {uno}/{sizes.Length*20} 件ずれ {firstUno}");
                report.AppendLine($"  {"",-13} 最適化あり: {opt}/{sizes.Length*20} 件ずれ {firstOpt}");
            }
        }
        // ---- 原因の裏づけ: 手で再現した足し方と、実際の結果を比べる ----
        report.AppendLine("== 原因の裏づけ(乱数と大小混在、全サイズ・20試行)");
        var allSameAsEmulation=true;
        foreach(var datasetName in new[]{"乱数(-1..1)","大小混在"}){
            int doubleCases=0, seqEqSystem=0, laneEqFLinq=0, laneEqSystem=0;
            int floatCases=0, floatDoubleAccEqSystem=0, floatFloatAccEqFLinq=0;
            foreach(var n in sizes){
                for(var trial=0;trial<20;trial++){
                    var random=new Random(n*1000+trial);
                    var raw=Enumerable.Range(0,n).Select(i=>datasets[datasetName](i,random)).ToArray();
                    var sys=(double)Call("SysDoubleSum",raw)!;
                    var opt=(double)Call("OptDoubleSum",raw)!;
                    double seq=0;
                    foreach(var v in raw){ seq+=v; }
                    var acc=Vector<double>.Zero;
                    var k=0;
                    for(;k<=n-Vector<double>.Count;k+=Vector<double>.Count){ acc+=new Vector<double>(raw,k); }
                    var lane=Vector.Sum(acc);
                    for(;k<n;k++){ lane+=raw[k]; }
                    doubleCases++;
                    if(BitConverter.DoubleToInt64Bits(seq)==BitConverter.DoubleToInt64Bits(sys)){ seqEqSystem++; }
                    if(BitConverter.DoubleToInt64Bits(lane)==BitConverter.DoubleToInt64Bits(opt)){ laneEqFLinq++; }
                    if(BitConverter.DoubleToInt64Bits(lane)==BitConverter.DoubleToInt64Bits(sys)){ laneEqSystem++; }

                    var fl=raw.Select(static v=>(float)v).ToArray();
                    var fsys=(float)Call("SysFloatSum",fl)!;
                    var fopt=(float)Call("OptFloatSum",fl)!;
                    double dacc=0; foreach(var v in fl){ dacc+=v; }
                    float facc=0; foreach(var v in fl){ facc+=v; }
                    floatCases++;
                    if(BitConverter.SingleToInt32Bits((float)dacc)==BitConverter.SingleToInt32Bits(fsys)){ floatDoubleAccEqSystem++; }
                    if(BitConverter.SingleToInt32Bits(facc)==BitConverter.SingleToInt32Bits(fopt)){ floatFloatAccEqFLinq++; }
                }
            }
            report.AppendLine($"  [{datasetName}] double: 順に足した結果==System {seqEqSystem}/{doubleCases}, SIMD風(4並列+Vector.Sum)の結果==FLinq {laneEqFLinq}/{doubleCases} (==System {laneEqSystem}/{doubleCases})");
            report.AppendLine($"  [{datasetName}] float : doubleで足してfloatへ戻した結果==System {floatDoubleAccEqSystem}/{floatCases}, floatのまま足した結果==FLinq {floatFloatAccEqFLinq}/{floatCases}");
            allSameAsEmulation&=seqEqSystem==doubleCases&&laneEqFLinq==doubleCases&&floatDoubleAccEqSystem==floatCases&&floatFloatAccEqFLinq==floatCases;
        }
        Assert.True(allSameAsEmulation,report.ToString());
        foreach(var datasetName in datasets.Keys){
            foreach(var path in new[]{"なし","あり"}){
                foreach(var caseName in new[]{"DoubleSumSel","DoubleAvg","FloatAvg"}){
                    Assert.True(divergent[$"{datasetName}|{caseName}|{path}"]==0,$"{datasetName} {caseName} {path}: 一致するはず{Environment.NewLine}{report}");
                }
                if(datasetName=="整数値(厳密)"){
                    foreach(var caseName in cases){
                        Assert.True(divergent[$"{datasetName}|{caseName}|{path}"]==0,$"整数値は厳密に足せるので一致するはず: {caseName} {path}{Environment.NewLine}{report}");
                    }
                }else{
                    foreach(var caseName in new[]{"DoubleSum","ListSum","FloatSum"}){
                        Assert.True(divergent[$"{datasetName}|{caseName}|{path}"]>0,$"既知のずれが出なかった(直ったなら期待を反転する): {datasetName} {caseName} {path}{Environment.NewLine}{report}");
                    }
                }
            }
        }
    }

    private static bool Same(object? a,object? b){
        return a switch{
            double x when b is double y=>BitConverter.DoubleToInt64Bits(x)==BitConverter.DoubleToInt64Bits(y),
            float x when b is float y=>BitConverter.SingleToInt32Bits(x)==BitConverter.SingleToInt32Bits(y),
            _=>Equals(a,b),
        };
    }

    private static string Show(object? value)=>value switch{
        double x=>$"{x:R}(0x{BitConverter.DoubleToInt64Bits(x):X16})",
        float x=>$"{x:R}(0x{BitConverter.SingleToInt32Bits(x):X8})",
        _=>value?.ToString()??"null",
    };
}
