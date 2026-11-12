using Seamless.Management;
using Seamless.Management.Components;

namespace Management.Tests;

public sealed class ソース選択範囲{
    [Theory][InlineData("\n")][InlineData("\r\n")][InlineData("\r")]
    public void 改行とサロゲート文字を含む宣言を入力欄の範囲へ変換する(string 改行){
        var コード=string.Join(改行,new[]{"// 🧭","namespace Sample{class DatabaseGenerator{}","namespace Tables{public struct Row{","public int Id{get;init;}","}}}"});
        var スナップショット=new SourceSnapshot("1",[new("Database.cs",コード)],[]);
        var 宣言=Assert.Single(SourceStructure.Read(スナップショット).Objects);
        var 範囲=プロジェクト作業領域.入力範囲(コード,宣言.Start,宣言.Length);
        var 入力=コード.Replace("\r\n","\n",StringComparison.Ordinal).Replace('\r','\n');
        Assert.Equal(入力.IndexOf("public struct Row",StringComparison.Ordinal),範囲.開始);
        Assert.Equal("public struct Row{\npublic int Id{get;init;}\n}",入力.Substring(範囲.開始,範囲.長さ));
    }
}
