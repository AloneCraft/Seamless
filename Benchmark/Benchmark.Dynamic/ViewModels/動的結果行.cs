using Benchmark.Dynamic.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Benchmark.Dynamic;

public sealed class 動的結果行:ObservableObject {
    private bool 選択済み状態か;
    private string 状態文字列="NotStarted";
    private string 意味一致文字列="—";
    private string 最適化TraceJSON=string.Empty;
    private IReadOnlyList<動的最適化Trace表示行> 最適化Trace表示行一覧=[];

    public required string 実行ID{get;init;}
    public required string QueryID{get;init;}
    public required SourceTarget 対象{get;init;}
    public required int QueryIndex{get;init;}
    public required string 短縮式{get;init;}
    public string クエリ式 {
        get {
            if(string.IsNullOrWhiteSpace(this.IR)) {
                return this.短縮式;
            }
            try {
                return QueryExpressionFormatter.Format(QueryIrJson.Deserialize(this.IR));
            } catch(Exception 例外) when(例外 is System.Text.Json.JsonException
                or ArgumentException
                or QueryIrValidationException) {
                return this.短縮式;
            }
        }
    }
    public required int N{get;init;}
    public required int 深度{get;init;}
    public string 生成時間{get;set;}="—";
    public string コンパイル時間{get;set;}="—";
    public string System中央値{get;set;}="—";
    public string System割当{get;set;}="—";
    public string FLinq中央値{get;set;}="—";
    public string FLinq割当{get;set;}="—";
    public string FLinq比{get;set;}="—";
    public string ZLinq中央値{get;set;}="—";
    public string ZLinq割当{get;set;}="—";
    public string ZLinq比{get;set;}="—";
    public int RewriteCount{get;set;}
    public string FixedPoint{get;set;}="—";
    public string 概要{get;set;}="";
    public string 生成Source{get;set;}="";
    public string IR{get;set;}="";
    public string 結果{get;set;}="";
    public string 診断{get;set;}="";
    public string 最適化Trace {
        get=>this.最適化TraceJSON;
        set {
            if(this.SetProperty(ref this.最適化TraceJSON,value??string.Empty)) {
                this.最適化Trace表示一覧=動的最適化Trace表示Parser.解析(this.最適化TraceJSON);
            }
        }
    }
    public IReadOnlyList<動的最適化Trace表示行> 最適化Trace表示一覧 {
        get=>this.最適化Trace表示行一覧;
        private set=>this.SetProperty(ref this.最適化Trace表示行一覧,value);
    }
    public string 計測詳細{get;set;}="";

    public bool 選択済みか {
        get=>this.選択済み状態か;
        set=>this.SetProperty(ref this.選択済み状態か,value);
    }

    public string 状態 {
        get=>this.状態文字列;
        set=>this.SetProperty(ref this.状態文字列,value);
    }

    public string 意味一致 {
        get=>this.意味一致文字列;
        set=>this.SetProperty(ref this.意味一致文字列,value);
    }

    public void 全列更新() {
        this.OnPropertyChanged(string.Empty);
    }
}
