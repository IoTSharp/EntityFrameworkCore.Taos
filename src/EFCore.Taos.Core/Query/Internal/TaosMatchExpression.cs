using System;
using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace IoTSharp.EntityFrameworkCore.Taos.Query.Internal
{
    public class TaosMatchExpression : SqlExpression
    {
        public TaosMatchExpression(
        SqlExpression match,
        SqlExpression pattern,
        RelationalTypeMapping? typeMapping)
        : base(typeof(bool), typeMapping)
        {
            Match = match;
            Pattern = pattern;
        }

        public SqlExpression Match { get; }
        public SqlExpression Pattern { get; }
        public override RelationalTypeMapping TypeMapping => base.TypeMapping!;
        

        protected override void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Visit(Match);
            expressionPrinter.Append(" Match ");
            expressionPrinter.Visit(Pattern);
        }
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var match = (SqlExpression)visitor.Visit(Match);
            var pattern = (SqlExpression)visitor.Visit(Pattern);


            var exp = Update(match, pattern);
            return exp;

        }

        public virtual TaosMatchExpression Update(
            SqlExpression match,
            SqlExpression pattern)
            => match != Match || pattern != Pattern
                ? new TaosMatchExpression(match, pattern, TypeMapping)
                : this;

        public override bool Equals(object? obj)
        => obj != null
            && (ReferenceEquals(this, obj)
                || obj is TaosMatchExpression matchExpression
                && Equals(matchExpression));

        private bool Equals(TaosMatchExpression matchExpression)
            => base.Equals(matchExpression)
                && Match.Equals(matchExpression.Match)
                && Pattern.Equals(matchExpression.Pattern);

        /// <inheritdoc />
        public override int GetHashCode()
            => HashCode.Combine(base.GetHashCode(), Match, Pattern);
    }
}
