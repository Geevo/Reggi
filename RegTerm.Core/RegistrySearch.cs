namespace RegTerm.Core;

/// <summary>
/// Resumable depth-first registry search.
/// </summary>
public sealed class RegistrySearch
{
    private enum Stage { Key, Values, Children }

    private sealed class Frame(RegistryPath path, bool isRoot = false)
    {
        public readonly RegistryPath Path = path;
        public readonly bool IsRoot = isRoot;

        public Stage Stage = Stage.Key;
        public IReadOnlyList<RegistryValueItem>? Values;
        public int ValueIndex;
        public IReadOnlyList<string>? SubKeys;
        public int ChildIndex;

        public RegistryPath ChildPath(int index) =>
            IsRoot ? RegistryPath.Root(SubKeys![index]) : Path.Child(SubKeys![index]);
    }

    private readonly IRegistryService _registry;
    private readonly SearchQuery _query;
    private readonly Stack<Frame> _stack = new();

    private volatile string _scanning = string.Empty;
    private long _keysScanned;

    public RegistrySearch(IRegistryService registry, SearchQuery query, RegistryPath start)
    {
        _registry = registry;
        _query = query;
        Seed(start);
    }

    /// <summary>Path currently being examined, for progress display.</summary>
    public string Scanning => _scanning;

    public long KeysScanned => Interlocked.Read(ref _keysScanned);

    /// <summary>True once the walk has run off the end of the registry.</summary>
    public bool Exhausted => _stack.Count == 0;

    /// <summary>
    /// Advances until the next match, or null if the registry is exhausted.
    /// Throws <see cref="OperationCanceledException"/> if cancelled; the cursor stays valid.
    /// </summary>
    public SearchHit? FindNext(CancellationToken cancellation)
    {
        while (_stack.Count > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            var frame = _stack.Peek();

            switch (frame.Stage)
            {
                case Stage.Key:
                    frame.Stage = Stage.Values;
                    _scanning = frame.Path.ToString();
                    Interlocked.Increment(ref _keysScanned);

                    // Hive names are not included in key-name matches.
                    if (_query.MatchKeys && !frame.Path.IsHiveRoot && _query.Matches(frame.Path.LeafName))
                        return new SearchHit(frame.Path, null);
                    break;

                case Stage.Values:
                    if (!_query.MatchValueNames && !_query.MatchData)
                    {
                        frame.Stage = Stage.Children;
                        break;
                    }

                    // Value reads are cached per key.
                    frame.Values ??= _registry.GetValues(frame.Path).Values;

                    if (frame.ValueIndex >= frame.Values.Count)
                    {
                        frame.Stage = Stage.Children;
                        break;
                    }

                    var value = frame.Values[frame.ValueIndex++];
                    if (value.IsUnset) break;

                    if (_query.MatchValueNames && _query.Matches(value.DisplayName))
                        return new SearchHit(frame.Path, value.Name);

                    if (_query.MatchData &&
                        RegistryValueCodec
                            .SearchableForms(value.Kind, value.RawValue, _query.LooksHexadecimal)
                            .Any(_query.Matches))
                        return new SearchHit(frame.Path, value.Name);
                    break;

                case Stage.Children:
                    frame.SubKeys ??= _registry.GetSubKeyNames(frame.Path);

                    if (frame.ChildIndex >= frame.SubKeys.Count)
                    {
                        _stack.Pop();
                        break;
                    }

                    _stack.Push(new Frame(frame.ChildPath(frame.ChildIndex++)));
                    break;
            }
        }

        _scanning = string.Empty;
        return null;
    }

    /// <summary>
    /// Positions the cursor at <paramref name="start"/> while retaining later siblings and hives.
    /// </summary>
    private void Seed(RegistryPath start)
    {
        var hives = _registry.HiveNames;

        var hiveIndex = 0;
        for (var i = 0; i < hives.Count; i++)
        {
            if (string.Equals(hives[i], start.Hive, StringComparison.OrdinalIgnoreCase))
            {
                hiveIndex = i;
                break;
            }
        }

        _stack.Push(new Frame(default, isRoot: true)
        {
            Stage = Stage.Children,
            SubKeys = hives,
            ChildIndex = hiveIndex + 1      // later hives, once this one is done
        });

        var chain = AncestorChain(start);
        for (var i = 0; i < chain.Count - 1; i++)
        {
            var subKeys = _registry.GetSubKeyNames(chain[i]);
            var nextName = chain[i + 1].LeafName;

            var resumeAt = subKeys.Count;
            for (var j = 0; j < subKeys.Count; j++)
            {
                if (string.Equals(subKeys[j], nextName, StringComparison.OrdinalIgnoreCase))
                {
                    resumeAt = j + 1;
                    break;
                }
            }

            _stack.Push(new Frame(chain[i])
            {
                Stage = Stage.Children,     // already passed; do not rescan name or values
                SubKeys = subKeys,
                ChildIndex = resumeAt
            });
        }

        _stack.Push(new Frame(start));       // scanned in full, starting at its own name
    }

    /// <summary>Hive root first, down to and including <paramref name="path"/>.</summary>
    private static List<RegistryPath> AncestorChain(RegistryPath path)
    {
        var chain = new List<RegistryPath>();
        RegistryPath? current = path;
        while (current is not null)
        {
            chain.Add(current.Value);
            current = current.Value.Parent();
        }
        chain.Reverse();
        return chain;
    }
}
