using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に非 null 値が入っていなければなりません。'required' 修飾子を追加するか、null 許容として宣言することを検討してください。
namespace Seamless.Reasoning;

[StructLayout(LayoutKind.Sequential)]
public class コア(nint OS上のID){
    public nint OS上のID{get; set;}=OS上のID;
    public string 種類{get; set;} // "P-Core" or "E-Core"
    //public ObservableCollection<Ram> EnumerableRam{ get; } = new();
    public Ram[] ArrayRam;
    public Ram L1{get; set;}
    public Ram L2{get; set;}
    public Ram L3{get; set;}
    public Ram L4{get; set;}
    public Ram Dram{get; set;}
    public long 時間当たりの回数{get; set;}
    private long 速度計測回数;
    public Dictionary<int,double> 他のコアとのレイテンシ{get; set;}
    [MethodImpl(MethodImplOptions.NoOptimization)]
    private static void 速度計測(long 回数){
        do{} while(--回数>=0);
    }
    public void 速度計測(Stopwatch Stopwatch,long 回数){
        Stopwatch.Restart();
        var s=Stopwatch;
        速度計測(回数);
        var ElapsedMilliseconds=Stopwatch.ElapsedMilliseconds;
        this.時間当たりの回数=回数/ElapsedMilliseconds;
    }
    public void 速度計測(Stopwatch Stopwatch){
        var 回数=this.速度計測回数=速度計測回数を求める(Stopwatch);
        this.速度計測(Stopwatch,回数);
    }
    public static long 速度計測回数を求める(Stopwatch Stopwatch){
        for(var 回数=1L;; 回数<<=1){
            Stopwatch.Restart();
            速度計測(回数);
            var ns=Stopwatch.ns();
            if(ns>Const.ns){
                return 回数/2;
            }
            else{
            }
        }
    }
    // 各サイズごとのレイテンシ（ナノ秒）を返す
    public static async ValueTask<(List<Ram> List一部Ram,List<Ram> List全部Temp)> キャッシュ計測Async(使いまわしバッファ 使いまわしバッファ){
        // 計測前にGCを掃除し、スレッドをバックグラウンドへ移動
        GC.Collect(2,GCCollectionMode.Forced,true);
        GC.WaitForPendingFinalizers();
        var List=await Task.Run(()=>{
            var List全部Ram=new List<Ram>();
            {
                var BytesSecond=計測演算性能Only(使いまわしバッファ);
                List全部Ram.Add(new(0,0,BytesSecond));
            }
            for(var Bits=Const.最小Bits; Bits<Const.最大Bits; Bits++){
                var Bytes=1<<Bits;
                //var 中間容量=(Bits+容量)/2;
                // A. レイテンシ計測
                var レイテンシns=計測ランダムReadレイテンシsecond(使いまわしバッファ,Bits);
                var BytesSecond=計測シーケンシャルReadBytesSecond(使いまわしバッファ,Bits);
                List全部Ram.Add(new(Bytes,レイテンシns,BytesSecond));
            }
            var List一部Ram=new List<Ram>();
            //if(List全部Temp.Count == 0) return List一部Ram;
            const double 跳ね上がり閾値=1.5;
            var 前回レイテンシ=List全部Ram[0].レイテンシsecond;
            var 階層カウント=0;
            for(var i=1; i<List全部Ram.Count; i++){
                var 現在=List全部Ram[i];
                if(現在.レイテンシsecond>前回レイテンシ*跳ね上がり閾値){
                    var 全部Ram=List全部Ram[i-1];
                    var 名前=$"L{階層カウント+1}";
                    List一部Ram.Add(全部Ram);
                    階層カウント++;
                    // L3まで判定したら、残りはRAMなのでループを抜ける構成もアリ
                    //if (階層カウント >= 3) break;
                }
                前回レイテンシ=現在.レイテンシsecond;
            }
            var 最終要素=List全部Ram.Last();
            List一部Ram.Add(最終要素);
            return(List一部Ram,List全部Temp:List全部Ram);
        });
        return List;
    }
    public static unsafe double 計測ランダムReadレイテンシsecond(使いまわしバッファ 使いまわしバッファ,int Bits){
        使いまわしバッファ.GetBuffers(Bits,out var Buffer0,out var BufferBytes);
        var Buffer=(void**)Buffer0;
        var ランダムアクセス回数=BufferBytes/sizeof(int);
        // 3. ウォームアップ
        var Current=Buffer[0];
        for(var a=0; a<ランダムアクセス回数; a++)
            Current=*(void**)Current;
        // 4. 本計測
        var Stopwatch=使いまわしバッファ.Stopwatch;
        Stopwatch.Restart();
        for(var ループBits=0; ループBits<64; ループBits++){
            var ループ回数=1L<<ループBits;
            Stopwatch.Restart();
            for(var a=0L; a<ループ回数; a++){
                var current=Buffer[0];
                //ランダムアクセスをBufferCount回する
                for(var b=0L; b<ランダムアクセス回数; b++)
                    current=*(void**)current;
            }
            var second=Stopwatch.second();
            if(second>Const.second){
                var 合計アクセス回数=ループ回数*ランダムアクセス回数;
                var アクセス当たりのsecond=second/合計アクセス回数;
                return アクセス当たりのsecond;
            }
        }
        throw new InvalidDataException(MethodBase.GetCurrentMethod()!.Name);
    }
    [MethodImpl(MethodImplOptions.NoOptimization)]
    public static long 計測演算性能Only(使いまわしバッファ 使いまわしバッファ){
        var BufferBytes=Unsafe.SizeOf<Vector256<uint>>();
        var v0=Vector256<uint>.Zero;
        var v1=Vector256<uint>.Zero;
        var Stopwatch=使いまわしバッファ.Stopwatch;
        for(var ループBits=0; ループBits<64; ループBits++){
            var ループ回数=1L<<ループBits;
            var c=0L;
            Stopwatch.Restart();
            for(var a=0L; a<ループ回数; a++){
                v0=Avx2.Add(v0,v1);
                //c+=BufferBytes;
            }
            var second=Stopwatch.second();
            if(second>Const.second){
                var count00000=c;
                var TotalBytes=BufferBytes*ループ回数;
                var BytesSecond=TotalBytes/second;
                return(long)BytesSecond;
            }
            else{

            }
        }
        throw new InvalidDataException(MethodBase.GetCurrentMethod()!.Name);
    }
    [MethodImpl(MethodImplOptions.NoOptimization)]
    public static unsafe long 計測シーケンシャルReadBytesSecond(使いまわしバッファ 使いまわしバッファ,int Bits){
        使いまわしバッファ.GetBuffers(Bits,out var Buffer,out var BufferBytes);
        var Size=Unsafe.SizeOf<Vector256<float>>();
        var start=(byte*)Buffer;
        var end=start+BufferBytes;
        var Stopwatch=使いまわしバッファ.Stopwatch;
        for(var ループBits=0; ループBits<64; ループBits++){
            var ループ回数=1L<<ループBits;
            var c=0L;
            Stopwatch.Restart();
            for(var a=0L; a<ループ回数; a++){
                for(var offset=start; offset<end; offset+=Size){
                    _=Vector256.LoadAligned(offset);
                }
                //c+=BufferBytes;
            }
            var second=Stopwatch.second();
            if(second>Const.second){
                var count00000=c;
                var TotalBytes=BufferBytes*ループ回数;
                var BytesSecond=TotalBytes/second;
                return(long)BytesSecond;
            }
            else{

            }
        }
        throw new InvalidDataException(MethodBase.GetCurrentMethod()!.Name);
    }
}
