using System.Diagnostics;
using System.Globalization;
using Seamless.Collections;
using Seamless.Collections.Tsavorite;
using Store=Seamless.Execution.Store;
namespace Benchmark.TpcC.Spike;
/// <summary>16 文字の ID(TPC-E の Code16 と同じ詰め方: 文字を上位バイトから 8 文字ずつ 2 つの ulong へ)。</summary>
public readonly record struct C16(ulong High,ulong Low){
    public static C16 From(string text){
        return new(Pack(text,0),Pack(text,8));
    }
    private static ulong Pack(string text,int offset){
        ulong value=0;
        for(var index=0;index<8;index++){
            value<<=8;
            var position=offset+index;
            if(position<text.Length){
                value|=text[position];
            }
        }
        return value;
    }
}
/// <summary>TPC-E の IndexHeader と同じ形(24 バイト)。</summary>
public struct IH{
    public long First;
    public long Last;
    public long Total;
}
public struct SetRow<TPad>:IRecord<C16> where TPad:unmanaged{
    public C16 K;
    public IH V;
    public TPad Pad;
    public readonly C16 Key=>this.K;
}
/// <summary>
/// 実験 5: Remove のあとに保存を繰り返し、開き直す(TPC-E found-bugs #2)。
/// --mode described : 記録にある最小の再現(辞書 Code16→IndexHeader、LogMemorySize=1<<18、seed 7、キー 68 種、2/3 で代入・1/3 で Remove、
///                    操作 n 件→保存 を 3 回(1・3 回目は操作用オブジェクトを閉じて保存、2 回目は開いたまま)→開き直す)を、
///                    キーの作り方 4 通り × n=1..120 で流す。同じ形を Set の Remove→Add(TsavoriteTable.Update と同じ)でも流す。
/// --mode sector    : found-bugs #3 の最小の再現(16 バイトのキーと 24 バイトの値を 19 件、保存 2 回→開き直す)。検出力の確認用。
/// --mode sweep     : 条件を乱数で振って --minutes の間くり返す(辞書/Set、値の大きさ、メモリ、保存と開き直しの回数、Remove の割合)。
/// </summary>
public static class Exp5{
    public static int Run(Args options){
        var mode=options.Str("mode","described");
        var root=Report.NewRoot(options,"exp5-"+mode);
        Console.WriteLine($"root={root} Garnet {typeof(Tsavorite.core.LightEpoch).Assembly.GetName().Version} Seamless.Collections.Tsavorite {typeof(SetStore<,>).Assembly.Location}");
        var failures=mode switch{
            "described"=>Described(root,options),
            "sector"=>Sector(root),
            "sweep"=>Sweep(root,options),
            "sectorscan"=>SectorScan(root,options),
            _=>throw new ArgumentException("--mode must be described, sector or sweep"),
        };
        if(failures==0){
            Console.WriteLine("PASS exp5 "+mode);
            return 0;
        }
        Console.WriteLine($"FAIL exp5 {mode}: {failures} failures");
        return 1;
    }
    private static readonly string[] KeySets=["sym","random","digits","long"];
    private static C16[] Keys(string set,int count){
        var keys=new C16[count];
        var rng=new Random(12345);
        for(var index=0;index<count;index++){
            keys[index]=set switch{
                "sym"=>C16.From("SYM"+index.ToString("D4",CultureInfo.InvariantCulture)),
                "random"=>C16.From(RandomCode(rng)),
                "digits"=>C16.From(index.ToString(CultureInfo.InvariantCulture)),
                "long"=>new C16(0,(ulong)index+1),
                _=>throw new ArgumentException(set),
            };
        }
        return keys;
    }
    private static string RandomCode(Random rng){
        var length=3+rng.Next(13);
        var chars=new char[length];
        for(var index=0;index<length;index++){
            chars[index]=(char)('A'+rng.Next(26));
        }
        return new string(chars);
    }
    public sealed record Spec(string Kind,string KeySet,int KeyCount,int Seed,int[] OpsPerRound,bool[] CloseBeforeSave,bool[] ReopenAfterSave,int RemovePercent,long LogMemory,long PageSize,int PadKind);
    private static int Described(string root,Args options){
        var failures=0;
        var maxOps=options.Int("max-ops",120);
        foreach(var kind in new[]{"dict","set"}){
            foreach(var keySet in KeySets){
                var failedAt=new List<int>();
                for(var ops=1;ops<=maxOps;ops++){
                    var spec=new Spec(kind,keySet,68,7,[ops,ops,ops],[true,false,true],[false,false,false],33,1L<<18,TsavoriteTuning.Default.PageSize,0);
                    var problem=Trial(Path.Combine(root,"t"),spec);
                    if(problem is not null){
                        failedAt.Add(ops);
                        if(failedAt.Count<=3){
                            Console.WriteLine($"  {kind}/{keySet} ops={ops}: {problem}");
                        }
                    }
                }
                failures+=failedAt.Count;
                Console.WriteLine($"described {kind}/{keySet}: {failedAt.Count}/{maxOps} failed [{string.Join(",",failedAt)}]");
            }
        }
        return failures;
    }
    private static int Sector(string root){
        var directory=Path.Combine(root,"sector");
        Directory.CreateDirectory(directory);
        var tuning=TsavoriteTuning.Default with{LogMemorySize=1L<<18};
        using(var store=Store.Open(directory)){
            var data=store.CreateTsavorite<C16,IH>("d",tuning);
            using(var handle=data.Create()){
                for(var index=0;index<19;index++){
                    handle[new C16(0,(ulong)index)]=new IH{First=index,Last=index*2,Total=index*3};
                }
            }
            Console.WriteLine($"sector: tail={data.Address.TailAddress}");
            store.Save();
            store.Save();
        }
        try{
            using var store=Store.Open(directory);
            var data=store.CreateTsavorite<C16,IH>("d",tuning);
            Console.WriteLine($"sector: reopened, count={data.GetLongCount()}");
            return 0;
        }catch(Exception error){
            Console.WriteLine($"sector: reopen failed: {Describe(error)}");
            return 1;
        }
    }
    /// <summary>
    /// 削除なしで N 件を入れ、保存 2 回(2 回目は操作用オブジェクトを閉じて)→開き直す、を N=1..max で流す。
    /// 辞書と Set(行の大きさ 3 通り)で、ログの末尾がレコードの途中に当たる件数が同じように失敗するかを見る(found-bugs #3 の形の一般化)。
    /// </summary>
    private static int SectorScan(string root,Args options){
        var max=options.Int("max",300);
        var failures=0;
        var directory=Path.Combine(root,"t");
        foreach(var kind in new[]{"dict","set0","set1","set2"}){
            var failed=new List<int>();
            for(var n=1;n<=max;n++){
                if(Directory.Exists(directory)){
                    Directory.Delete(directory,true);
                }
                Directory.CreateDirectory(directory);
                var spec=new Spec("set","sym",n,5,[],[],[],0,1L<<18,TsavoriteTuning.Default.PageSize,0);
                switch(kind){
                    case "dict":
                        spec=spec with{Kind="dict"};
                        break;
                    case "set1":
                        spec=spec with{PadKind=1};
                        break;
                    case "set2":
                        spec=spec with{PadKind=2};
                        break;
                }
                var keys=Keys("sym",n);
                var model=new Dictionary<C16,IH>();
                var tuning=TsavoriteTuning.Default with{LogMemorySize=spec.LogMemory,PageSize=spec.PageSize};
                string? problem=null;
                ITrialStore? store=null;
                try{
                    store=ITrialStore.Open(spec,directory,tuning);
                    for(var index=0;index<n;index++){
                        var value=new IH{First=index,Last=index*2,Total=index*3};
                        store.Put(keys[index],value);
                        model[keys[index]]=value;
                    }
                    store.Save(false);
                    store.Save(true);
                    store.Dispose();
                    store=null;
                    store=ITrialStore.Open(spec,directory,tuning);
                    problem=store.Verify(model);
                }catch(Exception error){
                    problem=Describe(error);
                }finally{
                    try{
                        store?.Dispose();
                    }catch(Exception){
                    }
                }
                if(problem is not null){
                    failed.Add(n);
                }
            }
            failures+=failed.Count;
            var more="";
            if(failed.Count>40){
                more=",...";
            }
            Console.WriteLine($"sectorscan {kind}: {failed.Count}/{max} failed [{string.Join(",",failed.Take(40))}{more}]");
        }
        return failures;
    }
    private static int Sweep(string root,Args options){
        var minutes=options.Double("minutes",10);
        var rng=new Random(options.Int("seed",1));
        var watch=Stopwatch.StartNew();
        var trials=0;
        var failures=0;
        var byKind=new Dictionary<string,(int Trials,int Failures)>();
        while(watch.Elapsed.TotalMinutes<minutes){
            var rounds=1+rng.Next(6);
            var ops=new int[rounds];
            var close=new bool[rounds];
            var reopen=new bool[rounds];
            for(var round=0;round<rounds;round++){
                ops[round]=1+rng.Next(150);
                close[round]=rng.Next(2)==0;
                reopen[round]=rng.Next(4)==0;
            }
            var memoryChoice=rng.Next(3);
            long page=TsavoriteTuning.Default.PageSize;
            long memory=1L<<18;
            if(memoryChoice==1){
                page=4096;
                memory=4*4096;
            }else if(memoryChoice==2){
                memory=1L<<20;
            }
            var kind="set";
            if(rng.Next(2)==0){
                kind="dict";
            }
            var spec=new Spec(kind,KeySets[rng.Next(KeySets.Length)],8+rng.Next(400),rng.Next(),ops,close,reopen,new[]{20,33,50,70}[rng.Next(4)],memory,page,rng.Next(3));
            var problem=Trial(Path.Combine(root,"t"),spec);
            trials++;
            var label=$"{spec.Kind}/pad{spec.PadKind}";
            var entry=byKind.GetValueOrDefault(label);
            entry.Trials++;
            if(problem is not null){
                failures++;
                entry.Failures++;
                if(failures<=10){
                    Console.WriteLine($"  FAIL {spec with{}} ops=[{string.Join(",",spec.OpsPerRound)}] close=[{string.Join(",",spec.CloseBeforeSave)}] reopen=[{string.Join(",",spec.ReopenAfterSave)}]: {problem}");
                }
            }
            byKind[label]=entry;
        }
        Console.WriteLine($"sweep: {trials} trials in {watch.Elapsed.TotalMinutes:0.0} min, {failures} failures; "+string.Join(", ",byKind.OrderBy(static pair=>pair.Key).Select(static pair=>$"{pair.Key}={pair.Value.Failures}/{pair.Value.Trials}")));
        return failures;
    }
    /// <summary>1 回の試し。開き直したときに例外になるか、中身が模型(最後の保存の時点)と違えば説明を返す。</summary>
    public static string? Trial(string directory,Spec spec){
        if(Directory.Exists(directory)){
            Directory.Delete(directory,true);
        }
        Directory.CreateDirectory(directory);
        var keys=Keys(spec.KeySet,spec.KeyCount);
        var rng=new Random(spec.Seed);
        var tuning=TsavoriteTuning.Default with{LogMemorySize=spec.LogMemory,PageSize=spec.PageSize};
        var model=new Dictionary<C16,IH>();
        ITrialStore? store=null;
        var stage="open";
        try{
            store=ITrialStore.Open(spec,directory,tuning);
            for(var round=0;round<spec.OpsPerRound.Length;round++){
                stage=$"round {round}";
                for(var op=0;op<spec.OpsPerRound[round];op++){
                    var key=keys[rng.Next(keys.Length)];
                    if(rng.Next(100)>=spec.RemovePercent){
                        var value=new IH{First=rng.Next(),Last=rng.Next(),Total=rng.Next()};
                        store.Put(key,value);
                        model[key]=value;
                    }else{
                        var removed=store.Remove(key);
                        if(removed!=model.Remove(key)){
                            return $"{stage}: Remove({key}) returned {removed}";
                        }
                    }
                }
                stage=$"save {round}";
                store.Save(spec.CloseBeforeSave[round]);
                if(spec.ReopenAfterSave[round]){
                    stage=$"reopen after save {round}";
                    store.Dispose();
                    store=null;
                    store=ITrialStore.Open(spec,directory,tuning);
                    var problem=store.Verify(model);
                    if(problem is not null){
                        return stage+": "+problem;
                    }
                }
            }
            stage="close";
            store.Dispose();
            store=null;
            stage="final reopen";
            store=ITrialStore.Open(spec,directory,tuning);
            var final=store.Verify(model);
            if(final is not null){
                return stage+": "+final;
            }
            return null;
        }catch(Exception error){
            return stage+": "+Describe(error);
        }finally{
            try{
                store?.Dispose();
            }catch(Exception){
            }
        }
    }
    private static string Describe(Exception error){
        var inner=error;
        while(inner.InnerException is not null&&inner is AggregateException or InvalidOperationException or System.Reflection.TargetInvocationException){
            inner=inner.InnerException;
        }
        var frame=inner.StackTrace?.Split('\n').FirstOrDefault()?.Trim()??"";
        return $"{error.GetType().Name}: {error.Message} <= {inner.GetType().Name} {frame}";
    }
    /// <summary>辞書と Set を同じ手順で試すための包み。</summary>
    private interface ITrialStore:IDisposable{
        void Put(C16 key,IH value);
        bool Remove(C16 key);
        void Save(bool closeHandle);
        string? Verify(Dictionary<C16,IH> model);
        static ITrialStore Open(Spec spec,string directory,TsavoriteTuning tuning){
            if(spec.Kind=="dict"){
                return new DictionaryTrial(directory,tuning);
            }
            return spec.PadKind switch{
                0=>new SetTrial<Pad8>(directory,tuning),
                1=>new SetTrial<Pad56>(directory,tuning),
                _=>new SetTrial<Pad600>(directory,tuning),
            };
        }
    }
    private sealed class DictionaryTrial:ITrialStore{
        private readonly Store store;
        private readonly DictionaryStore<C16,IH> data;
        private Seamless.Collections.Tsavorite.IDictionary<C16,IH>? handle;
        public DictionaryTrial(string directory,TsavoriteTuning tuning){
            this.store=Store.Open(directory);
            try{
                this.data=this.store.CreateTsavorite<C16,IH>("d",tuning);
                this.handle=this.data.Create();
            }catch{
                this.store.Dispose();
                throw;
            }
        }
        public void Put(C16 key,IH value){
            this.handle![key]=value;
        }
        public bool Remove(C16 key)=>this.handle!.Remove(key);
        public void Save(bool closeHandle){
            if(closeHandle){
                this.handle!.Dispose();
                this.store.Save();
                this.handle=this.data.Create();
            }else{
                this.store.Save();
            }
        }
        public string? Verify(Dictionary<C16,IH> model){
            var scanned=0;
            foreach(var pair in this.handle!){
                scanned++;
                var key=pair.Key;
                var value=pair.Value;
                if(!model.TryGetValue(key,out var expected)){
                    return $"extra key {key}";
                }
                if(expected.First!=value.First||expected.Last!=value.Last||expected.Total!=value.Total){
                    return $"value of {key} differs";
                }
            }
            foreach(var pair in model){
                if(!this.handle.TryGetValue(pair.Key,out _)){
                    return $"missing key {pair.Key}";
                }
            }
            var count=this.data.GetLongCount();
            if(scanned!=model.Count||count!=model.Count){
                return $"count scan={scanned} store={count} model={model.Count}";
            }
            return null;
        }
        public void Dispose(){
            this.handle?.Dispose();
            this.store.Dispose();
        }
    }
    /// <summary>Set の上書きは TPC-E の TsavoriteTable.Update と同じ Remove→Add。</summary>
    private sealed class SetTrial<TPad>:ITrialStore where TPad:unmanaged{
        private readonly Store store;
        private readonly SetStore<C16,SetRow<TPad>> data;
        private Set<C16,SetRow<TPad>>? handle;
        public SetTrial(string directory,TsavoriteTuning tuning){
            this.store=Store.Open(directory);
            try{
                this.data=this.store.CreateTsavoriteSet<C16,SetRow<TPad>>("s",tuning);
                this.handle=this.data.Create();
            }catch{
                this.store.Dispose();
                throw;
            }
        }
        public void Put(C16 key,IH value){
            var row=new SetRow<TPad>{K=key,V=value};
            Hashing.Fill(ref row.Pad,value.First);
            if(this.handle!.ContainsKey(key)){
                if(!this.handle.Remove(key)){
                    throw new SpikeFailure($"Remove({key}) returned false in replace");
                }
            }
            if(!this.handle.Add(row)){
                throw new SpikeFailure($"Add({key}) returned false");
            }
        }
        public bool Remove(C16 key)=>this.handle!.Remove(key);
        public void Save(bool closeHandle){
            if(closeHandle){
                this.handle!.Dispose();
                this.store.Save();
                this.handle=this.data.Create();
            }else{
                this.store.Save();
            }
        }
        public string? Verify(Dictionary<C16,IH> model){
            var scanned=0;
            foreach(var row in this.handle!){
                scanned++;
                if(!model.TryGetValue(row.K,out var expected)){
                    return $"extra key {row.K}";
                }
                if(expected.First!=row.V.First||expected.Last!=row.V.Last||expected.Total!=row.V.Total){
                    return $"value of {row.K} differs";
                }
                var pad=row.Pad;
                var check=default(TPad);
                Hashing.Fill(ref check,row.V.First);
                if(Hashing.Of(pad)!=Hashing.Of(check)){
                    return $"padding of {row.K} differs";
                }
            }
            foreach(var pair in model){
                if(!this.handle.ContainsKey(pair.Key)){
                    return $"missing key {pair.Key}";
                }
            }
            var count=this.data.GetLongCount();
            if(scanned!=model.Count||count!=model.Count){
                return $"count scan={scanned} store={count} model={model.Count}";
            }
            return null;
        }
        public void Dispose(){
            this.handle?.Dispose();
            this.store.Dispose();
        }
    }
}
