// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Exposures.Diagnostics;
public interface IFuzzRegressionSinkExposure
{
    void Save(FuzzFailure fuzzFailure);
}