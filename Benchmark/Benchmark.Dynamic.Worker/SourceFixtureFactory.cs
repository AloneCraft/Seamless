using Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Worker;

public sealed class SourceFixture:IDisposable{
    private readonly IReadOnlyList<IDisposable> 破棄対象;
    private int 破棄済み;

    internal SourceFixture(
        SourceTarget 対象,
        object Primary,
        object Secondary,
        IReadOnlyList<int> PrimaryValues,
        IReadOnlyList<int> SecondaryValues,
        int PrimaryStart,
        int SecondaryStart,
        IReadOnlyList<IDisposable>? 破棄対象=null){
        this.Target=対象;
        this.Primary=Primary;
        this.Secondary=Secondary;
        this.PrimaryValues=PrimaryValues;
        this.SecondaryValues=SecondaryValues;
        this.PrimaryStart=PrimaryStart;
        this.SecondaryStart=SecondaryStart;
        this.RequiresSameThread=対象.RequiresSameThread();
        this.CreationThreadId=Environment.CurrentManagedThreadId;
        this.破棄対象=破棄対象??[];
    }

    public SourceTarget Target{get;}
    public object Primary{get;}
    public object Secondary{get;}
    public IReadOnlyList<int> PrimaryValues{get;}
    public IReadOnlyList<int> SecondaryValues{get;}
    public int PrimaryStart{get;}
    public int SecondaryStart{get;}
    public bool RequiresSameThread{get;}
    public int CreationThreadId{get;}
    public bool IsDisposed=>Volatile.Read(ref this.破棄済み)!=0;

    public void Dispose(){
        if(this.IsDisposed){
            return;
        }
        if(this.RequiresSameThread&&Environment.CurrentManagedThreadId!=this.CreationThreadId){
            throw new InvalidOperationException(
                $"Tsavorite fixture must be disposed on thread {this.CreationThreadId}, actual {Environment.CurrentManagedThreadId}.");
        }
        if(Interlocked.Exchange(ref this.破棄済み,1)!=0){
            return;
        }
        List<Exception>? 例外一覧=null;
        for(var インデックス=this.破棄対象.Count-1;インデックス>=0;インデックス--){
            try{
                this.破棄対象[インデックス].Dispose();
            }catch(Exception 例外){
                例外一覧??=[];
                例外一覧.Add(例外);
            }
        }
        if(例外一覧 is not null){
            throw new AggregateException("Source fixture disposal failed.",例外一覧);
        }
    }
}

public sealed class SourceFixtureFactory{
    public const int MaximumInputElementCount=100_000;

    public SourceFixture Create(SourceTarget 対象,int 要素数,Seed256 Seed,QueryElementType 要素型=QueryElementType.Int,bool 特殊な値を混ぜる=false){
        if(要素数<1||要素数>MaximumInputElementCount){
            throw new ArgumentOutOfRangeException(nameof(要素数));
        }
        var PrimaryStart=開始値を作成(Seed,"primary",要素数);
        var SecondaryStart=開始値を作成(Seed,"secondary",要素数);
        var PrimaryValues=Enumerable.Range(PrimaryStart,要素数).ToArray();
        var SecondaryValues=Enumerable.Range(SecondaryStart,要素数).ToArray();
        if(要素型!=QueryElementType.Int){
            return 型つきを作成(対象,要素型,Seed,PrimaryValues,SecondaryValues,PrimaryStart,SecondaryStart,特殊な値を混ぜる);
        }
        return 対象.GetSourceKind() switch{
            SourceKind.Array=>作成(
                対象,
                PrimaryValues.ToArray(),
                SecondaryValues.ToArray(),
                PrimaryValues,
                SecondaryValues,
                PrimaryStart,
                SecondaryStart),
            SourceKind.IEnumerable=>作成(
                対象,
                列挙(PrimaryValues),
                列挙(SecondaryValues),
                PrimaryValues,
                SecondaryValues,
                PrimaryStart,
                SecondaryStart),
            SourceKind.Range=>作成(
                対象,
                new RangeSourceFixture(PrimaryStart,要素数),
                new RangeSourceFixture(SecondaryStart,要素数),
                PrimaryValues,
                SecondaryValues,
                PrimaryStart,
                SecondaryStart),
            SourceKind.List=>作成(
                対象,
                PrimaryValues.ToList(),
                SecondaryValues.ToList(),
                PrimaryValues,
                SecondaryValues,
                PrimaryStart,
                SecondaryStart),
            SourceKind.Persisted=>Persistedを作成(
                対象,
                PrimaryValues,
                SecondaryValues,
                PrimaryStart,
                SecondaryStart),
            SourceKind.Tsavorite=>Tsavoriteを作成(
                対象,
                PrimaryValues,
                SecondaryValues,
                PrimaryStart,
                SecondaryStart),
            _=>throw new ArgumentOutOfRangeException(nameof(対象)),
        };
    }

    // int 以外の要素型。配列・List・IEnumerable だけを作る(Range は int 専用、永続系は対象外)。
    private static SourceFixture 型つきを作成(
        SourceTarget 対象,
        QueryElementType 要素型,
        Seed256 Seed,
        int[] PrimaryValues,
        int[] SecondaryValues,
        int PrimaryStart,
        int SecondaryStart,
        bool 特殊な値を混ぜる){
        var 記述子=ElementTypeCatalog.Get(要素型);
        var 種類=対象.GetSourceKind();
        if(!QueryCapabilityProfiles.SupportsElementType(対象,要素型)){
            throw new NotSupportedException($"{要素型} is not supported on {対象}.");
        }
        var Primary=型つきの源を作成(種類,記述子,記述子.CreateValues(PrimaryValues,Seed,0,特殊な値を混ぜる));
        var Secondary=型つきの源を作成(種類,記述子,記述子.CreateValues(SecondaryValues,Seed,1,特殊な値を混ぜる));
        return 作成(対象,Primary,Secondary,PrimaryValues,SecondaryValues,PrimaryStart,SecondaryStart);
    }

    private static object 型つきの源を作成(SourceKind 種類,ElementTypeDescriptor 記述子,object?[] 値一覧){
        var 配列=Array.CreateInstance(記述子.ClrType,値一覧.Length);
        for(var インデックス=0;インデックス<値一覧.Length;インデックス++){
            配列.SetValue(値一覧[インデックス],インデックス);
        }
        switch(種類){
            case SourceKind.Array:
                return 配列;
            case SourceKind.List:{
                var 一覧=(System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(記述子.ClrType))!;
                foreach(var 値 in 値一覧){
                    一覧.Add(値);
                }
                return 一覧;
            }
            case SourceKind.IEnumerable:
                return typeof(SourceFixtureFactory)
                    .GetMethod(nameof(型つきを列挙),System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(記述子.ClrType)
                    .Invoke(null,[配列])!;
            default:
                throw new NotSupportedException($"{種類} is not supported for typed fixtures.");
        }
    }

    private static IEnumerable<T> 型つきを列挙<T>(T[] 値一覧){
        foreach(var 値 in 値一覧){
            yield return 値;
        }
    }

    private static SourceFixture Persistedを作成(
        SourceTarget 対象,
        int[] PrimaryValues,
        int[] SecondaryValues,
        int PrimaryStart,
        int SecondaryStart){
        global::Seamless.Collections.Persisted.Set<int>? Primary=null;
        global::Seamless.Collections.Persisted.Set<int>? Secondary=null;
        try{
            Primary=global::Seamless.Collections.Persisted.Store.CreateTempSet<int>(
                InitialCapacity:Math.Max(16,PrimaryValues.Length),
                InitialDataCapacity:Math.Max(4_096,PrimaryValues.Length*sizeof(int)));
            Secondary=global::Seamless.Collections.Persisted.Store.CreateTempSet<int>(
                InitialCapacity:Math.Max(16,SecondaryValues.Length),
                InitialDataCapacity:Math.Max(4_096,SecondaryValues.Length*sizeof(int)));
            foreach(var 値 in PrimaryValues){
                Primary.Add(値);
            }
            foreach(var 値 in SecondaryValues){
                Secondary.Add(値);
            }
            return 作成(
                対象,
                Primary,
                Secondary,
                PrimaryValues,
                SecondaryValues,
                PrimaryStart,
                SecondaryStart,
                [Primary,Secondary]);
        }catch{
            Secondary?.Dispose();
            Primary?.Dispose();
            throw;
        }
    }

    private static SourceFixture Tsavoriteを作成(
        SourceTarget 対象,
        int[] PrimaryValues,
        int[] SecondaryValues,
        int PrimaryStart,
        int SecondaryStart){
        global::Seamless.Collections.Tsavorite.HashSetStore<int>? PrimaryStore=null;
        global::Seamless.Collections.Tsavorite.HashSetStore<int>? SecondaryStore=null;
        global::Seamless.Collections.Tsavorite.HashSet<int>? Primary=null;
        global::Seamless.Collections.Tsavorite.HashSet<int>? Secondary=null;
        try{
            PrimaryStore=global::Seamless.Collections.Tsavorite.Store.Create().Create<int>();
            SecondaryStore=global::Seamless.Collections.Tsavorite.Store.Create().Create<int>();
            Primary=PrimaryStore.Create();
            Secondary=SecondaryStore.Create();
            foreach(var 値 in PrimaryValues){
                Primary.Add(値);
            }
            foreach(var 値 in SecondaryValues){
                Secondary.Add(値);
            }
            return 作成(
                対象,
                Primary,
                Secondary,
                PrimaryValues,
                SecondaryValues,
                PrimaryStart,
                SecondaryStart,
                [PrimaryStore,SecondaryStore,Primary,Secondary]);
        }catch{
            Secondary?.Dispose();
            Primary?.Dispose();
            SecondaryStore?.Dispose();
            PrimaryStore?.Dispose();
            throw;
        }
    }

    private static SourceFixture 作成(
        SourceTarget 対象,
        object Primary,
        object Secondary,
        IReadOnlyList<int> PrimaryValues,
        IReadOnlyList<int> SecondaryValues,
        int PrimaryStart,
        int SecondaryStart,
        IReadOnlyList<IDisposable>? 破棄対象=null){
        return new(
            対象,
            Primary,
            Secondary,
            PrimaryValues,
            SecondaryValues,
            PrimaryStart,
            SecondaryStart,
            破棄対象);
    }

    private static IEnumerable<int> 列挙(IReadOnlyList<int> 値一覧){
        foreach(var 値 in 値一覧){
            yield return 値;
        }
    }

    private static int 開始値を作成(Seed256 Seed,string 用途,int 要素数){
        var 派生=SeedDerivation.Derive(Seed,"fixture."+用途);
        var 乱数=new DeterministicPrng(派生);
        var 中央寄せ開始=-(要素数/2);
        var 小さいずれ=乱数.NextInt32(-8,9);
        return checked(中央寄せ開始+小さいずれ);
    }
}
