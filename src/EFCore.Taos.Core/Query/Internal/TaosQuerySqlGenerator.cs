// Copyright (c)  Maikebing. All rights reserved.
// Licensed under the MIT License, See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;

namespace IoTSharp.EntityFrameworkCore.Taos.Query.Internal
{
    public class TaosQuerySqlGenerator : QuerySqlGenerator
    {
        private readonly ISqlGenerationHelper _sqlGenerationHelper;

        public TaosQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies)
            : base(dependencies)
        {
            _sqlGenerationHelper = dependencies.SqlGenerationHelper;
        }
        protected override string AliasSeparator => "";
        protected override Expression VisitSelect(SelectExpression selectExpression)
        {
#pragma warning disable EF1001 // Internal EF Core API usage.
            selectExpression.RemoveTypeAs();
#pragma warning restore EF1001 // Internal EF Core API usage.

            var exp = base.VisitSelect(selectExpression);
            return exp;
        }

        protected override Expression VisitColumn(ColumnExpression columnExpression)
        {
            Sql.Append(_sqlGenerationHelper.DelimitIdentifier(columnExpression.Name));
            return columnExpression;
        }

        protected override Expression VisitTable(TableExpression tableExpression)
        {
            Sql.Append(_sqlGenerationHelper.DelimitIdentifier(tableExpression.Name, tableExpression.Schema));
            return tableExpression;
        }


        //[System.Obsolete]
        //protected override string GenerateOperator(SqlBinaryExpression binaryExpression)
        //    => binaryExpression.OperatorType == ExpressionType.Add
        //        && binaryExpression.Type == typeof(string)
        //            ? " || "
        //            : base.GetOperator(binaryExpression);

        protected override void GenerateLimitOffset(SelectExpression selectExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));

            if (selectExpression.Limit != null
                || selectExpression.Offset != null)
            {
                base.Sql.AppendLine()
                    .Append("LIMIT ");

                Visit(
                    selectExpression.Limit
                    ?? new SqlConstantExpression(Expression.Constant(-1), selectExpression.Offset.TypeMapping));

                if (selectExpression.Offset != null)
                {
                    Sql.Append(" OFFSET ");

                    Visit(selectExpression.Offset);
                }
            }
        }

        protected override void GenerateSetOperationOperand(SetOperationBase setOperation, SelectExpression operand)
        {
            // Taos doesn't support parentheses around set operation operands
            Visit(operand);
        }
        protected override Expression VisitExtension(Expression extensionExpression)
        {
            var exp = extensionExpression switch
            {
                TaosMatchExpression matchExpression => VisitTaosMatchExpression(matchExpression),
                _ => base.VisitExtension(extensionExpression)
            };


            return exp;
        }

        private Expression VisitTaosMatchExpression(TaosMatchExpression matchExpression)
        {
            Visit(matchExpression.Match);
            this.Sql.Append(" MATCH ");
            return Visit(matchExpression.Pattern);

        }

        protected override Expression VisitSqlConstant(SqlConstantExpression sqlConstantExpression)
        {
            return base.VisitSqlConstant(sqlConstantExpression);
        }
    }
}
