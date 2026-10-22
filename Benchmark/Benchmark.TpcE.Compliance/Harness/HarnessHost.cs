using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Benchmark.TpcE.Core.Harness;
namespace Benchmark.TpcE.Compliance;
/// <summary>
/// EGenTxnHarness(C++)を C# から動かす。EGenHarness.dll(橋渡し)を読み込み、取引ごとに <see cref="Run"/> を呼ぶ。
/// ハーネスが DB インターフェース経由で呼ぶフレームは、<see cref="IHarnessFrameEngine"/> へ中継する。
/// 一度に 1 本のスレッドから使う(SUT は取引を直列に実行する)。
/// </summary>
internal sealed unsafe class HarnessHost:IDisposable{
    private static readonly Dictionary<string,int> StructSizes=BuildSizes();
    private static readonly (int Input,int Output)[] FrameSizes=BuildFrameSizes();
    [ThreadStatic]private static HarnessHost? current;
    private readonly nint library;
    private readonly nint handle;
    private readonly delegate* unmanaged[Cdecl]<nint,int,void*,void*,int*,int> run;
    private readonly delegate* unmanaged[Cdecl]<nint,void> destroy;
    private IHarnessFrameEngine? engine;
    private IMarketSink? market;
    private Exception? failure;
    public HarnessHost(string dllPath){
        this.library=NativeLibrary.Load(dllPath);
        var create=(delegate* unmanaged[Cdecl]<delegate* unmanaged[Cdecl]<int,void*,void*,int>,delegate* unmanaged[Cdecl]<void*,int>,nint>)NativeLibrary.GetExport(this.library,"Harness_Create");
        this.run=(delegate* unmanaged[Cdecl]<nint,int,void*,void*,int*,int>)NativeLibrary.GetExport(this.library,"Harness_Run");
        this.destroy=(delegate* unmanaged[Cdecl]<nint,void>)NativeLibrary.GetExport(this.library,"Harness_Destroy");
        this.handle=create(&OnFrame,&OnSend);
    }
    /// <summary>
    /// 取引を 1 件流す。<paramref name="input"/> と <paramref name="output"/> は EGen の TxnInput / TxnOutput の構造体。
    /// 戻り値はハーネスの出力の status(0 成功、正は警告、負は誤り)。フレームが例外を出したときは、その例外を包んで投げる。
    /// </summary>
    public int Run(HarnessTransaction transaction,IHarnessFrameEngine frameEngine,IMarketSink marketSink,Span<byte> input,Span<byte> output){
        this.engine=frameEngine;
        this.market=marketSink;
        this.failure=null;
        current=this;
        try{
            int failureCode;
            int status;
            fixed(byte* inputPointer=input){
                fixed(byte* outputPointer=output){
                    status=this.run(this.handle,(int)transaction,inputPointer,outputPointer,&failureCode);
                }
            }
            if(failureCode!=0){
                throw new InvalidOperationException($"The harness stopped while running {transaction} (code {failureCode}).",this.failure);
            }
            return status;
        }finally{
            current=null;
            frameEngine.EndTransaction();
        }
    }
    public void Dispose(){
        if(this.handle!=0){
            this.destroy(this.handle);
        }
        NativeLibrary.Free(this.library);
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    private static int OnFrame(int frameId,void* input,void* output){
        var host=current;
        if(host is null||host.engine is null||host.market is null){
            return 2;
        }
        try{
            var sizes=FrameSizes[frameId];
            var inputSpan=Span<byte>.Empty;
            if(input!=null&&sizes.Input>0){
                inputSpan=new Span<byte>(input,sizes.Input);
            }
            var outputSpan=Span<byte>.Empty;
            if(output!=null&&sizes.Output>0){
                outputSpan=new Span<byte>(output,sizes.Output);
            }
            host.engine.ExecuteFrame((HarnessFrame)frameId,inputSpan,outputSpan,host.market);
            return 0;
        }catch(Exception error){
            host.failure=error;
            return 1;
        }
    }
    [UnmanagedCallersOnly(CallConvs=[typeof(CallConvCdecl)])]
    private static int OnSend(void* request){
        var host=current;
        if(host is null||host.market is null){
            return 0;
        }
        try{
            host.market.SendToMarket(new ReadOnlySpan<byte>(request,StructSizes["TradeRequest"]));
            return 1;
        }catch(Exception error){
            host.failure=error;
            return 0;
        }
    }
    private static Dictionary<string,int> BuildSizes(){
        var sizes=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var (name,aliases,size) in HarnessLayout.Classes){
            sizes[name]=size;
            foreach(var alias in aliases){
                // 別名(例: TTradeStatusFrame1Input)は先頭の T を除いた名前で引く。
                sizes[alias.Substring(1)]=size;
            }
        }
        return sizes;
    }
    private static (int Input,int Output)[] BuildFrameSizes(){
        var result=new (int Input,int Output)[HarnessFrameInfo.All.Count];
        foreach(var info in HarnessFrameInfo.All){
            var input=0;
            var output=0;
            if(info.Input is not null){
                input=StructSizes[info.Input];
            }
            if(info.Output is not null){
                output=StructSizes[info.Output];
            }
            result[(int)info.Frame]=(input,output);
        }
        return result;
    }
    /// <summary>構造体の大きさ(バイト)。TxnInput / TxnOutput の領域を用意するときに使う。</summary>
    public static int SizeOf(string structName)=>StructSizes[structName];
}
