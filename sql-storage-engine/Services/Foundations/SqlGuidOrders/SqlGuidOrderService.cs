// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlGuidOrders;
internal sealed partial class SqlGuidOrderService : ISqlGuidOrderService
{
    private readonly sql_storage_engine.Brokers.SqlGuidOrders.SqlGuidOrderBroker broker;
    public SqlGuidOrderService(sql_storage_engine.Brokers.SqlGuidOrders.SqlGuidOrderBroker broker)
    {
        this.broker = broker;
    }

    internal static SqlGuidOrderService CreateSqlGuidOrderService() =>
        new(broker: sql_storage_engine.Brokers.SqlGuidOrders.SqlGuidOrderBroker.CreateSqlGuidOrderBroker());

    public SqlValueComparisonContext CompareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext) =>
        TryCatch(operation: () =>
    {
        ValidateValues(sqlValueComparisonContext: sqlValueComparisonContext);

        return sqlValueComparisonContext with
        {
            Order = broker.Compare(left: ((UniqueIdentifierSqlValue)sqlValueComparisonContext.Left!).Value, right: ((UniqueIdentifierSqlValue)sqlValueComparisonContext.Right!).Value)
        };
    });
}