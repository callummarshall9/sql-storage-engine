// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models;
public readonly record struct BTreeEntry<TKey, TValue>
{
    private readonly TKey? _key;
    public TKey Key { get => _key!; init => _key = value; }

    private readonly TValue? _value;
    public TValue Value { get => _value!; init => _value = value; }
}