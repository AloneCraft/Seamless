using Tsavorite.core;
using System.Diagnostics.CodeAnalysis;
namespace Tsavorite.Collections;

/// <summary>
/// Tests recovery when only the hybrid log checkpoint exists and the index checkpoint is absent.
/// </summary>
public class DictionaryGuidEmptyRecoveryTests {
    private static string CreateFolder() => TestPath.Directory<DictionaryGuidEmptyRecoveryTests>();

    [BenchFact,Benchmark]
    [SuppressMessage("Interoperability","CA1416:Validate platform compatibility",Justification = "Test uses configured local devices.")]
    public void SaveLogOnly_LoadWithExplicitToken() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid logToken;
        {
            using var LogDevice = Devices.CreateLogDevice(folder);
            using var LogObjDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,int>(LogDevice,LogObjDevice);
            using var dict = store.Create();
            dict[1]=11;
            dict[2]=22;
            dict[3]=33;
            logToken=store.SaveLogOnly();
        }
        {
            using var LogDevice = Devices.CreateLogDevice(folder);
            using var LogObjDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,int>(LogDevice,LogObjDevice);
            store.Load(logToken);
            using var dict = store.Create();
            Assert.True(dict.Contains(1));
            Assert.True(dict.Contains(2));
            Assert.True(dict.Contains(3));
            Assert.False(dict.Contains(99));
        }
    }

    [BenchFact,Benchmark]
    [SuppressMessage("Interoperability","CA1416:Validate platform compatibility",Justification = "Test uses configured local devices.")]
    public void SaveLogOnly_LoadLatest() {
        var folder = CreateFolder();
        var checkpointDir = folder+"_checkpoint";
        {
            using var LogDevice = Devices.CreateLogDevice(folder);
            using var LogObjDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,int>(LogDevice,LogObjDevice);
            using var dict = store.Create();
            dict[10]=1000;
            dict[20]=2000;
            store.SaveLogOnly();
        }
        {
            using var LogDevice = Devices.CreateLogDevice(folder);
            using var LogObjDevice = Devices.CreateLogDevice(folder+"_obj");
            using var store = Store.Create(CheckpointDir: checkpointDir).Create<int,int>(LogDevice,LogObjDevice);
            store.Load();
            using var dict = store.Create();
            Assert.Equal(1000,dict[10]);
            Assert.Equal(2000,dict[20]);
            Assert.False(dict.Contains(99));
        }
    }
}