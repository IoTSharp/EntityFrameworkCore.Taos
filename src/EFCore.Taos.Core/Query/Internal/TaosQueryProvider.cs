using Microsoft.EntityFrameworkCore.Query.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IoTSharp.EntityFrameworkCore.Taos.Query.Internal
{
    public class TaosQueryProvider : EntityQueryProvider
    {
        public TaosQueryProvider(IQueryCompiler queryCompiler) : base(queryCompiler)
        {
        }
        public override TResult Execute<TResult>(Expression expression)
        {
            return base.Execute<TResult>(expression);
        }
        public override IQueryable CreateQuery(Expression expression)
        {
            return base.CreateQuery(expression);
        }
        public override IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return base.CreateQuery<TElement>(expression);
        }
        public override TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            return base.ExecuteAsync<TResult>(expression, cancellationToken);
        }
    }
}
