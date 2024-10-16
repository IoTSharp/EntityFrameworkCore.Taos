using System;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Utilities;

namespace IoTSharp.EntityFrameworkCore.Taos.Query.Internal
{
    internal class TaosSqlNullabilityProcessor : SqlNullabilityProcessor
    {
        public TaosSqlNullabilityProcessor(RelationalParameterBasedSqlProcessorDependencies dependencies, bool useRelationalNulls) : base(dependencies, useRelationalNulls)
        {

        }

        protected override SqlExpression VisitCustomSqlExpression(SqlExpression sqlExpression, bool allowOptimizedExpansion, out bool nullable)
        {
            return sqlExpression switch
            {
                TaosMatchExpression matchExpression => VisitTaosMatchExpression(matchExpression, allowOptimizedExpansion, out nullable),
                _ => base.VisitCustomSqlExpression(sqlExpression, allowOptimizedExpansion, out nullable)
            };
        }

        private SqlExpression VisitTaosMatchExpression(TaosMatchExpression matchExpression, bool allowOptimizedExpansion, out bool nullable)
        {
            Check.NotNull(matchExpression, nameof(matchExpression));

            var match = Visit(matchExpression.Match, allowOptimizedExpansion, out var matchNullable);
            var pattern = Visit(matchExpression.Pattern, allowOptimizedExpansion, out var patternNullable);

            nullable = matchNullable || patternNullable;

            var exp = matchExpression.Update(match, pattern);
            return exp;
        }
    }
}