using System.Diagnostics;
using Xunit.Abstractions;
using Seamless.Reasoning;

namespace Test.Tsavorite.Reasoning;

public class UnitTest1{
    private readonly ITestOutputHelper _output;

    public UnitTest1(ITestOutputHelper output){
        _output=output;
    }

    [Fact]
    public async void Test1(){
        var 使いまわしバッファ=new 使いまわしバッファ(29);
        var Arrayコア_Length=2;
        var Arrayコア=new コア[Arrayコア_Length];
        for(var a=0;a<Arrayコア.Length;a++){
            Trace.WriteLine(a.ToString());
            var OS上のID=new nint(1L<<a);
            実行環境.SetThreadAffinityMask(実行環境.GetCurrentThread(),OS上のID);
            //コア.L1計測(s,L1計測回数);
            var(List一部Ram,List全部Ram)=await コア.キャッシュ計測Async(使いまわしバッファ);
            foreach(var b in List全部Ram){
                this.WriteLine($"コア{a},{b.ToString()}");
            }
            var コア0=new コア(OS上のID);
            var EnumerableRam=コア0.ArrayRam;
            foreach(var b in List全部Ram){
                EnumerableRam.Add(b);
            }
            if(List一部Ram.Count>0){
                コア0.L1=List一部Ram[0];
                if(List一部Ram.Count>1){
                    コア0.L2=List一部Ram[1];
                    if(List一部Ram.Count>2){
                        コア0.L3=List一部Ram[2];
                        if(List一部Ram.Count>3){
                            コア0.L4=List一部Ram[3];
                            if(List一部Ram.Count>4){
                                コア0.Dram=List一部Ram[4];
                            }
                        }
                    }
                }
            }
            Arrayコア[a]=コア0;
            this.WriteLine($"コア{a},L1   {コア0.L1.ToString()}");
            this.WriteLine($"コア{a},L2   {コア0.L2.ToString()}");
            this.WriteLine($"コア{a},L3   {コア0.L3.ToString()}");
            this.WriteLine($"コア{a},L4   {コア0.L4.ToString()}");
            this.WriteLine($"コア{a},Dram {コア0.Dram.ToString()}");
        }{
            var Arrayコアコア_Length=Arrayコア_Length*Arrayコア.Length;
            var Arrayコアコア=new コアコア[Arrayコアコア_Length];
            var c=0;
            foreach(var コアa in Arrayコア){
                foreach(var コアb in Arrayコア){
                    if(コアa.OS上のID==コアb.OS上のID)
                        continue;
                    //var Bytesb=コアb.L1.Bytes;
                    //long Bytes;
                    //if(Bytesa<Bytesb){
                    //    Bytes=Bytesa;
                    //} else{
                    //    Bytes=Bytesb;
                    //}
                    //var Bits=BitOperations.Log2((ulong)Bytes);
                    var コアコア=new コアコア(コアa,コアb);
                    コアコア.往復レイテンシns計測();
                    this.WriteLine($"コア:{コアa.OS上のID} コア:{コアb.OS上のID} 往復レイテンシ:{コアコア.往復レイテンシns}ns");
                    //var pair=コアコア.計測ペア干渉(a,b,使いまわしバッファ,Bits);
                    Arrayコアコア[c++]=コアコア;
                }
            }
            // App.xaml.cs などで保持しているインスタンス経由でアクセス
        }

    }
    private void WriteLine(string message){
        this._output.WriteLine(message);
        Trace.WriteLine(message);
    }
}



