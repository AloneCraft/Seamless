using System.Collections.Concurrent;
using Sample.NetShop.Core;
using 対象=Sample.NetShop.Core.物語の再生器;
using 設定=Sample.NetShop.Core.実行設定;

namespace Sample.NetShop.Tests.Story;

public sealed class 物語の再生器{
    private static 設定 最小設定(テスト用フォルダ フォルダ)=>new(){
        データフォルダ=フォルダ.下("data"),
        作業フォルダ=フォルダ.下("work"),
        決算の注文件数=10_000,
        低メモリか=true,
    };

    [Fact,Trait("Category","Integration")]public async Task 再生Async(){
        using var フォルダ=new テスト用フォルダ();
        var 最小=最小設定(フォルダ);
        {
            //全章を待たずに1周し、どの章も失敗しない。作業フォルダは残らない
            var 再生器=new 対象(最小){速度=0};
            var 最後の状態=new ConcurrentDictionary<int,章の状態>();
            var 報告の数=0;
            再生器.章状態変化時+=状態=>最後の状態[状態.番号]=状態;
            再生器.報告時+=(_,_)=>Interlocked.Increment(ref 報告の数);
            await 再生器.再生Async(1,CancellationToken.None);
            for(var 番号=0;番号<再生器.章一覧.Count;番号++){
                var 状態=最後の状態[番号];
                Assert.True(状態.進行==章の進行.完了,$"{再生器.章一覧[番号].題名}: {状態.進行} {状態.失敗理由}");
            }
            Assert.True(報告の数>0);
            Assert.Empty(Directory.GetDirectories(最小.作業フォルダ));
        }
        {
            //同じデータフォルダで再生し直すと、商店DBは前回の続きから始まる
            var 再生器=new 対象(最小){速度=0};
            var ログ=new ConcurrentQueue<string>();
            再生器.報告時+=(_,報告)=>{
                if(報告 is ログ報告 行){
                    ログ.Enqueue(行.文);
                }
            };
            await 再生器.再生Async(1,CancellationToken.None);
            Assert.Contains(ログ,文=>文.StartsWith("前回の続きから",StringComparison.Ordinal));
        }
    }

    [Fact,Trait("Category","Integration")]public async Task 途中の章から始める(){
        using var フォルダ=new テスト用フォルダ();
        {
            //3章(商品と顧客を登録する章)を飛ばして4章から始めても、注文処理は空の店ではなく商品と顧客のある店で動く
            var 再生器=new 対象(最小設定(フォルダ) with{開始章=3}){速度=0};
            var ログ=new ConcurrentQueue<string>();
            再生器.報告時+=(_,報告)=>{
                if(報告 is ログ報告 行){
                    ログ.Enqueue(行.文);
                }
            };
            await 再生器.再生Async(1,CancellationToken.None);
            Assert.DoesNotContain(ログ,文=>文.Contains("は登録されていません",StringComparison.Ordinal));
            Assert.Contains(ログ,文=>文.StartsWith("✔ 注文#",StringComparison.Ordinal));
        }
        using var フォルダ2=new テスト用フォルダ();
        {
            //最後の章(停電からの復旧)から始めても、取引の中の注文は登録に成功する
            var 再生器=new 対象(最小設定(フォルダ2) with{開始章=7}){速度=0};
            var ログ=new ConcurrentQueue<string>();
            再生器.報告時+=(_,報告)=>{
                if(報告 is ログ報告 行){
                    ログ.Enqueue(行.文);
                }
            };
            await 再生器.再生Async(1,CancellationToken.None);
            Assert.Contains(ログ,文=>文.Contains("成功: True",StringComparison.Ordinal));
        }
    }

    [Fact,Trait("Category","Integration")]public async Task 章へ移動(){
        using var フォルダ=new テスト用フォルダ();
        {
            //一時停止中に移動すると、実行中の章を打ち切って指定の章から続ける
            var 再生器=new 対象(最小設定(フォルダ)){速度=1};
            var 始まった章=new ConcurrentQueue<int>();
            var 最初の章が始まった=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            再生器.章状態変化時+=状態=>{
                if(状態.進行==章の進行.実行中){
                    始まった章.Enqueue(状態.番号);
                    最初の章が始まった.TrySetResult();
                }
            };
            using var 取消=new CancellationTokenSource();
            var 再生=再生器.再生Async(null,取消.Token);
            await 最初の章が始まった.Task.WaitAsync(TimeSpan.FromSeconds(30));
            再生器.一時停止();
            Assert.True(再生器.一時停止中か);
            var 最後=再生器.章一覧.Count-1;
            再生器.章へ移動(最後);
            Assert.False(再生器.一時停止中か);
            var 期限=DateTime.UtcNow.AddSeconds(30);
            while(!始まった章.Contains(最後)&&DateTime.UtcNow<期限){
                await Task.Delay(50);
            }
            取消.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>再生);
            Assert.Equal([0,最後],始まった章.Take(2));
        }
    }
}
