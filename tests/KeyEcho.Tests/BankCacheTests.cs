using KeyEcho.Core;

namespace KeyEcho.Tests;

public sealed class BankCacheTests
{
    [Fact] public void EvictsLeastRecentlyUsedPackAndReusesTheRecentOne()
    {
        var cache = new SoundBankCache(2);
        var a = cache.GetOrLoad("a", () => SoundBank.Uniform(new float[10]));
        var b = cache.GetOrLoad("b", () => SoundBank.Uniform(new float[20]));
        Assert.Same(a, cache.GetOrLoad("a", () => throw new Exception("Must not decode twice")));
        cache.GetOrLoad("c", () => SoundBank.Uniform(new float[30]));
        Assert.Equal(2, cache.Count); Assert.Equal(160, cache.CachedBytes);
        var replacement = cache.GetOrLoad("b", () => SoundBank.Uniform(new float[20]));
        Assert.NotSame(b, replacement); Assert.Equal(2, cache.Count);
    }
    [Fact] public void FailedLoadPreservesWorkingPacksAndSharedSamplesAreCountedOnce()
    {
        float[] samples = new float[10];
        var bank = new SoundBank(new Dictionary<int, float[]> { [30] = samples, [31] = samples }, [samples]);
        Assert.Equal(40, bank.CachedBytes);
        var cache = new SoundBankCache(1);
        cache.GetOrLoad("a", () => bank);
        Assert.Throws<IOException>(() => cache.GetOrLoad("broken", () => throw new IOException("Missing sound")));
        Assert.Same(bank, cache.GetOrLoad("a", () => throw new Exception("Working pack must survive")));
    }
}
