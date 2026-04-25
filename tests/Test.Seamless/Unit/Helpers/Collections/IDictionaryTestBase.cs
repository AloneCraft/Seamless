using Seamless;
using Seamless.Collections;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Tsavorite.core;
namespace Test.Seamless.Unit.Helpers.Collections;
/// <summary>
/// <see cref="IDictionary{TKey,TValue}"/> �C���^�[�t�F�[�X�o�R�ł̃e�X�g���N���X�B
/// <see cref="DictionaryTestBase{TStrategy,TKey,TValue}"/> �͂��̃N���X��p�����A
/// <see cref="ISeamlessDictionary{TKey,TValue}"/> �ŗL�̃e�X�g��ǉ�����B
/// </summary>
public abstract class IDictionaryTestBase<TStrategy, TKey, TValue>
    where TStrategy : IDictTestStrategy<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private static string CreateDiskFolder() =>
        Path.Combine(Environment.CurrentDirectory,"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    [Fact, Trait("Category","Unit")]
    public void IsReadOnly(){
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        IDictionary<TKey,TValue> dict = rawDict;
        {
            // �ǂݎ���p�ł͂Ȃ�����
            Assert.False(dict.IsReadOnly);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Add(){
        {
            // �ǉ����������邱��
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // �d���L�[�ł͗�O�ɂȂ邱��
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.Throws<ArgumentException>(() => dict.Add(TStrategy.K(1),TStrategy.V(2)));
        }
        {
            // ���O���t���͗�O�ɂȂ邱��
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<OutOfMemoryException>((Action)(() => {
                for(var i = 0; ; i++) dict.Add(TStrategy.K(i),TStrategy.V(i));
            }));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Add_KeyValuePair(){
        {
            // KeyValuePair �Œǉ��ł��邱��
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(new KeyValuePair<TKey,TValue>(TStrategy.K(5),TStrategy.V(5)));
            Assert.True(dict.ContainsKey(TStrategy.K(5)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Contains(){
        {
            // �L�[�ƒl����v����v�f�� true
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.Contains(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.V(1))));
        }
        {
            // �s��v�v�f�� false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.Contains(new KeyValuePair<TKey,TValue>(TStrategy.K(99),TStrategy.VWrong())));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void CopyTo(){
        {
            // �S�v�f��z��ɃR�s�[�ł��邱��
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            dict.Add(TStrategy.K(10),TStrategy.V(10));
            var array = new KeyValuePair<TKey,TValue>[2];
            dict.CopyTo(array,0);
            Assert.Equal(2,array.Length);
        }
        {
            // null �z��͗�O
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<ArgumentNullException>(() => dict.CopyTo(null!,0));
        }
        {
            // ���̃C���f�b�N�X�͗�O
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<ArgumentOutOfRangeException>(() => dict.CopyTo(new KeyValuePair<TKey,TValue>[2],-1));
        }
        {
            // �z�񂪏���������Ɨ�O
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            dict.Add(TStrategy.K(2),TStrategy.V(2));
            Assert.Throws<ArgumentException>(() => dict.CopyTo(new KeyValuePair<TKey,TValue>[1],0));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void ToList(){
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        var list = dict.ToList();
        {
            // �S�G���g����Ԃ�����
            Assert.Equal(2,list.Count);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Keys(){
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // �L�[�ꗗ��擾�ł��邱��
            Assert.Equivalent(new[] { TStrategy.K(1),TStrategy.K(2) },dict.Keys);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Values(){
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // �l�ꗗ��擾�ł��邱��
            Assert.Equivalent(new[] { TStrategy.V(1),TStrategy.V(2) },dict.Values);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void ContainsKey(){
        {
            // ���݂���L�[�� true
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // ���݂��Ȃ��L�[�� false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.ContainsKey(TStrategy.K(999)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void TryGetValue(){
        {
            // �L�[����v����Ƃ��̃e�X�g
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(3),TStrategy.V(3));
            Assert.True(dict.TryGetValue(TStrategy.K(3),out var val));
            Assert.Equal(TStrategy.V(3),val);
        }
        {
            // �L�[����v���Ȃ��Ƃ��̃e�X�g
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.TryGetValue(TStrategy.K(0),out var val));
            Assert.Equal(default,val);
        }
        {
            // ��������ł� Read �p�X���������邱��
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.TryGetValue(TStrategy.K(1),out var v));
            Assert.Equal(TStrategy.V(1),v);
        }
        {
            // FlushAndEvict ��̃R�[���h�f�[�^��ǂ߂邱��
            var folder = CreateDiskFolder();
            using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var sh = TStrategy.CreateStorageStore(logDevice,objDevice);
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(0),TStrategy.V(0));
            sh.FlushAndEvict(true);
            Assert.True(dict.TryGetValue(TStrategy.K(0),out var v));
            Assert.Equal(TStrategy.V(0),v);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Item(){
        {
            // setter �Œl��ݒ�ł��邱��
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict[TStrategy.K(10)]=TStrategy.V(10);
            Assert.True(dict.TryGetValue(TStrategy.K(10),out var v));
            Assert.Equal(TStrategy.V(10),v);
        }
        {
            // getter �ŕs���݃L�[�Q�Ǝ��͗�O
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<KeyNotFoundException>(() => {
                var _ = dict[TStrategy.K(999)];
            });
        }
        {
            // ���O���t���� setter �͗�O
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<OutOfMemoryException>((Action)(() => {
                for(var i = 0; ; i++) dict[TStrategy.K(i)]=TStrategy.V(i);
            }));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Remove(){
        {
            // ���݃L�[�̍폜���������邱��
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.Remove(TStrategy.K(1)));
            Assert.False(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // ���݂��Ȃ��L�[�� false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            Assert.False(dict.Remove(TStrategy.K(99)));
        }
        {
            // Write �p�X�̍폜���������邱��
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.True(dict.Remove(TStrategy.K(1)));
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Remove_KeyValuePair(){
        {
            // �L�[�ƒl����v����ƍ폜����
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var removed = dict.Remove(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.V(1)));
            foreach(var _ in dict){ }
            Assert.True(removed);
            Assert.False(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // �l�s��v�Ȃ�폜���Ȃ�
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            var removed = dict.Remove(new KeyValuePair<TKey,TValue>(TStrategy.K(1),TStrategy.VWrong()));
            Assert.False(removed);
            Assert.True(dict.ContainsKey(TStrategy.K(1)));
        }
        {
            // �L�[�s���݂Ȃ� false
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            var removed = dict.Remove(new KeyValuePair<TKey,TValue>(TStrategy.K(99),TStrategy.VWrong()));
            Assert.False(removed);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Clear(){
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // �N���A��͌����� 0 �ɂȂ邱��
            dict.Clear();
            Assert.Empty(dict);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void Count(){
        using var sh = TStrategy.CreateMemoryStore();
        using var rawDict = sh.NewDict();
        IDictionary<TKey,TValue> dict = rawDict;
        dict.Add(TStrategy.K(1),TStrategy.V(1));
        dict.Add(TStrategy.K(2),TStrategy.V(2));
        {
            // �������ǉ����ƈ�v���邱��
            Assert.Equal(2,dict.Count);
        }
    }
    [Fact, Trait("Category","Unit")]
    public void GetEnumerator(){
        {
            // IEnumerable<T> �o�R�ŗ񋓂ł��邱��
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            IEnumerable<KeyValuePair<TKey,TValue>> e = dict;
            Assert.Single(e);
        }
        {
            // IEnumerable �o�R�ŗ񋓂ł��邱��
            using var sh = TStrategy.CreateMemoryStore();
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            IEnumerable e = dict;
            var count = 0;
            var en = e.GetEnumerator();
            while(en.MoveNext()) count++;
            Assert.Equal(1,count);
        }
    }
    [Fact, Trait("Category","Unit")]
    [SuppressMessage("Interoperability","CA1416:�v���b�g�t�H�[���̌݊��������",Justification = "<�ۗ���>")]
    public void WouldOverflowMutable(){
        {
            // mutableFraction=0 �ł��{����ł��邱��
            var folder = CreateDiskFolder();
            using var logDevice = new ManagedLocalStorageDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var objDevice = new ManagedLocalStorageDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var sh = TStrategy.CreateStorageStore(logDevice,objDevice,memorySize: Const.MinObjectLogCapacity,mutableFraction: 0);
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            dict.Add(TStrategy.K(1),TStrategy.V(1));
            Assert.Equal(TStrategy.V(1),dict[TStrategy.K(1)]);
        }
        {
            // mutable �̈��g���؂�� OutOfMemoryException
            using var sh = TStrategy.CreateMemoryStore(memorySize: Const.LogMemorySize,mutableFraction: 0.5);
            using var rawDict = sh.NewDict();
            IDictionary<TKey,TValue> dict = rawDict;
            Assert.Throws<OutOfMemoryException>((Action)(() => {
                for(var i = 0; ; i++) dict.Add(TStrategy.K(i),TStrategy.V(i));
            }));
        }
    }
    [Fact, Trait("Category","Unit")]
    [SuppressMessage("Interoperability","CA1416:�v���b�g�t�H�[���̌݊��������",Justification = "<�ۗ���>")]
    public void SaveLoad(){
        var folder = CreateDiskFolder();
        var checkpointDir = folder+"_checkpoint";
        Guid token;
        {
            using var logDevice = new ManagedLocalStorageDevice(folder);
            using var logObjDevice = new ManagedLocalStorageDevice(folder+"_obj");
            using var sh = TStrategy.CreateStorageStore(logDevice,logObjDevice,checkpointDir: checkpointDir);
            using var rawSet = sh.NewDict();
            IDictionary<TKey,TValue> set = rawSet;
            set[TStrategy.K(1)]=TStrategy.V(1);
            set[TStrategy.K(2)]=TStrategy.V(2);
            set[TStrategy.K(3)]=TStrategy.V(3);
            Assert.Equal(3,set.Count);
            Assert.True(set.ContainsKey(TStrategy.K(1)));
            token=sh.Save();
            Assert.True(set.ContainsKey(TStrategy.K(1)));
        }
        {
            using var logDevice = new ManagedLocalStorageDevice(folder);
            using var logObjDevice = new ManagedLocalStorageDevice(folder+"_obj");
            using var sh = TStrategy.CreateStorageStore(logDevice,logObjDevice,checkpointDir: checkpointDir,recoverDevice: true);
            sh.Load(token);
            var (begin2,head2,ro2,tail2)=sh.Address;
            using var rawSet = sh.NewDict();
            IDictionary<TKey,TValue> set = rawSet;
            Assert.True(set.ContainsKey(TStrategy.K(1)),$"ContainsKey(K(1)) failed. Count(iter)={set.Count}, Addr=({begin2},{head2},{ro2},{tail2})");
            Assert.True(set.ContainsKey(TStrategy.K(2)));
            Assert.True(set.ContainsKey(TStrategy.K(3)));
            Assert.False(set.ContainsKey(TStrategy.K(99)));
        }
    }
}
