namespace Sample.FLinqChainVerifier.Core;

/// <summary>左列に表示する検証段階の説明定義。</summary>
public sealed record 検証段階説明(
    検証段階 段階,
    string 題名,
    string 副題,
    string 説明);

/// <summary>右列に表示する全体統計。</summary>
public sealed record 検証統計(
    int 総実行数,
    int 合格数,
    int 展開済数,
    int 結果一致数,
    int 非劣化数,
    int 警告または失敗数);

/// <summary>
/// 段階1(2段ペア網羅) -> 段階2(代表3〜4段チェーン) -> 段階3(全組み合わせ自動生成) -> 段階4(長尺ランダム無限探索)
/// を自動または選択操作で実行し、UIへリアルタイム通知する再生器。
/// </summary>
public sealed class 検証再生器{
    private readonly 動的検証ランナー ランナー=new();
    private readonly object 排他=new();
    private volatile bool 一時停止要求;
    private int 移動先段階=-1;
    private int 総実行数;
    private int 合格数;
    private int 展開済数;
    private int 結果一致数;
    private int 非劣化数;
    private int 警告または失敗数;

    public 検証再生器(string? 失敗テスト保存先フォルダ=null){
        this.ランナー.失敗テスト保存先フォルダ=失敗テスト保存先フォルダ;
        this.段階一覧=[
            new(
                検証段階.ペア網羅,
                "2段ペア網羅 (A.B)",
                "単体メソッドペア + 手書き理想コード比較",
                "入力ソースから呼べる2段ペア(A.B)を実行し、手書き理想コードと結果・展開構造・性能が一致するかを確認します。"),
            new(
                検証段階.代表チェーン,
                "代表3〜4段チェーン (A.B.C.D)",
                "A.B, B.C, C.D の組み合わせと理想コード比較",
                "6つの基本プリミティブを組み合わせた3〜4段チェーン(A.B.C.D)が、手書きの理想コードと同等に置換されることを確認します。"),
            new(
                検証段階.全組み合わせ自動生成,
                "全組み合わせ自動生成",
                "展開確認・結果一致・ペア合成からの理想特性推定",
                "メソッドの全組み合わせを自動生成し、フォールバックが0であること、入力と結果が一致すること、2メソッドペア情報から推定した理想特性を満たすことを確認します。"),
            new(
                検証段階.ランダム無限探索,
                "長尺ランダム無限探索",
                "延々と実行・ログ蓄積・シードからテスト生成",
                "さらに長い多段メソッドチェーンをシード値からランダマイズ自動生成して延々と検証し続けます。問題検出時は原因と改善案、およびシード再現用の単体テストコードを生成します。"),
        ];
    }

    public IReadOnlyList<検証段階説明>段階一覧{get;}
    public 入力ソース種別 現在のソース種別{get;set;}=入力ソース種別.Array;
    public ulong 基準シード{get;set;}=0x1018_2026UL;
    public double 速度{get;set;}=1.0;

    public bool 一時停止中か=>this.一時停止要求;

    public event Action<検証段階>? 段階変化時;
    public event Action<チェーン検証結果,検証統計>? 結果報告時;

    public void 一時停止()=>this.一時停止要求=true;
    public void 再開()=>this.一時停止要求=false;

    public void 段階へ移動(検証段階 段階){
        lock(this.排他){
            this.移動先段階=(int)段階;
        }
    }

    /// <summary>指定したシード値・試行番号・段数で単発のチェーンを即時検証する。</summary>
    public チェーン検証結果 シード指定で1件検証(入力ソース種別 ソース種別,ulong シード値,int 試行番号){
        var 項目=チェーン生成器.段階4ランダムチェーンを生成(ソース種別,シード値,試行番号);
        var 結果=this.ランナー.検証を実行(項目);
        var 統計=this.統計を更新(結果);
        this.結果報告時?.Invoke(結果,統計);
        return 結果;
    }

    /// <summary>UIまたはバックグラウンドから継続的に検証を回すメインループ。</summary>
    public async Task 再生Async(CancellationToken 取消トークン){
        var 現在段階=検証段階.ペア網羅;
        while(!取消トークン.IsCancellationRequested){
            if(this.移動先を取り出す(out var 指定段階)){
                現在段階=指定段階;
            }
            this.段階変化時?.Invoke(現在段階);

            var 途中で移動したか=await this.段階を実行Async(現在段階,取消トークン);
            if(!途中で移動したか){
                if(現在段階==検証段階.ペア網羅){
                    現在段階=検証段階.代表チェーン;
                }else if(現在段階==検証段階.代表チェーン){
                    現在段階=検証段階.全組み合わせ自動生成;
                }else if(現在段階==検証段階.全組み合わせ自動生成){
                    現在段階=検証段階.ランダム無限探索;
                }else{
                    // 段階4は延々と繰り返す
                    現在段階=検証段階.ランダム無限探索;
                }
            }
        }
    }

    private async Task<bool>段階を実行Async(検証段階 段階,CancellationToken 取消トークン){
        if(段階==検証段階.ペア網羅){
            var 一覧=チェーン生成器.段階1ペア網羅を生成(this.現在のソース種別,this.基準シード);
            foreach(var 項目 in 一覧){
                if(await this.一件処理して移動判定Async(項目,取消トークン)){
                    return true;
                }
            }
            return false;
        }

        if(段階==検証段階.代表チェーン){
            var 一覧=チェーン生成器.段階2代表チェーンを生成(this.現在のソース種別,this.基準シード);
            foreach(var 項目 in 一覧){
                if(await this.一件処理して移動判定Async(項目,取消トークン)){
                    return true;
                }
            }
            return false;
        }

        if(段階==検証段階.全組み合わせ自動生成){
            var 件数上限=60;
            var カウント=0;
            foreach(var 項目 in チェーン生成器.段階3全組み合わせを生成(this.現在のソース種別,this.基準シード)){
                if(await this.一件処理して移動判定Async(項目,取消トークン)){
                    return true;
                }
                カウント++;
                if(カウント>=件数上限){
                    break;
                }
            }
            return false;
        }

        // 段階4: ランダム無限探索 (段階移動されるかキャンセルされるまで延々と実行)
        var 試行番号=0;
        while(!取消トークン.IsCancellationRequested){
            var 項目=チェーン生成器.段階4ランダムチェーンを生成(this.現在のソース種別,this.基準シード,試行番号);
            if(await this.一件処理して移動判定Async(項目,取消トークン)){
                return true;
            }
            試行番号++;
        }
        return false;
    }

    private async Task<bool>一件処理して移動判定Async(チェーン検証項目 項目,CancellationToken 取消トークン){
        while(this.一時停止要求&&!取消トークン.IsCancellationRequested){
            if(this.移動先があるか()){
                return true;
            }
            await Task.Delay(80,取消トークン);
        }
        取消トークン.ThrowIfCancellationRequested();
        if(this.移動先があるか()){
            return true;
        }

        // 現在のソース種別に合わせた項目へ更新
        var 実行項目=項目;
        if(項目.ソース種別!=this.現在のソース種別){
            実行項目=チェーン生成器.項目を構築(
                項目.段階,
                this.現在のソース種別,
                項目.シード値,
                項目.試行番号,
                項目.メソッド名一覧,
                項目.手書き理想コード);
        }

        var 結果=this.ランナー.検証を実行(実行項目);
        var 統計=this.統計を更新(結果);
        this.結果報告時?.Invoke(結果,統計);

        var 速度倍率=this.速度;
        if(速度倍率>0){
            var 待機ミリ秒=(int)Math.Clamp(700.0/速度倍率,30.0,2000.0);
            var 経過=0;
            while(経過<待機ミリ秒&&!取消トークン.IsCancellationRequested){
                if(this.移動先があるか()){
                    return true;
                }
                await Task.Delay(50,取消トークン);
                経過+=50;
            }
        }
        return this.移動先があるか();
    }

    private 検証統計 統計を更新(チェーン検証結果 結果){
        lock(this.排他){
            this.総実行数++;
            if(結果.完全合格か){
                this.合格数++;
            }else{
                this.警告または失敗数++;
            }
            if(結果.全メソッド展開済みか&&結果.実行時フォールバック数==0){
                this.展開済数++;
            }
            if(結果.全データ結果一致か){
                this.結果一致数++;
            }
            if(結果.パフォーマンス合格か){
                this.非劣化数++;
            }
            return new 検証統計(
                this.総実行数,
                this.合格数,
                this.展開済数,
                this.結果一致数,
                this.非劣化数,
                this.警告または失敗数);
        }
    }

    private bool 移動先があるか(){
        lock(this.排他){
            return this.移動先段階>=0;
        }
    }

    private bool 移動先を取り出す(out 検証段階 段階){
        lock(this.排他){
            if(this.移動先段階>=0){
                段階=(検証段階)this.移動先段階;
                this.移動先段階=-1;
                return true;
            }
            段階=検証段階.ペア網羅;
            return false;
        }
    }
}
