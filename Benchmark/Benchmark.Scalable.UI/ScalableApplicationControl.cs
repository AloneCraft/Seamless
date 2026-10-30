using Benchmark.Infrastructure;
using System.Text.Json;

namespace Benchmark.Scalable.UI;

public sealed class 大規模アプリ操作サービス(MainPage 画面,大規模実行サービス サービス):Iアプリ操作サービス{
    public IReadOnlyList<string> 操作一覧=>["run"];
    public Task<アプリ操作準備?> 準備Async(string 操作,JsonElement 設定)=>MainThread.InvokeOnMainThreadAsync<アプリ操作準備?>(()=>{
        if(操作!="run"){throw new InvalidDataException("未対応の操作です。");}
        var 要求=大規模外部実行設定.読み取る(設定).要求;
        var 予約=サービス.実行排他.予約する();
        if(予約 is null){return null;}
        return new アプリ操作準備(予約,取消=>MainThread.InvokeOnMainThreadAsync(()=>画面.外部実行Async(要求,取消)));
    });
}
