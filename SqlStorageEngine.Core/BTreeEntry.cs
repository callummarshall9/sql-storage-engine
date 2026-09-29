namespace sql_storage_engine;

public readonly record struct BTreeEntry<TKey, TValue>(TKey Key, TValue Value);
