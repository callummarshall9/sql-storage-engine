// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Foundations.ScopedStreams;
internal interface IScopedStreamService<T>
{
    ScopedStreamContext WrapScopedStreamContext(ScopedStreamContext scopedStreamContext);
}