// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Identifiers;
/// <summary>Describes a scalar or compound identifier for diagnostic rendering.</summary>
internal sealed class IdentifierDisplay
{
    public IdentifierDisplayKind Kind { get; init; }
    public ulong Value { get; init; }
    public Guid DatabaseValue { get; init; }
    public ushort SlotValue { get; init; }
    public uint GenerationValue { get; init; }
}