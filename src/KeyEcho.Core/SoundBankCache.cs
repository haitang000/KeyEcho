namespace KeyEcho.Core;

/// <summary>Bounded LRU for fully decoded packs. Playback retains a direct bank reference.</summary>
public sealed class SoundBankCache(int capacity = 2)
{
    private readonly object _gate = new();
    private readonly int _capacity = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    private readonly Dictionary<string, (SoundBank Bank, LinkedListNode<string> Node)> _banks = [];
    private readonly LinkedList<string> _recent = new();
    public int Count { get { lock (_gate) return _banks.Count; } }
    public long CachedBytes { get { lock (_gate) return _banks.Values.Sum(entry => entry.Bank.CachedBytes); } }

    public SoundBank GetOrLoad(string id, Func<SoundBank> load)
    {
        lock (_gate)
        {
            if (_banks.TryGetValue(id, out var cached))
            {
                _recent.Remove(cached.Node); _recent.AddFirst(cached.Node);
                return cached.Bank;
            }
            // Decode before evicting, so a missing/corrupt pack leaves working banks intact.
            var bank = load();
            if (_banks.Count == _capacity)
            {
                string oldest = _recent.Last!.Value;
                _recent.RemoveLast(); _banks.Remove(oldest);
            }
            _banks[id] = (bank, _recent.AddFirst(id));
            return bank;
        }
    }
}
