using System.Collections.Generic;
using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore.Query;

namespace IoTSharp.EntityFrameworkCore.Taos.Query.Internal
{
    internal class TaosParameterBasedSqlProcessor : RelationalParameterBasedSqlProcessor
    {
        public TaosParameterBasedSqlProcessor(RelationalParameterBasedSqlProcessorDependencies dependencies, bool useRelationalNulls) : base(dependencies, useRelationalNulls)
        {
        }
        protected override Expression ProcessSqlNullability(Expression queryExpression, IReadOnlyDictionary<string, object> parametersValues, out bool canCache)
        {
            return new TaosSqlNullabilityProcessor(Dependencies, UseRelationalNulls).Process(queryExpression, parametersValues, out canCache);
        }
    }
}