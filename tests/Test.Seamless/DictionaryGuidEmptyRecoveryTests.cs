using Tsavorite.core;
using System.Diagnostics.CodeAnalysis;
using Seamless.Stores;
namespace Test.Seamless;

/// <summary>
/// indexCheckpointToken=Guid.EqualityEmpty によるリカバリーのテスト。
///
/// Tsavorite の RecoverAsync(indexToken, hybridLogToken) で indexToken=Guid.EqualityEmpty を渡すと：
/// - インデックスチェックポイントからの復元をスキップ
/// - ログを先頭から再生してインデックス（ハッシュテーブル）を再構築
///
/// 用途:
/// 1. TakeHybridLogCheckpointAsync でログのみ保存した場合（インデックスチェックポイントが存在しない）
/// 2. インデックスチェックポイントが破損した場合の復旧
/// 3. ストレージ節約（インデックス保存を省略、復旧時にログから再構築）
///
/// 注意:
/// - 引数なし RecoverAsync() は自動検出でインデックスチェックポイントも復元しようとするため、
///   フルチェックポイント後に使うとインデックス復元の問題が発生する場合がある。
/// - 確実な復元には Load(token) を使う（内部で Guid.EqualityEmpty + logToken で復元）。
/// - LoadLatest() はログのみチェックポイント（SaveLogOnly）との組み合わせで安全に使用可能。
/// </summary>
public class DictionaryGuidEmptyRecoveryTests{
    private static string CreateFolder()=>Path.Combine(Environment.CurrentDirectory,"SeamlessTest_"+Guid.NewGuid().ToString("N"));

    [Fact,Trait("Category","Unit")]
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void SaveLogOnly_Loadで指定検出(){
        // TakeHybridLogCheckpointAsync はログのみ保存（インデックスチェックポイントは作られない）
        // Load は内部で RecoverAsync(Guid.EqualityEmpty, logToken) を呼ぶ
        // → インデックスチェックポイントが無いのでログを先頭から再生して再構築
        var folder=CreateFolder();
        var checkpointDir=folder+"_checkpoint";
        Guid logToken;
        {
            using var LogDevice=Devices.CreateLogDevice(folder);
            using var LogObjDevice=Devices.CreateLogDevice(folder+"_obj");
            using var store=Factory.Create(CheckpointDir:checkpointDir).Create<int,int>(LogDevice,LogObjDevice);
            using var dict=store.Create();
            dict[1]=11;
            dict[2]=22;
            dict[3]=33;
            logToken=store.SaveLogOnly();
        }{
            using var LogDevice=Devices.CreateLogDevice(folder);
            using var LogObjDevice=Devices.CreateLogDevice(folder+"_obj");
            using var store=Factory.Create(CheckpointDir:checkpointDir).Create<int,int>(LogDevice,LogObjDevice);
            store.Load(logToken);
            using var dict=store.Create();
            Assert.True(dict.Contains(1));
            Assert.True(dict.Contains(2));
            Assert.True(dict.Contains(3));
            Assert.False(dict.Contains(99));
        }
    }

    [Fact,Trait("Category","Unit")]
    [SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification="<保留中>")]
    public void SaveLogOnly_Load自動検出(){
        // SaveLogOnly → LoadLatest: インデックスチェックポイントが無い場合、
        // FindRecoveryInfo が "No index checkpoint found, recovering from beginning of log" で
        // Guid.EqualityEmpty と同等の動作（ログから再構築）を自動的に行う
        var folder=CreateFolder();
        var checkpointDir=folder+"_checkpoint";
        {
            using var LogDevice=Devices.CreateLogDevice(folder);
            using var LogObjDevice=Devices.CreateLogDevice(folder+"_obj");
            using var store=Factory.Create(CheckpointDir:checkpointDir).Create<int,int>(LogDevice,LogObjDevice);
            using var dict=store.Create();
            dict[10]=1000;
            dict[20]=2000;
            store.SaveLogOnly();
        }{
            using var LogDevice=Devices.CreateLogDevice(folder);
            using var LogObjDevice=Devices.CreateLogDevice(folder+"_obj");
            using var store=Factory.Create(CheckpointDir:checkpointDir).Create<int,int>(LogDevice,LogObjDevice);
            // トークンを指定せず自動検出で復元
            // インデックスチェックポイントが無いのでログから再構築（Guid.EqualityEmptyと同等）
            store.Load();
            using var dict=store.Create();
            Assert.Equal(1000,dict[10]);
            Assert.Equal(2000,dict[20]);
            Assert.False(dict.Contains(99));
        }
    }
}
