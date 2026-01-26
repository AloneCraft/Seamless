using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に非 null 値が入っていなければなりません。'required' 修飾子を追加するか、null 許容として宣言することを検討してください。
namespace Seamless.Reasoning;
public class コアコア(コア CoreA,コア CoreB) {
    public コア CoreA = CoreA;
    public コア CoreB = CoreB;
    public SharingLevel Level;
    public double 往復レイテンシns;
    public double BandwidthTotal; // 二人で叩いた時の合計帯域(GiB/s)
    private volatile bool Task1を継続;
    public unsafe void 往復レイテンシns計測() {
        using var barrier = new Barrier(2);
        var sharedFlag = (byte*)NativeMemory.AlignedAlloc(192,64);
        const int offset = 64;
        sharedFlag[offset] = 0;//なぜ配列の中間か、他のローカル変数がキャッシュラインに乗らないように
        var tasks = new Task[2];
        this.Task1を継続 = true;
        tasks[0] = Task.Run(() => {
            実行環境.SetThreadAffinityMask(実行環境.GetCurrentThread(),this.CoreA.OS上のID);
            var Stopwatch=new Stopwatch();
            barrier.SignalAndWait();
            for(var Bits = 0;Bits < 32;Bits++) {
                var ByteCount = 1L << Bits;
                Stopwatch.Restart();
                for(var a = 0;a < ByteCount;a++) {
                    //①
                    Volatile.Write(ref sharedFlag[offset],1);//受信側に1を送る
                    while(Volatile.Read(ref sharedFlag[offset]) == 1) {
                        //Thread.SpinWait(1);
                    }
                }
                var ns = Stopwatch.ns();
                if(ns > Const.ns) {
                    this.往復レイテンシns = ns / ByteCount;
                    this.Task1を継続 = false;
                    Volatile.Write(ref sharedFlag[offset],1);
                    break;
                }
            }
        });
        tasks[1] = Task.Run(() => {
            実行環境.SetThreadAffinityMask(実行環境.GetCurrentThread(),this.CoreB.OS上のID);
            barrier.SignalAndWait();
            while(this.Task1を継続) {
                //送信側から1を受信するまで待つ
                while(Volatile.Read(ref sharedFlag[offset]) == 0) {
                    //Thread.SpinWait(1);
                }
                //②
                Volatile.Write(ref sharedFlag[offset],0);
            }
        });
        Task.WaitAll(tasks);
        NativeMemory.AlignedFree(sharedFlag);
    }
    public void 計測ペア干渉(使いまわしバッファ buffer,int Bits) {
        using var barrier = new Barrier(2); // 2つのスレッドが揃うまで待つ
        var tasks = new Task<double>[2];
        // スレッド A 起動
        tasks[0] = Task.Run(() => {
            実行環境.SetThreadAffinityMask(実行環境.GetCurrentThread(),this.CoreA.OS上のID);
            barrier.SignalAndWait(); // Bが来るのを待つ
            return コア.計測ランダムReadレイテンシsecond(buffer,Bits); // 実測メソッド
        });

        // スレッド B 起動
        tasks[1] = Task.Run(() => {
            実行環境.SetThreadAffinityMask(実行環境.GetCurrentThread(),this.CoreB.OS上のID);
            barrier.SignalAndWait(); // Aが来るのを待つ
            return コア.計測ランダムReadレイテンシsecond(buffer,Bits); // 実測メソッド
        });
        var Handle = Task.WhenAll(tasks);
        Handle.Wait();
        var Result = Handle.Result;

        // 二人の平均レイテンシや、単体時との差分を pair に格納
        this.往復レイテンシns = (Result[0] + Result[1]) / 2.0;
    }
}
