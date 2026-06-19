using System.Diagnostics;
using System.Runtime.InteropServices;
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に非 null 値が入っていなければなりません。'required' 修飾子を追加するか、null 許容として宣言することを検討してください。
namespace Seamless.Reasoning
;

public unsafe class 使いまわしバッファ:IDisposable{
    // マネージド配列の中にアンマネージドポインタを格納
    private void*[] Buffers;
    public Stopwatch Stopwatch;
    private bool disposed;
    /// <summary>
    ///
    /// </summary>
    /// <param name="Bits">バイト数を表すビット数。8なら256バイト。</param>
    /// <returns></returns>
    public void GetBuffers(int Bits,out void*Buffer,out int Length){
        Buffer=this.Buffers[Bits];
        Length=1<<Bits;
    }
    public 使いまわしバッファ(int MaxByteBits){
        this.Stopwatch=new();
        var rng=new Random();
        this.Buffers=new void*[MaxByteBits];
        for(var a=0; a<MaxByteBits; a++){
            var Bytes=1<<a;
            if(Bytes<sizeof(nint)){
                this.Buffers[a]=null;
                continue;
            }
            // void** として扱うことで、各要素に「次のポインタ」を格納できる
            const int Align=32;
            var Buffer=(void**)NativeMemory.AlignedAlloc((nuint)Bytes,Align);
            Debug.Assert((long)Buffer%Align==0);
            var Count=Bytes/sizeof(nint);
            // 1. まずはシーケンシャルにインデックスを準備
            var indices=Enumerable.Range(0,Count).ToArray();
            // 2. インデックス側をシャッフル (Fisher-Yates)
            // メモリ上のポインタを直接スワップするより、インデックスを混ぜる方が安全かつ確実
            for(var i=Count-1; i>0; i--){
                var j=rng.Next(i+1);
                (indices[i],indices[j])=(indices[j],indices[i]);
            }
            // 3. シャッフルされたインデックスに基づいて「次のアドレス」を書き込む
            for(var i=0; i<Count-1; i++){
                // Bufferの[現在のインデックス]地点に、[次のインデックス]のメモリアドレスを格納
                Buffer[indices[i]]=&Buffer[indices[i+1]];
            }
            // 循環させる
            Buffer[indices[Count-1]]=&Buffer[indices[0]];
            this.Buffers[a]=Buffer;
        }
    }

    // 計測用メソッドの例 (await の外で呼ぶ)
    public double Measure(int bitIndex,int iterations){
        var current=(void**)this.Buffers[bitIndex];
        if(current==null) return 0;
        this.Stopwatch.Restart();
        for(var i=0; i<iterations; i++){
            current=(void**)*current;
        }
        this.Stopwatch.Stop();
        return this.Stopwatch.Elapsed.TotalNanoseconds/iterations;
    }
    public void Dispose(){
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing){
        if(!this.disposed){
            foreach(var ptr in this.Buffers){
                if(ptr!=null){
                    NativeMemory.AlignedFree(ptr);
                }
            }
            this.disposed=true;
        }
    }
    ~使いまわしバッファ()=>this.Dispose(false);
}
