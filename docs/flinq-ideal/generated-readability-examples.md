# FLinq 生成コードの改善前後

2026-10-25。同一の `Work/DemandPerf/Program.cs` を、変更前と変更後の生成器でそれぞれ強制再ビルドした実出力の抜粋（共通の字下げのみ整理）。説明用に変数名を書き換えた擬似コードではない。

## GroupBy の集計

元のクエリ:

```csharp
Data.GroupBy(x=>x%1000).Select(g=>g.Key*10+g.Count()).ToArray()
```

改善前:

```csharp
var pipelineSpan=pipelineSrc.GetSpan();
for(var pipelineIndex=0;pipelineIndex<pipelineSpan.Length;pipelineIndex++) {
    var sourceItem=pipelineSpan[pipelineIndex];
    int key2=default!;

    key2=sourceItem%1000;
    int groupElem=default!;
    groupElem=(int)(object?)sourceItem!;
    ref var acc=ref groupAggMap.GetValueRefOrAddDefault(key2,out var exists);
    acc++;
}
```

改善後:

```csharp
var pipelineSpan=pipelineSrc.GetSpan();
for(var pipelineIndex=0;pipelineIndex<pipelineSpan.Length;pipelineIndex++) {
    var x=pipelineSpan[pipelineIndex];
    int key2=x%1000;
    ref var aggregate=ref groupAggMap.GetValueRefOrAddDefault(key2,out _);
    aggregate++;
}
```

## Join の外側の走査

元のクエリ:

```csharp
Data.Join(Inner,x=>x%1000,y=>y,(x,y)=>x+y).Count()
```

改善前:

```csharp
for(var joinOuterRoot2Index=0;joinOuterRoot2Index<joinOuterRoot2Span.Length;joinOuterRoot2Index++){
    var joinOuterItem=joinOuterRoot2Span[joinOuterRoot2Index];
    int joinOuterKey=default!;

    joinOuterKey=joinOuterItem%1000;
    if(!__joinHasKey2(joinOuterKey)) continue;
    if(!joinLookup.TryGetRange(joinOuterKey,out var joinStart,out var joinEnd)) continue;
    for(;joinStart<joinEnd;joinStart++){
        int joinResult=default!;

        var y=joinArranged[joinStart];
        joinResult=joinOuterItem+y;
        countResult++;
    }
}
```

改善後:

```csharp
for(var joinOuterRootIndex=0;joinOuterRootIndex<joinOuterRootSpan.Length;joinOuterRootIndex++){
    var x=joinOuterRootSpan[joinOuterRootIndex];
    int joinOuterKey=x%1000;
    if(!joinHasKey(joinOuterKey)) continue;
    if(!joinLookup.TryGetRange(joinOuterKey,out var joinStart,out var joinEnd)) continue;
    for(;joinStart<joinEnd;joinStart++){
        int joinResult=default!;

        var y=joinArranged[joinStart];
        joinResult=x+y;
        countResult++;
    }
}
```

## メンバーと安全性

生成列挙子では `this.source`、`this.enumerator`、`this.Current` の形を使用する。今回、生成クラスのインスタンスメンバーも同じ後処理へ含めた。引数・ローカル・static メンバーには付けない。構造体内の入れ子ラムダがフィールドを使う場合は、必要な値をローカルへ写して取り込ませる。

Join の `joinResult=x+y` は Count の結果には使わないが、結果選択子の実行自体が要求されるため、任意に削除しない。評価の省略が例外や副作用を変える変更は、この可読性改善には含めない。

元のラムダ名が残っている他の宣言と衝突する場合は、役割名または番号付き名を使う。UnsafeAccessor と InterceptsLocation の契約名、補助型の識別用名は維持する。

## 検証
生成器ビルドは警告0・エラー0。関連回帰404件と、`_` の境界条件を補正した後の重点確認120件が成功した（重複を含む）。上記を含む9クエリの実行結果は System.Linq と一致した。速度向上の数値は今回測定していない。
