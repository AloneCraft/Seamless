using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
// ReSharper disable AsyncVoidMethod
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に非 null 値が入っていなければなりません。'required' 修飾子を追加するか、null 許容として宣言することを検討してください。

namespace Seamless.Reasoning;

public class Benchmark:IBenchmark {
    private void WriteLine(string message){
        Trace.WriteLine(message);
    }
    private bool disposedValue;

    protected virtual void Dispose(bool disposing){
        if(!this.disposedValue){
            if(disposing){
                // TODO: マネージド状態を破棄します (マネージド オブジェクト)
            }

            // TODO: アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
            // TODO: 大きなフィールドを null に設定します
            this.disposedValue=true;
        }
    }

    public void Dispose(){
        // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    private BenchmarkConfig BenchmarkConfig;
    private readonly Stopwatch Stopwatch = new();
    public void Setup(BenchmarkConfig BenchmarkConfig){
        this.BenchmarkConfig=BenchmarkConfig;
    }
    private const int Align = 32;
    public unsafe double 計測ランダムReadレイテンシsecond(int Bits){
        var rng = new Random();
        var BufferBytes = 1<<Bits;
        var Buffer = (void**)NativeMemory.AlignedAlloc((nuint)BufferBytes,Align);

        try {
            {
                Debug.Assert((long)Buffer%Align==0);
                var Count = BufferBytes/sizeof(nint);
                // 1. まずはシーケンシャルにインデックスを準備
                var indices = Enumerable.Range(0,Count).ToArray();
                // 2. インデックス側をシャッフル (Fisher-Yates)
                // メモリ上のポインタを直接スワップするより、インデックスを混ぜる方が安全かつ確実
                for(var i = Count-1; i>0; i--){
                    var j = rng.Next(i+1);
                    (indices[i],indices[j])=(indices[j],indices[i]);
                }
                // 3. シャッフルされたインデックスに基づいて「次のアドレス」を書き込む
                for(var i = 0; i<Count-1; i++){
                    // Bufferの[現在のインデックス]地点に、[次のインデックス]のメモリアドレスを格納
                    Buffer[indices[i]]=&Buffer[indices[i+1]];
                }
                // 循環させる
                Buffer[indices[Count-1]]=&Buffer[indices[0]];
            }
            var ランダムアクセス回数 = BufferBytes/sizeof(int);
            // 3. ウォームアップ
            var Current = Buffer[0];
            for(var a = 0; a<ランダムアクセス回数; a++)
                Current=*(void**)Current;
            // 4. 本計測
            var Stopwatch = this.Stopwatch;
            Stopwatch.Restart();
            for(var ループBits = 0; ループBits<20; ループBits++){
                var ループ回数 = 1L<<ループBits;
                Stopwatch.Restart();
                for(var a = 0L; a<ループ回数; a++){
                    var current = Buffer[0];
                    //ランダムアクセスをBufferCount回する
                    for(var b = 0L; b<ランダムアクセス回数; b++)
                        current=*(void**)current;
                }
                var second = Stopwatch.second();
                if(second>Const.second){
                    var 合計アクセス回数 = ループ回数*ランダムアクセス回数;
                    var アクセス当たりのsecond = second/合計アクセス回数;
                    return アクセス当たりのsecond;
                }
            }
            throw new InvalidDataException(MethodBase.GetCurrentMethod()!.Name);
        }
        catch(Exception ex){
            throw;
        }
        finally {
            NativeMemory.AlignedFree(Buffer);
        }
    }
    [MethodImpl(MethodImplOptions.NoOptimization)]
    public bool 演算性能計測(long ループ回数,out long BytesSecond){
        var BufferBytes = Unsafe.SizeOf<Vector256<uint>>();
        var v0 = Vector256<uint>.Zero;
        var v1 = Vector256<uint>.Zero;
        var Stopwatch = this.Stopwatch;
        {
            var c = 0L;
            Stopwatch.Restart();
            for(var a = 0L; a<ループ回数; a++){
                v0=Avx2.Add(v0,v1);
                //c+=BufferBytes;
            }
            var second = Stopwatch.second();
            if(second>Const.second){
                var count00000 = c;
                var TotalBytes = BufferBytes*ループ回数;
                BytesSecond=(long)(TotalBytes/second);
                return true;
            }
            else {

            }
        }
        BytesSecond=-1;
        return false;
    }
    [MethodImpl(MethodImplOptions.NoOptimization)]
    public long 演算性能計測(){
        for(var ループBits = 0; ループBits<64; ループBits++){
            var ループ回数 = 1L<<ループBits;
            if(this.演算性能計測(ループ回数,out var BytesSecond)){
                return BytesSecond;
            }
        }
        throw new InvalidDataException(MethodBase.GetCurrentMethod()!.Name);
    }
    [MethodImpl(MethodImplOptions.NoOptimization)]
    public long 演算性能計測(int ループBits,CancellationToken ct){
        var ループ回数 = 1L<<ループBits;
        if(ct.IsCancellationRequested)
            ct.ThrowIfCancellationRequested();
        if(this.演算性能計測(ループ回数,out var BytesSecond)){
            return BytesSecond;
        }
        throw new InvalidDataException(MethodBase.GetCurrentMethod()!.Name);
    }
    [MethodImpl(MethodImplOptions.NoOptimization)]
    public async Task<long> 演算性能計測Async(CancellationToken ct = default){
        return await Task.Run(() => {
            for(var ループBits = 0; ループBits<64; ループBits++){
                ct.ThrowIfCancellationRequested();
                var ループ回数 = 1L<<ループBits;
                if(this.演算性能計測(ループ回数,out var BytesSecond)){
                    return BytesSecond;
                }
            }
            throw new InvalidDataException("全ビット試行しましたが、計測規定時間に達しませんでした。");
        },ct);
    }
    [MethodImpl(MethodImplOptions.NoOptimization)]
    public unsafe bool シーケンシャル性能計測BytesSecond(long BufferBytes,long ループ回数,out long BytesSecond){
        var Buffer = (void**)NativeMemory.AlignedAlloc((nuint)BufferBytes,Align);
        try {
            var Size = Unsafe.SizeOf<Vector256<float>>();
            var start = (byte*)Buffer;
            var end = start+BufferBytes;
            var Stopwatch = this.Stopwatch;
            {
                var c = 0L;
                Stopwatch.Restart();
                for(var a = 0L; a<ループ回数; a++){
                    for(var offset = start; offset<end; offset+=Size){
                        _=Vector256.LoadAligned(offset);
                    }
                    //c+=BufferBytes;
                }
                var second = Stopwatch.second();
                if(second>Const.second){
                    var count00000 = c;
                    var TotalBytes = BufferBytes*ループ回数;
                    BytesSecond=(long)(TotalBytes/second);
                    return true;
                }
                else {
                    BytesSecond=-1;
                    return false;
                }
            }
        }
        finally {
            NativeMemory.Free(Buffer);
        }
    }
    [MethodImpl(MethodImplOptions.NoOptimization)]
    public long シーケンシャル性能計測BytesSecond(){
        for(var ループBits = 0; ループBits<64; ループBits++){
            var ループ回数 = 1L<<ループBits;
            if(this.シーケンシャル性能計測BytesSecond(ループBits,ループ回数,out var BytesSecond)){
                return BytesSecond;
            }
        }
        throw new InvalidDataException(MethodBase.GetCurrentMethod()!.Name);
    }
    [MethodImpl(MethodImplOptions.NoOptimization)]
    public unsafe long シーケンシャル性能計測BytesSecond(int Bits){
        var BufferBytes = 1L<<Bits;
        var Buffer = (void**)NativeMemory.AlignedAlloc((nuint)BufferBytes,Align);
        try {
            var Size = Unsafe.SizeOf<Vector256<float>>();
            var start = (byte*)Buffer;
            var end = start+BufferBytes;
            var Stopwatch = this.Stopwatch;
            //this.シーケンシャル性能計測BytesSecond(BufferBytes);
            for(var ループBits = 0; ループBits<64; ループBits++){
                var ループ回数 = 1L<<ループBits;
                var c = 0L;
                Stopwatch.Restart();
                for(var a = 0L; a<ループ回数; a++){
                    for(var offset = start; offset<end; offset+=Size){
                        _=Vector256.LoadAligned(offset);
                    }
                    //c+=BufferBytes;
                }
                var second = Stopwatch.second();
                if(second>Const.second){
                    var count00000 = c;
                    var TotalBytes = BufferBytes*ループ回数;
                    var BytesSecond = TotalBytes/second;
                    return (long)BytesSecond;
                }
                else { }
            }
        }
        finally {
            NativeMemory.AlignedFree(Buffer);
        }
        throw new InvalidDataException(MethodBase.GetCurrentMethod()!.Name);
    }
    //public async void GetRamsEnumerableAsync(ObservableCollection<Ram> Rams,CancellationToken ct=default){
    //    // 計測前にGCを掃除し、スレッドをバックグラウンドへ移動
    //    GC.Collect(2,GCCollectionMode.Forced,true);
    //    GC.WaitForPendingFinalizers();
    //    for(var Bits=Const.最小Bits;Bits<Const.最大Bits;Bits++){
    //        var Bytes=1 << Bits;
    //        //var 中間容量=(Bits+容量)/2;
    //        // A. レイテンシ計測
    //        var ListRam=new List<Ram>();
    //        await Task.Run(()=>{
    //            var レイテンシns=this.計測ランダムReadレイテンシsecond(Bits);
    //            var BytesSecond=this.シーケンシャル性能計測BytesSecond(Bits);
    //            ListRam.Add(new Ram(Bytes,レイテンシns,BytesSecond));
    //        });
    //        foreach(var a in ListRam){
    //            Rams.Add(a);
    //        }
    //    }
    //}
    public async IAsyncEnumerable<特定サイズRam> GetRamsEnumerable([EnumeratorCancellation] CancellationToken ct = default){
        // 計測前にGCを掃除し、スレッドをバックグラウンドへ移動
        GC.Collect(2,GCCollectionMode.Forced,true);
        GC.WaitForPendingFinalizers();
        for(var Bits = Const.最小Bits; Bits<Const.最大Bits; Bits++){
            var Bytes = 1<<Bits;
            //var 中間容量=(Bits+容量)/2;
            // A. レイテンシ計測
            var レイテンシns = this.計測ランダムReadレイテンシsecond(Bits);
            var BytesSecond = this.シーケンシャル性能計測BytesSecond(Bits);
            yield return new 特定サイズRam(Bytes,レイテンシns,BytesSecond);
        }
    }
    //public async ValueTask<(List<Ram>List一部Ram,List<Ram>List全部Temp)> キャッシュ計測結果崖分離Async(){
    //    // 計測前にGCを掃除し、スレッドをバックグラウンドへ移動
    //    GC.Collect(2,GCCollectionMode.Forced,true);
    //    GC.WaitForPendingFinalizers();
    //    var List=await Task.Run(()=>{
    //        var List全部Ram=new List<Ram>();
    //        {
    //            var BytesSecond=this.演算性能計測();
    //            List全部Ram.Add(new(0,0,BytesSecond));
    //        }
    //        for(var Bits=Const.最小Bits;Bits<Const.最大Bits;Bits++){
    //            var Bytes=1<<Bits;
    //            //var 中間容量=(Bits+容量)/2;
    //            // A. レイテンシ計測
    //            var レイテンシns =this.計測ランダムReadレイテンシsecond(Bits);
    //            var BytesSecond =this.シーケンシャル性能計測BytesSecond(Bits);
    //            List全部Ram.Add(new(Bytes, レイテンシns, BytesSecond));
    //        }
    //        var List一部Ram=new List<Ram>();
    //        //if(List全部Temp.Count == 0) return List一部Ram;
    //        const double 跳ね上がり閾値=1.5;
    //        var 前回レイテンシ=List全部Ram[0].レイテンシsecond;
    //        var 階層カウント=0;
    //        for(var i=1;i < List全部Ram.Count;i++){
    //            var 現在=List全部Ram[i];
    //            if(現在.レイテンシsecond > 前回レイテンシ * 跳ね上がり閾値){
    //                var 全部Ram=List全部Ram[i - 1];
    //                var 名前=$"L{階層カウント + 1}";
    //                List一部Ram.Add(全部Ram);
    //                階層カウント++;
    //                // L3まで判定したら、残りはRAMなのでループを抜ける構成もアリ
    //                //if (階層カウント >= 3) break;
    //            }
    //            前回レイテンシ=現在.レイテンシsecond;
    //        }
    //        var 最終要素=List全部Ram.Last();
    //        List一部Ram.Add(最終要素);
    //        return (List一部Ram,List全部Temp:List全部Ram);
    //    });
    //    return List;
    //}
    //public async IAsyncEnumerable<コア> GetコアEnumerableAsync([EnumeratorCancellation]CancellationToken ct=default){
    //    var coreCount=2;// 環境に合わせて取得
    //    for(var a=0;a<coreCount;a++){
    //        var osId=new nint(1L<<a);
    //        var coreResult=await Task.Run(async ()=>{
    //            実行環境.SetThreadAffinityMask(実行環境.GetCurrentThread(),osId);
    //            Thread.Sleep(1);
    //            GC.Collect(2,GCCollectionMode.Forced,true);
    //            GC.WaitForPendingFinalizers();
    //            // 計測実行（awaitしてもアフィニティが維持されるよう ConfigureAwait(false) 等に注意）
    //            var (list一部,list全部)=await this.キャッシュ計測結果崖分離Async();
    //            var k=new コア(osId);//{EnumerableRam=list全部};
    //            foreach(var b in list全部){
    //                k.EnumerableRam.Add(b);
    //            }
    //            if(list一部.Count>0) k.L1=list一部[0];
    //            if(list一部.Count>1) k.L2=list一部[1];
    //            if(list一部.Count>2) k.L3=list一部[2];
    //            if(list一部.Count>3) k.L4=list一部[3];
    //            if(list一部.Count>4) k.Dram=list一部[4];
    //            return k;
    //        });
    //        yield return coreResult;
    //    }
    //}
    public record struct 特定サイズRam(long Bytes,double レイテンシSecond,long 帯域BytesSecond);
    public record struct Ram(long[] ArrayBytes,double[] ArrayレイテンシSecond,long[] Array帯域BytesSecond);
    public record struct コア(Ram Ram);
    public コア[] Arrayコア;
    public async void Run(){
        //var 使いまわしバッファ=new 使いまわしバッファ(29);
        //var List=await Task.Run(() =>
        var Bits数 = Const.最大Bits-Const.最小Bits;
        var Arrayコア_Length = Environment.ProcessorCount;
        var Arrayコア = this.Arrayコア=new コア[Arrayコア_Length];
        ThreadPool.QueueUserWorkItem(async st => {
            for(var a = 0; a<Arrayコア.Length; a++){
                Trace.WriteLine(a.ToString());
                var OS上のID = new nint(1L<<a);
                実行環境.SetThreadAffinityMask(実行環境.GetCurrentThread(),OS上のID);
                Thread.Sleep(1);
                var ArrayBytes = new long[Bits数];
                var ArrayレイテンシSecond = new double[Bits数];
                var Array帯域BytesSecond = new long[Bits数];
                //{
                //    var BytesSecond=this.演算性能計測();
                //    List全部Ram.Add(new(0,0,BytesSecond));
                //}
                var b = 0;
                for(var Bits = Const.最小Bits; Bits<Const.最大Bits; Bits++){
                    var Bytes = 1<<Bits;
                    var レイテンシns = this.計測ランダムReadレイテンシsecond(Bits);
                    var BytesSecond = this.シーケンシャル性能計測BytesSecond(Bits);
                    ArrayBytes[b]=Bytes;
                    ArrayレイテンシSecond[b]=レイテンシns;
                    Array帯域BytesSecond[b]=BytesSecond;
                    b++;
                }
                var List一部Bytes = new List<long>();
                var List一部レイテンシSecond = new List<double>();
                var List一部帯域BytesSecond = new List<long>();
                const double 跳ね上がり閾値 = 1.5;
                var 前回レイテンシ = ArrayレイテンシSecond[0];
                var 階層カウント = 0;
                for(var i = 1; i<ArrayレイテンシSecond.Length; i++){
                    var レイテンシSecond0 = ArrayレイテンシSecond[i];
                    if(レイテンシSecond0>前回レイテンシ*跳ね上がり閾値){
                        var レイテンシSecond1 = ArrayレイテンシSecond[i-1];
                        //var 名前=$"L{階層カウント + 1}";
                        List一部レイテンシSecond.Add(レイテンシSecond1);
                        階層カウント++;
                    }
                    前回レイテンシ=レイテンシSecond0;
                }
                var 最終要素 = ArrayレイテンシSecond.Last();
                List一部レイテンシSecond.Add(最終要素);
                var 特定サイズRam = new 特定サイズRam(ArrayBytes.Last(),ArrayレイテンシSecond.Last(),Array帯域BytesSecond.Last());
                var Ram = new Ram(List一部Bytes.ToArray(),List一部レイテンシSecond.ToArray(),List一部帯域BytesSecond.ToArray());
                var コア = new コア(Ram);
                Arrayコア[a]=コア;
                //var (List一部Ram, List全部Ram)=await this.キャッシュ計測結果崖分離Async();
                //foreach(var b in ArrayレイテンシSecond){
                //    this.WriteLine($"コア{a},{b.ToString()}");
                //}
                //var コア0=new コア(OS上のID);
                //var EnumerableRam=コア0.ArrayRam;
                //foreach(var b in ArrayレイテンシSecond){
                //    EnumerableRam.Add(b);
                //}
                //if(List一部Ram.Count > 0){
                //    コア0.L1=List一部Ram[0];
                //    if(List一部Ram.Count > 1){
                //        コア0.L2=List一部Ram[1];
                //        if(List一部Ram.Count > 2){
                //            コア0.L3=List一部Ram[2];
                //            if(List一部Ram.Count > 3){
                //                コア0.L4=List一部Ram[3];
                //                if(List一部Ram.Count > 4){
                //                    コア0.Dram=List一部Ram[4];
                //                }
                //            }
                //        }
                //    }
                //}
                //Arrayコア[a]=コア0;
                //this.WriteLine($"コア{a},L1   {コア0.L1.ToString()}");
                //this.WriteLine($"コア{a},L2   {コア0.L2.ToString()}");
                //this.WriteLine($"コア{a},L3   {コア0.L3.ToString()}");
                //this.WriteLine($"コア{a},L4   {コア0.L4.ToString()}");
                //this.WriteLine($"コア{a},Dram {コア0.Dram.ToString()}");
            }
        });
        //{
        //    var Arrayコアコア_Length=Arrayコア_Length * Arrayコア.Length;
        //    var Arrayコアコア=new コアコア[Arrayコアコア_Length];
        //    var c=0;
        //    foreach(var コアa in Arrayコア){
        //        foreach(var コアb in Arrayコア){
        //            if(コアa.OS上のID == コアb.OS上のID)
        //                continue;
        //            //var Bytesb=コアb.L1.Bytes;
        //            //long Bytes;
        //            //if(Bytesa<Bytesb){
        //            //    Bytes=Bytesa;
        //            //} else{
        //            //    Bytes=Bytesb;
        //            //}
        //            //var Bits=BitOperations.Log2((ulong)Bytes);
        //            var コアコア=new コアコア(コアa,コアb);
        //            コアコア.往復レイテンシns計測();
        //            this.WriteLine($"コア:{コアa.OS上のID} コア:{コアb.OS上のID} 往復レイテンシ:{コアコア.往復レイテンシns}ns");
        //            //var pair=コアコア.計測ペア干渉(a,b,使いまわしバッファ,Bits);
        //            Arrayコアコア[c++]=コアコア;
        //        }
        //    }
        //    // App.xaml.cs などで保持しているインスタンス経由でアクセス
        //}
    }
    public void Cleanup(){
    }
}
