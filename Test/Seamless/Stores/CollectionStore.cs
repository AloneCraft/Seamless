using Seamless.Collections.Tsavorite.Stores;
namespace Test.Seamless.Stores;
/// <summary>
/// src/Seamless.Collections.Tsavorite/Store/CollectionStore`2.cs �̑S�����J�o�[����B
/// �����e�X�g(CoverInternals��)�ŃJ�o�[���Ă��Ȃ�����ɒ��͂���B
/// </summary>

public class CollectionStore {
    private static string CreateFolder() => TestPath.Directory<CollectionStore>();
    private const long TestMemorySize = global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity;
    //CollectionStore �R���X�g���N�^: ObjectLogDevice.Capacity<0 �̕���
    //Capacity=-1�̃f�o�C�X��n����ObjectLogSegmentSize=Const.MinObjectLogCapacity�ɂȂ�
    [Fact, Trait("Category","Unit")]
    public void Constructor_ObjectLogCapacity_Negative() {
        var folder = CreateFolder();
        using var LogDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        //Capacity��w�肵�Ȃ��ƃf�t�H���g=-1�ɂȂ�(Capacity<0�����ʂ�)
        using var ObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true);
        //Capacity<0 �� ObjectLogSegmentSize=Const.MinObjectLogCapacity �Ƃ��Đ���ɍ\�z����邱��
        using var store = Store.Create(MemorySize: TestMemorySize).Create<int,string>(LogDevice,ObjDevice);
        using var dict = store.Create();
        dict.Add(1,"a");
        Assert.Equal("a",dict[1]);
    }
    //CollectionFactory.CompactWithLookup: CompactUntil<=BeginAddress �̑���return����
    //return�̌��2.0���ƃJ�o�[�ł���
    //�f�[�^�Ȃ��܂��͒����2��ڌĂяo����SafeReadOnlyAddress<CompactUntil�ƂȂ�ꍇ
    [Fact, Trait("Category","Unit")]
    public void CompactWithLookup_EarlyReturn() {
        var folder = CreateFolder();
        using var LogDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        using var store = Store.Create(MemorySize: TestMemorySize,MutableFraction: 0.1).Create<int,string>(LogDevice,ObjDevice);
        using var dict = store.Create();
        //�f�[�^����������CompactWithLookup����s
        for(var a = 0; a<1000; a++) {
            dict.Add(a,$"v{a}");
        }
        {
            var before = store.Address;
            store.CompactWithLookup();
            var after = store.Address;
            Assert.True(after.BeginAddress>=before.BeginAddress);
        }
        //2��ڌĂяo��: BeginAddress���i�񂾌�ɂ���ɌĂяo��
        //SafeReadOnlyAddress��BeginAddress�ȉ��ɂȂ��CompactUntil<=BeginAddress�̑���return����������
        {
            var beforeSecond = store.Address;
            store.CompactWithLookup();
            var afterSecond = store.Address;
            //��O�������������튮�����邱�Ƃ�m�F
            Assert.True(afterSecond.BeginAddress>=beforeSecond.BeginAddress);
        }
        //�A���Ăяo��: ����return��else�o�H��J�o�[ (BeginAddress=CompactUntil�ɂȂ�ꍇ)
        store.CompactWithLookup();
        store.CompactWithLookup();
    }
    //1.1.1CollectionFactory.CompactWithLookup: CompactUntil>SafeReadOnlyAddress �ŃN���b�v����镪��
    [Fact, Trait("Category","Unit")]
    public void CompactWithLookup() {
        //1.1.1��
        var folder = CreateFolder();
        using var LogDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        using var store = Store.Create(MemorySize: TestMemorySize,MutableFraction: 0.1).Create<int,string>(LogDevice,ObjDevice);
        using var dict = store.Create();
        //�f�[�^����������CompactWithLookup����s
        for(var a = 0; a<1000; a++) {
            dict.Add(a,$"v{a}");
        }
        //1.1.1�ł�ReadOnlyAddress�̐i�s���o�b�t�@�e�ʃx�[�X�̂���10000���ł�SafeReadOnlyAddress��
        //BeginAddress�𒴂�������return�ɂȂ�BShiftReadOnlyAddress�ŋ����I��TailAddress�܂�
        //�ǂݎ���p�ɂ���SafeReadOnlyAddress��i�߁ACompactWithLookup�����ۂɃR���p�N�V������
        //���s�ł����Ԃɂ���B2.0beta3�ł͊���ReadOnlyAddress���i��ł��邽�ߙp���Ŗ��Q�B
        store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,true);
        {
            var before = store.Address;
            store.CompactWithLookup();
            var after = store.Address;
            Assert.True(after.BeginAddress>=before.BeginAddress);
        }
        //2��ڌĂяo��: BeginAddress���i�񂾌�ɂ���ɌĂяo��
        //SafeReadOnlyAddress��BeginAddress�ȉ��ɂȂ��CompactUntil<=BeginAddress�̑���return����������
        {
            var beforeSecond = store.Address;
            store.CompactWithLookup();
            var afterSecond = store.Address;
            //��O�������������튮�����邱�Ƃ�m�F
            Assert.True(afterSecond.BeginAddress>=beforeSecond.BeginAddress);
        }
    }
    //2.0CollectionFactory.CompactWithLookup: CompactUntil>SafeReadOnlyAddress �ŃN���b�v����镪��
    [Fact, Trait("Category","Unit")]
    public void CompactWithLookup_ClipToSafeReadOnly() {
        var folder = CreateFolder();
        using var LogDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: TestMemorySize);
        using var ObjDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Collections.Tsavorite.Const.MinObjectLogCapacity);
        //MutableFraction=0�őS�f�[�^�𑦎��ǂݎ���p�ɂ���
        using var store = Store.Create(MemorySize: TestMemorySize,MutableFraction: 0).Create<int,string>(LogDevice,ObjDevice);
        using var dict = store.Create();
        //���ʂ̃f�[�^��ǉ�����SafeReadOnlyAddress��SegmentSize�ȉ��ɂȂ�󋵂���
        dict.Add(1,"a");
        dict.Add(2,"b");
        var address = store.Address;
        //SafeReadOnlyAddress����������Ԃ�CompactWithLookup �� CompactUntil>SafeReadOnlyAddress�̃N���b�v����
        store.CompactWithLookup();
        //��O�Ȃ��������邱��
        Assert.True(store.Address.BeginAddress>=address.BeginAddress);
    }
    [Fact, Trait("Category","Unit")]
    public void Dispose_DoubleCall_Idempotent() {
        var store = Store.Create().Create<int>();
        store.Dispose();
        store.Dispose();
    }
    [Fact, Trait("Category","Unit")]
    public void Finalize_GcAsyncDispose() {
        static void CreateOrphan() {
            _=Store.Create().Create<int,string>();
        }
        for(var i = 0; i<16; i++) {
            CreateOrphan();
        }
        GC.Collect(GC.MaxGeneration,GCCollectionMode.Forced,blocking: true);
        GC.WaitForPendingFinalizers();
        using var store = Store.Create().Create<int,string>();
    }
}
