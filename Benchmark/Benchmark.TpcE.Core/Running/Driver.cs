using System.Text.Json;
using Seamless.Execution;
namespace Benchmark.TpcE.Core;
/// <summary>MEE の予定(約定か、相場への指値の到達)。</summary>
public sealed record MeeEvent(long Time,long Sequence,MeeEventKind Kind,long TradeId,Code16 Symbol,int Quantity,decimal Price);
public enum MeeEventKind{
    /// <summary>Trade-Result を起こす(約定)。</summary>
    TradeResult,
    /// <summary>指値の到達を相場(ティッカー)へ積む。</summary>
    LimitReached,
}
/// <summary>
/// 運転側の状態。保存のたびに子資源 <c>~driver</c> としてマニフェストと一緒に確定するので、強制終了しても DB と食い違わない。
/// 乱数は (種, 流れ, 番号) から毎回作るので、位置は保存しなくてよい(番号だけ持つ)。
/// </summary>
public sealed record DriverState{
    public const int FormatVersion=1;
    public int Version{get;init;}=FormatVersion;
    public ulong Seed{get;init;}
    public TpcEScale Scale{get;init;}=new();
    /// <summary>実行したすべての取引(TC・DM・TR・MF・CE)の通し番号。次に流す取引の番号。</summary>
    public long TxnIndex{get;set;}
    /// <summary>実行した CE の取引の数。</summary>
    public long CeCount{get;set;}
    public long Clock{get;set;}
    public long LastIssued{get;set;}
    public long NextTradeId{get;set;}
    public long LastInitialTradeId{get;init;}
    public long InitialTradeStart{get;init;}
    public long InitialTradeEnd{get;init;}
    public long FirstUnfinishedTradeId{get;init;}
    public bool TradeCleanupDone{get;set;}
    public int DataMaintenanceTable{get;set;}
    public long NextDataMaintenance{get;set;}
    public long NextSequence{get;set;}
    public List<MeeEvent> Events{get;init;}=[];
    public List<TickerEntry> Ticker{get;init;}=[];
    public int TickerOwn{get;set;}
    public int TickerOther{get;set;}
    public System.Collections.Generic.Dictionary<TxnKind,long> Counts{get;init;}=[];
    public DriverState DeepClone()=>JsonSerializer.Deserialize<DriverState>(JsonSerializer.Serialize(this,DriverJson.Options),DriverJson.Options)!;
}
internal static class DriverJson{
    public static readonly JsonSerializerOptions Options=new(){WriteIndented=false,IncludeFields=false};
}
/// <summary>運転側の状態を保存と一緒に確定する子資源(最初に登録する)。</summary>
public sealed class DriverStateComponent(Func<DriverState> current,Action<DriverState> restore):IStoreComponent{
    public const string ComponentName="~driver";
    public string Name=>ComponentName;
    public string Save()=>JsonSerializer.Serialize(current(),DriverJson.Options);
    public void Flush(){
    }
    public void Load(string state)=>restore(JsonSerializer.Deserialize<DriverState>(state,DriverJson.Options)!);
    public void Dispose(){
    }
    public static DriverState? Read(Store store){
        if(store.TryGetSavedState(ComponentName,out var state)){
            return JsonSerializer.Deserialize<DriverState>(state,DriverJson.Options);
        }
        return null;
    }
}
/// <summary>
/// 運転側(CE・MEE・DM)。次に流す取引を決め、結果(基準のエンジンのもの)を受けて MEE の予定を立てる。
/// 1 ステップの順: 時刻が来た MEE の取引 → 時刻が来ていれば Data-Maintenance → CE を 1 件(模擬時計を進める)。
/// 最初の 1 件は Trade-Cleanup(新しく始めたときだけ。開き直しや再開では流さない)。
/// </summary>
public sealed class Driver{
    private readonly TpcEModel model;
    private readonly CeContext context;
    private readonly CeInputGenerator generator;
    private readonly long tick;
    public Driver(DriverState state){
        this.State=state;
        this.model=new TpcEModel(state.Seed,state.Scale);
        this.context=new CeContext(this.model){
            LastInitialTradeId=state.LastInitialTradeId,
            InitialTradeStart=state.InitialTradeStart,
            InitialTradeEnd=state.InitialTradeEnd,
            MarketDays=this.model.MarketDays(),
        };
        this.generator=new CeInputGenerator(this.context);
        // 公称 2 tpsE/1,000 顧客(6.6.8.2)。全取引は tpsE/0.10、CE はその 89%。
        this.tick=Math.Max(1,56_180L/state.Scale.Customers);
    }
    public DriverState State{get;private set;}
    public TpcEModel Model=>this.model;
    public CeContext Context=>this.context;
    /// <summary>新しい実行の最初の状態。</summary>
    public static DriverState Initial(ulong seed,TpcEScale scale,GenerationResult generation){
        var model=new TpcEModel(seed,scale);
        return new(){
            Seed=seed,
            Scale=scale,
            Clock=model.RunStart,
            LastIssued=generation.InitialTradeEnd,
            NextTradeId=generation.LastInitialTradeId+1,
            LastInitialTradeId=generation.LastInitialTradeId,
            InitialTradeStart=generation.InitialTradeStart,
            InitialTradeEnd=generation.InitialTradeEnd,
            FirstUnfinishedTradeId=generation.FirstUnfinishedTradeId,
            NextDataMaintenance=model.RunStart+TpcESpec.DataMaintenanceIntervalMilliseconds,
        };
    }
    /// <summary>保存から戻した状態に置き換える(開き直し・再開)。</summary>
    public void Restore(DriverState state){
        if(state.Version!=DriverState.FormatVersion||state.Seed!=this.State.Seed||state.Scale!=this.State.Scale){
            throw new InvalidOperationException("The saved driver state does not match this run (version, seed or scale).");
        }
        this.State=state;
    }
    /// <summary>次に流す取引の入力を作る。</summary>
    public TxnRequest Next(){
        var state=this.State;
        if(!state.TradeCleanupDone){
            return this.Issue(new TradeCleanupInput{
                StatusCanceled=FixedData.Canceled,
                StatusPending=FixedData.Pending,
                StatusSubmitted=FixedData.Submitted,
                TradeId=state.FirstUnfinishedTradeId,
            },state.Clock);
        }
        if(state.Events.Count>0&&state.Events[0].Time<=state.Clock){
            var due=state.Events[0];
            state.Events.RemoveAt(0);
            return this.FromEvent(due);
        }
        if(state.Ticker.Count>=TpcESpec.MaxFeedLength){
            return this.MarketFeed(state.Clock);
        }
        if(state.Clock>=state.NextDataMaintenance){
            var rng=Rng.For(state.Seed,"dm",state.TxnIndex);
            var table=state.DataMaintenanceTable;
            state.DataMaintenanceTable=(table+1)%12;
            var time=state.NextDataMaintenance;
            state.NextDataMaintenance+=TpcESpec.DataMaintenanceIntervalMilliseconds;
            this.context.Now=Math.Max(time,state.LastIssued+1);
            return this.Issue(this.generator.DataMaintenance(ref rng,table),time);
        }
        {
            var rng=Rng.For(state.Seed,"ce",state.TxnIndex);
            var kind=CeInputGenerator.PickKind(ref rng);
            this.context.Now=Math.Max(state.Clock,state.LastIssued+1);
            this.context.NextTradeId=state.NextTradeId;
            var request=this.generator.Generate(kind,ref rng);
            if(request is TradeOrderInput){
                state.NextTradeId++;
            }
            state.CeCount++;
            var issued=this.Issue(request,state.Clock);
            state.Clock+=this.tick;
            return issued;
        }
    }
    private TxnRequest Issue(TxnRequest request,long time){
        var state=this.State;
        var now=Math.Max(time,state.LastIssued+1);
        state.LastIssued=now;
        state.TxnIndex++;
        state.Counts[request.Kind]=state.Counts.GetValueOrDefault(request.Kind)+1;
        return request with{Now=now};
    }
    private TxnRequest FromEvent(MeeEvent due){
        var state=this.State;
        if(due.Kind==MeeEventKind.TradeResult){
            // 約定の結果を受けたときに相場へ積むため、銘柄と数量を覚えておく(保存は取引の合間なので、状態に入れなくてよい)。
            this.lastTradeResult=(due.TradeId,due.Symbol,due.Quantity);
            return this.Issue(new TradeResultInput{TradeId=due.TradeId,TradePrice=due.Price},due.Time);
        }
        // 指値の到達: 相場に積む(他社の分として数える)。20 件たまったら Market-Feed。
        state.Ticker.Add(new(due.Symbol,due.Price,due.Quantity));
        state.TickerOther++;
        return this.Next();
    }
    private TxnRequest MarketFeed(long time){
        var state=this.State;
        var entries=state.Ticker.GetRange(0,TpcESpec.MaxFeedLength);
        state.Ticker.RemoveRange(0,TpcESpec.MaxFeedLength);
        state.TickerOwn=0;
        state.TickerOther=state.Ticker.Count;
        var unique=new HashSet<Code16>();
        foreach(var entry in entries){
            unique.Add(entry.Symbol);
        }
        return this.Issue(new MarketFeedInput{
            Entries=[..entries],
            StatusSubmitted=FixedData.Submitted,
            TypeLimitBuy=FixedData.LimitBuy,
            TypeLimitSell=FixedData.LimitSell,
            TypeStopLoss=FixedData.StopLoss,
            UniqueSymbols=unique.Count,
        },time);
    }
    /// <summary>取引の結果(基準のエンジンのもの)を受けて MEE の予定を立てる。</summary>
    public void Completed(TxnRequest request,TxnResult result){
        var state=this.State;
        switch(request){
            case TradeCleanupInput:
                state.TradeCleanupDone=true;
                break;
            case TradeOrderInput when result is TradeOrderOutput order&&!order.RolledBack&&order.Status==0:
                this.OnTradeOrder(request,order);
                break;
            case MarketFeedInput when result is MarketFeedOutput feed:
                foreach(var triggered in feed.Triggered){
                    var rng=Rng.For(state.Seed,"mee-delay",triggered.TradeId);
                    this.Schedule(request.Now+rng.Range(1000,5000),MeeEventKind.TradeResult,triggered.TradeId,triggered.Symbol,triggered.TradeQuantity,triggered.PriceQuote);
                }
                break;
            case TradeResultInput resultInput:
                this.OnTradeResult(resultInput);
                break;
        }
    }
    private void OnTradeOrder(TxnRequest request,TradeOrderOutput order){
        var rng=Rng.For(this.State.Seed,"mee-delay",order.TradeId);
        var security=this.model.SecurityOf(order.Symbol);
        if(order.IsMarket){
            var time=request.Now+rng.Range(1000,5000);
            this.Schedule(time,MeeEventKind.TradeResult,order.TradeId,order.Symbol,order.Quantity,this.model.Price(security,time));
            return;
        }
        var bidCents=decimal.ToInt64(decimal.Round(order.RequestedPrice*100m,0,MidpointRounding.AwayFromZero));
        var trigger=this.model.TriggerTime(security,order.TradeTypeId==FixedData.LimitSell,bidCents,request.Now+1000);
        if(trigger<0){
            // 範囲外の指値は到達しない(TRADE_REQUEST に残り続ける)。
            return;
        }
        this.Schedule(trigger,MeeEventKind.LimitReached,order.TradeId,order.Symbol,order.Quantity,this.model.Price(security,trigger));
    }
    private void OnTradeResult(TradeResultInput input){
        var state=this.State;
        // 約定した取引を相場に積み、他社の取引の分を 1 件足す(相場 20 件のうち 10 件が自社、6.3.1 の注記と 3.3.3)。
        if(this.lastTradeResult.TradeId!=input.TradeId){
            throw new InvalidOperationException($"Trade-Result {input.TradeId} was not issued by the MEE.");
        }
        var (_,symbol,quantity)=this.lastTradeResult;
        state.Ticker.Add(new(symbol,input.TradePrice,quantity));
        state.TickerOwn++;
        if(state.TickerOther<state.TickerOwn){
            var rng=Rng.For(state.Seed,"mee-other",state.TxnIndex);
            var security=rng.Range(1,this.model.Scale.Securities);
            int[] quantities=[100,200,400,800];
            state.Ticker.Add(new(this.model.SymbolOf(security),this.model.Price(security,input.Now),quantities[rng.Range(0,3)]));
            state.TickerOther++;
        }
    }
    private (long TradeId,Code16 Symbol,int Quantity) lastTradeResult;
    private void Schedule(long time,MeeEventKind kind,long tradeId,Code16 symbol,int quantity,decimal price){
        var state=this.State;
        var item=new MeeEvent(time,state.NextSequence++,kind,tradeId,symbol,quantity,price);
        var index=state.Events.BinarySearch(item,MeeEventOrder.Instance);
        if(index<0){
            index=~index;
        }
        state.Events.Insert(index,item);
    }
    private sealed class MeeEventOrder:IComparer<MeeEvent>{
        public static readonly MeeEventOrder Instance=new();
        public int Compare(MeeEvent? left,MeeEvent? right){
            var time=left!.Time.CompareTo(right!.Time);
            if(time!=0){
                return time;
            }
            return left.Sequence.CompareTo(right.Sequence);
        }
    }
}
