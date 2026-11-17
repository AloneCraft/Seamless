using System.Globalization;
using Seamless.Management;

namespace Seamless.Management.Components;

internal sealed record 生成配置ブロック(GeneratedBasicBlock 元,int Y){
    internal const int X=172,幅=300,高さ=92;
    internal int 下端=>this.Y+高さ;
}
internal sealed record 生成配置接続(GeneratedControlEdge 元,string 経路,int 上端,int 下端,bool 戻りか);
internal sealed class 生成コード配置{
    internal 生成配置ブロック[] ブロック{get;private init;}=[];
    internal 生成配置接続[] 接続{get;private init;}=[];
    internal int 幅=>720;
    internal int 高さ=>Math.Max(320,this.ブロック.Length*136+80);
    internal bool 不正な識別子か{get;private init;}
    internal static 生成コード配置 作成(GeneratedExecutionCode コード){
        if(コード.Blocks.Length>2048||コード.Edges.Length>8192||コード.Blocks.Select(項目=>項目.Id).Distinct().Count()!=コード.Blocks.Length){return new(){不正な識別子か=true};}
        var ブロック=コード.Blocks.OrderBy(項目=>項目.Id).Select((項目,番号)=>new 生成配置ブロック(項目,40+番号*136)).ToArray();
        var 位置=ブロック.ToDictionary(項目=>項目.元.Id);var 接続=new List<生成配置接続>();
        foreach(var 辺 in コード.Edges){
            if(!位置.TryGetValue(辺.From,out var 元)){return new(){不正な識別子か=true};}
            生成配置ブロック? 先=null;
            if(辺.To is{} ID&&!位置.TryGetValue(ID,out 先)){return new(){不正な識別子か=true};}
            var 元Y=元.Y+生成配置ブロック.高さ/2;var 先Y=元.下端+24;var 戻りか=false;
            if(先 is not null){先Y=先.Y+生成配置ブロック.高さ/2;戻りか=先.Y<=元.Y;}
            var 横=520+接続.Count%8*14;var 側=生成配置ブロック.X+生成配置ブロック.幅;
            if(戻りか){横=32+接続.Count%8*14;側=生成配置ブロック.X;}
            var 経路=string.Create(CultureInfo.InvariantCulture,$"M {側} {元Y} C {横} {元Y}, {横} {先Y}, {側} {先Y}");
            if(先==元){経路=string.Create(CultureInfo.InvariantCulture,$"M {側} {元Y} C {横} {元Y-56}, {横} {元Y+56}, {側} {元Y+24}");}
            接続.Add(new(辺,経路,Math.Min(元Y,先Y)-56,Math.Max(元Y,先Y)+56,戻りか));
        }
        return new(){ブロック=ブロック,接続=接続.ToArray()};
    }
}
