using Benchmark.Dynamic.Core;
using Benchmark.Dynamic.Infrastructure;
using System.Text.Json;

namespace Benchmark.Dynamic.UI;

public enum 動的ライブ段階{
    ペア全組み合わせ,
    三段全組み合わせ,
    ランダム探索,
}

public sealed record 動的ライブ設定(
    SourceTarget 対象,
    QueryElementType 要素型,
    Seed256 基準シード,
    int 入力要素数=16,
    bool 特殊な値を混ぜる=false,
    動的ライブ段階 開始段階=動的ライブ段階.ペア全組み合わせ,
    int 開始番号=0,
    int ランダムN=4,
    OperatorSetMode モード=OperatorSetMode.Maximum);

/// <summary>1つのクエリを1つのSourceで検証した結果(画面に出す形)。</summary>
public sealed record 動的ライブ結果(
    動的ライブ段階 段階,
    int 通番,
    string QueryId,
    SourceTarget 対象,
    QueryElementType 要素型,
    Seed256 基準シード,
    int 入力要素数,
    WorkerExecutionStatus 状態,
    string 式,
    string 生成コード,
    string 展開コード,
    string 診断,
    bool 全経路一致か,
    string 再現テスト,
    IReadOnlyList<動的ライブ計測> 計測一覧){
    public bool 合格か=>this.状態==WorkerExecutionStatus.Succeeded&&this.全経路一致か;
}

public sealed record 動的ライブ計測(string 実装,double 中央値ナノ秒,double 割当バイト);

/// <summary>
/// Workerを起動して、全組み合わせ(ペア・3段)とランダム探索を順に検証し、結果を1件ずつ渡す。
/// 結果を渡す側の待ちはそのままWorkerへの背圧になるので、画面の一時停止・速度調整はここで待てばよい。
/// 検証の合否は Worker が決める(独自の合格しきい値は持たない)。
/// </summary>
public sealed class 動的ライブ検証サービス{
    private const int ランダム探索の件数=1_000_000;
    private readonly string Workerパス;
    private readonly IWorkerProcessClient WorkerClient;

    public 動的ライブ検証サービス(string Workerパス,IWorkerProcessClient? WorkerClient=null){
        ArgumentException.ThrowIfNullOrWhiteSpace(Workerパス);
        this.Workerパス=Path.GetFullPath(Workerパス);
        this.WorkerClient=WorkerClient??new VersionedJsonLineWorkerProcessClient();
    }

    public async Task 実行Async(
        動的ライブ設定 設定,
        Func<動的ライブ結果,CancellationToken,Task> 結果受信時,
        Action<動的ライブ段階>? 段階変化時,
        CancellationToken 取消){
        ArgumentNullException.ThrowIfNull(設定);
        ArgumentNullException.ThrowIfNull(結果受信時);
        if(!QueryCapabilityProfiles.SupportsElementType(設定.対象,設定.要素型)){
            throw new ArgumentException($"{設定.要素型} は {設定.対象} では使えません。",nameof(設定));
        }
        var 開始=(int)設定.開始段階;
        for(var 段階番号=開始;段階番号<=(int)動的ライブ段階.ランダム探索;段階番号++){
            取消.ThrowIfCancellationRequested();
            var 段階=(動的ライブ段階)段階番号;
            段階変化時?.Invoke(段階);
            var 開始番号=段階==設定.開始段階?設定.開始番号:0;
            await this.段階を実行Async(設定,段階,開始番号,結果受信時,取消).ConfigureAwait(false);
        }
    }

    public static WorkerRunRequest 段階の要求を作る(動的ライブ設定 設定,動的ライブ段階 段階,int 開始番号,string 実行ID){
        var 計測=new WorkerMeasurementProfile("Live-v1",1,3,0,4096);
        var 全組み合わせか=段階!=動的ライブ段階.ランダム探索;
        var 中間数=段階==動的ライブ段階.ペア全組み合わせ?1:2;
        var 演算子数=全組み合わせか?中間数+1:設定.ランダムN;
        var 件数=全組み合わせか
            ?全組み合わせの件数(設定,中間数)
            :ランダム探索の件数;
        if(全組み合わせか&&開始番号>=件数){
            開始番号=0;
        }
        return new WorkerRunRequest(
            実行ID,
            設定.基準シード,
            Math.Max(1,件数-開始番号),
            演算子数,
            全組み合わせか?演算子数:Math.Min(演算子数,8),
            設定.入力要素数,
            設定.モード,
            [設定.対象],
            計測,
            RandomQueryGenerator.GeneratorVersion,
            QueryCapabilityProfile.CurrentProfileVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            BuiltInOperatorCatalog.OperatorSetVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            QueryIndexOffset:開始番号,
            GenerationMode:全組み合わせか?QueryGenerationMode.Exhaustive:QueryGenerationMode.Random,
            ExhaustiveIntermediateCount:全組み合わせか?中間数:0,
            ExpectExceptions:true,
            ElementType:設定.要素型,
            IncludeSpecialValues:設定.特殊な値を混ぜる);
    }

    /// <summary>成り立たない並びを飛ばした後の、全組み合わせの件数。</summary>
    public static int 全組み合わせの件数(動的ライブ設定 設定,int 中間数){
        var 許可=QueryCapabilityProfiles.CreateSet(設定.モード,[設定.対象],設定.要素型);
        var 中間一覧=許可.OperatorIds
            .Where(static id=>BuiltInOperatorCatalog.Get(id).NodeKind is QueryNodeKind.Unary or QueryNodeKind.Binary or QueryNodeKind.Nested)
            .ToArray();
        var 終端一覧=許可.OperatorIds
            .Where(static id=>BuiltInOperatorCatalog.Get(id).NodeKind==QueryNodeKind.Terminal)
            .ToArray();
        var 件数=0;
        foreach(var 仕様 in ExhaustiveQueryEnumerator.Enumerate(中間数,中間一覧,終端一覧)){
            if(ExhaustiveQueryEnumerator.TryGenerate(仕様,設定.基準シード,設定.対象,設定.入力要素数,out _,true,設定.要素型)){
                件数++;
            }
        }
        return 件数;
    }

    private async Task 段階を実行Async(
        動的ライブ設定 設定,
        動的ライブ段階 段階,
        int 開始番号,
        Func<動的ライブ結果,CancellationToken,Task> 結果受信時,
        CancellationToken 取消){
        var 実行ID="live-"+Guid.NewGuid().ToString("N");
        var 要求=await Task.Run(()=>段階の要求を作る(設定,段階,開始番号,実行ID),取消).ConfigureAwait(false);
        await using var Session=this.WorkerClient.Start(this.Workerパス);
        var IR一覧=new Dictionary<string,WorkerQueryStartedEvent>(StringComparer.Ordinal);
        await Session.SendAsync(実行ID,WorkerMessageTypes.StartRun,要求,取消).ConfigureAwait(false);
        try{
            await foreach(var Envelope in Session.ReadAllAsync(取消).ConfigureAwait(false)){
                switch(Envelope.MessageType){
                    case WorkerMessageTypes.QueryStarted:{
                        var 開始イベント=DynamicWorkerProtocol.DeserializePayload<WorkerQueryStartedEvent>(Envelope);
                        IR一覧[開始イベント.QueryId]=開始イベント;
                        break;
                    }
                    case WorkerMessageTypes.SourceExecutionCompleted:{
                        var 完了=DynamicWorkerProtocol.DeserializePayload<WorkerSourceExecutionCompletedEvent>(Envelope);
                        IR一覧.Remove(完了.QueryId,out var クエリ);
                        var 結果=結果を作る(設定,段階,要求,完了,クエリ);
                        await 結果受信時(結果,取消).ConfigureAwait(false);
                        break;
                    }
                    case WorkerMessageTypes.RunCompleted:
                    case WorkerMessageTypes.RunCanceled:
                        return;
                    case WorkerMessageTypes.Fatal:{
                        var 致命=DynamicWorkerProtocol.DeserializePayload<WorkerFatalEvent>(Envelope);
                        // 全組み合わせを数え切った後に先の番号を求めたときなど。段階の終わりとして次へ進む。
                        if(致命.Message.Contains("exhaustive enumeration",StringComparison.OrdinalIgnoreCase)){
                            return;
                        }
                        throw new InvalidOperationException($"Workerが停止しました: {致命.Message}");
                    }
                }
            }
        }finally{
            if(!Session.HasExited){
                try{
                    await Session.CancelAsync(実行ID).ConfigureAwait(false);
                }catch(Exception 例外) when(例外 is InvalidOperationException or IOException or ObjectDisposedException){
                }
            }
        }
    }

    public static 動的ライブ結果 結果を作る(
        動的ライブ設定 設定,
        動的ライブ段階 段階,
        WorkerRunRequest 要求,
        WorkerSourceExecutionCompletedEvent 完了,
        WorkerQueryStartedEvent? クエリ){
        var 式=string.Empty;
        var 再現テスト=string.Empty;
        QueryIrDocument? IR=null;
        if(クエリ is not null){
            try{
                IR=QueryIrJson.Deserialize(クエリ.IrJson);
                式=QueryExpressionFormatter.Format(IR);
            }catch(Exception 例外) when(例外 is JsonException or InvalidDataException or ArgumentException){
                式="(IRを読めませんでした)";
            }
        }
        var 差異=差異を読む(完了.ResultJson,out var 一致);
        var 診断=診断を要約する(完了,差異);
        if(IR is not null){
            再現テスト=再現テストを作る(要求,完了,IR,診断);
        }
        return new 動的ライブ結果(
            段階,
            完了.QueryIndex,
            完了.QueryId,
            完了.SourceTarget,
            設定.要素型,
            設定.基準シード,
            設定.入力要素数,
            完了.Status,
            式,
            完了.SourceText,
            完了.ExpandedSourceText??string.Empty,
            診断,
            一致,
            再現テスト,
            計測を読む(完了.MeasurementsJson));
    }

    private static string 再現テストを作る(
        WorkerRunRequest 要求,
        WorkerSourceExecutionCompletedEvent 完了,
        QueryIrDocument IR,
        string 診断){
        if(IR.ElementType!=QueryElementType.Int){
            return"// int 以外の要素型の再現テストは未対応です。式・シード・要素型から Benchmark.Dynamic で再現してください。";
        }
        if(IR.SourceTarget is not(SourceTarget.SystemArray or SourceTarget.SystemList or SourceTarget.SystemIEnumerable or SourceTarget.SystemRange)){
            return"// 永続系Sourceの再現テストは未対応です。";
        }
        var 入力シード=SeedDerivation.Derive(要求.BaseSeed,"fixture",完了.QueryIndex);
        var 主=FixtureValues.Values(入力シード,"primary",要求.InputElementCount);
        var 副=FixtureValues.Values(入力シード,"secondary",要求.InputElementCount);
        return ReproTestEmitter.Emit(IR,主,副,new ReproTestOptions{Note=$"Status={完了.Status}{Environment.NewLine}{診断}"});
    }

    private static string 診断を要約する(WorkerSourceExecutionCompletedEvent 完了,string 差異){
        var 行=new List<string>{$"Status: {完了.Status}"};
        if(差異.Length!=0){
            行.Add(差異);
        }
        if(!string.IsNullOrWhiteSpace(完了.DiagnosticsJson)&&完了.Status!=WorkerExecutionStatus.Succeeded){
            try{
                using var 文書=JsonDocument.Parse(完了.DiagnosticsJson);
                foreach(var 診断 in 文書.RootElement.EnumerateArray()){
                    if(診断.TryGetProperty("severity",out var 重大度)&&重大度.GetString()=="Error"){
                        var ID=診断.TryGetProperty("id",out var 識別子)?識別子.GetString():"";
                        var 本文=診断.TryGetProperty("message",out var メッセージ)?メッセージ.GetString():"";
                        行.Add($"{ID}: {本文}");
                    }
                }
            }catch(JsonException){
                行.Add(完了.DiagnosticsJson);
            }
        }
        return string.Join(Environment.NewLine,行);
    }

    private static string 差異を読む(string? 結果JSON,out bool 一致){
        一致=false;
        if(string.IsNullOrWhiteSpace(結果JSON)){
            return string.Empty;
        }
        try{
            using var 文書=JsonDocument.Parse(結果JSON);
            var 根=文書.RootElement;
            var 差分=new List<string>();
            var 例外あり=根.TryGetProperty("exceptions",out var 例外一覧)
                &&例外一覧.ValueKind==JsonValueKind.Object
                &&例外一覧.EnumerateObject().Any();
            if(根.TryGetProperty("comparisons",out var 比較一覧)&&比較一覧.ValueKind==JsonValueKind.Object){
                一致=!例外あり;
                foreach(var 比較 in 比較一覧.EnumerateObject()){
                    if(!比較.Value.GetProperty("isMatch").GetBoolean()){
                        一致=false;
                        差分.Add($"{比較.Name}: {(比較.Value.TryGetProperty("difference",out var 差異)?差異.GetString():null)}");
                    }
                }
            }
            if(例外あり){
                差分.Add("実行例外: "+string.Join(", ",例外一覧.EnumerateObject().Select(static 項目=>項目.Name)));
            }
            return string.Join(Environment.NewLine,差分);
        }catch(JsonException){
            return string.Empty;
        }
    }

    private static IReadOnlyList<動的ライブ計測> 計測を読む(string? JSON){
        if(string.IsNullOrWhiteSpace(JSON)){
            return[];
        }
        try{
            using var 文書=JsonDocument.Parse(JSON);
            if(!文書.RootElement.TryGetProperty("measurement",out var 計測)
                ||計測.ValueKind!=JsonValueKind.Object
                ||!計測.TryGetProperty("measurements",out var 一覧)){
                return[];
            }
            var 結果=new List<動的ライブ計測>();
            foreach(var 項目 in 一覧.EnumerateArray()){
                結果.Add(new(
                    実装名(項目.GetProperty("implementation").GetString()),
                    項目.GetProperty("medianNanoseconds").GetDouble(),
                    項目.GetProperty("allocatedBytesPerOperation").GetDouble()));
            }
            return 結果;
        }catch(Exception 例外) when(例外 is JsonException or InvalidOperationException or KeyNotFoundException){
            return[];
        }
    }

    private static string 実装名(string? 値){
        return 値?.ToLowerInvariant() switch{
            "system"=>"System.Linq",
            "flinq"=>"FLinq",
            "zlinq"=>"ZLinq",
            "flinqunoptimized"=>"FLinq最適化なし",
            _=>値??"?",
        };
    }
}
