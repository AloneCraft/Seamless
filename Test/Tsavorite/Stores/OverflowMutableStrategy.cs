using Tsavorite.Stores;

namespace Test.Tsavorite.Stores;

public class OverflowMutableStrategy {
    [Fact, Trait("Category","Unit")]
    public void MemoryOverflowMutableStrategy_WouldOverflowMutable() {
        {
            var logMemorySize = 1024L;
            var mutableFraction = 0.5;
            var beginAddress = 4096L;
            var keyLength = 16;
            var valueLength = 32;
            var mutableBytes = (long)(logMemorySize*mutableFraction);
            var requiredBytes = MemoryOverflowMutableStrategy.GetRequiredBytes(keyLength,valueLength);
            var lastFittingTailAddress = beginAddress+mutableBytes-requiredBytes;

            Assert.False(MemoryOverflowMutableStrategy.WouldOverflowMutable(logMemorySize,mutableFraction,beginAddress,lastFittingTailAddress,keyLength,valueLength));
            Assert.True(MemoryOverflowMutableStrategy.WouldOverflowMutable(logMemorySize,mutableFraction,beginAddress,lastFittingTailAddress+1,keyLength,valueLength));
        }
        {
            var logMemorySize = 1024L;
            var mutableFraction = 0.333;
            var beginAddress = 8192L;
            var keyLength = 7;
            var valueLength = 11;
            var mutableBytes = (long)(logMemorySize*mutableFraction);
            var requiredBytes = MemoryOverflowMutableStrategy.GetRequiredBytes(keyLength,valueLength);
            var lastFittingTailAddress = beginAddress+mutableBytes-requiredBytes;

            Assert.False(MemoryOverflowMutableStrategy.WouldOverflowMutable(logMemorySize,mutableFraction,beginAddress,lastFittingTailAddress,keyLength,valueLength));
            Assert.True(MemoryOverflowMutableStrategy.WouldOverflowMutable(logMemorySize,mutableFraction,beginAddress,lastFittingTailAddress+1,keyLength,valueLength));
        }
        {
            var logMemorySize = 1024L;
            var mutableFraction = 0.0;
            var beginAddress = 2048L;
            var keyLength = 64;
            var valueLength = 128;
            var requiredBytes = MemoryOverflowMutableStrategy.GetRequiredBytes(keyLength,valueLength);
            var lastFittingTailAddress = beginAddress+logMemorySize-requiredBytes;

            Assert.False(MemoryOverflowMutableStrategy.WouldOverflowMutable(logMemorySize,mutableFraction,beginAddress,lastFittingTailAddress,keyLength,valueLength));
            Assert.True(MemoryOverflowMutableStrategy.WouldOverflowMutable(logMemorySize,mutableFraction,beginAddress,lastFittingTailAddress+1,keyLength,valueLength));
        }
    }

    [Fact, Trait("Category","Unit")]
    public void StorageOverflowMutableStrategy_WouldOverflowMutable() {
        {
            var storageCapacity = 1000L;
            var strategy = new StorageOverflowMutableStrategy(storageCapacity,limitFraction: 0.8,reserveBytes: 0);
            var keyLength = 10;
            var valueLength = 20;
            var requiredBytes = MemoryOverflowMutableStrategy.GetRequiredBytes(keyLength,valueLength);
            var lastFittingTailAddress = strategy.LimitBytes-requiredBytes;

            Assert.Equal(800L,strategy.LimitBytes);
            Assert.False(strategy.WouldOverflowMutable(tailAddress: lastFittingTailAddress,keyLength,valueLength));
            Assert.True(strategy.WouldOverflowMutable(tailAddress: lastFittingTailAddress+1,keyLength,valueLength));
        }
        {
            var storageCapacity = 1000L;
            var strategy = new StorageOverflowMutableStrategy(storageCapacity,limitFraction: 1.0,reserveBytes: 200);
            var keyLength = 10;
            var valueLength = 20;
            var requiredBytes = MemoryOverflowMutableStrategy.GetRequiredBytes(keyLength,valueLength);
            var lastFittingTailAddress = strategy.LimitBytes-requiredBytes;

            Assert.Equal(800L,strategy.LimitBytes);
            Assert.False(strategy.WouldOverflowMutable(tailAddress: lastFittingTailAddress,keyLength,valueLength));
            Assert.True(strategy.WouldOverflowMutable(tailAddress: lastFittingTailAddress+1,keyLength,valueLength));
        }
        {
            var storageCapacity = 1000L;
            var strategy = new StorageOverflowMutableStrategy(storageCapacity,limitFraction: 0.9,reserveBytes: 250);
            var keyLength = 10;
            var valueLength = 20;
            var requiredBytes = MemoryOverflowMutableStrategy.GetRequiredBytes(keyLength,valueLength);
            var lastFittingTailAddress = strategy.LimitBytes-requiredBytes;

            Assert.Equal(750L,strategy.LimitBytes);
            Assert.False(strategy.WouldOverflowMutable(tailAddress: lastFittingTailAddress,keyLength,valueLength));
            Assert.True(strategy.WouldOverflowMutable(tailAddress: lastFittingTailAddress+1,keyLength,valueLength));
        }
        {
            var storageCapacity = 1000L;
            var strategy = new StorageOverflowMutableStrategy(storageCapacity,limitFraction: 0.8,reserveBytes: 0);
            var keyLength = 10;
            var valueLength = 20;
            var requiredBytes = MemoryOverflowMutableStrategy.GetRequiredBytes(keyLength,valueLength);
            var firstOverflowTailAddress = strategy.LimitBytes-requiredBytes+1;

            Assert.True(strategy.WouldOverflowMutable(tailAddress: firstOverflowTailAddress,keyLength,valueLength));
        }
        {
            var strategy = new StorageOverflowMutableStrategy(storageCapacity: -1,limitFraction: 0.0,reserveBytes: 0);

            Assert.Equal(long.MaxValue,strategy.LimitBytes);
            Assert.False(strategy.WouldOverflowMutable(tailAddress: long.MaxValue,keyLength: 10,valueLength: 20));
        }
    }
}
