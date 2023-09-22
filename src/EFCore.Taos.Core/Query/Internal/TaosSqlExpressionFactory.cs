using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Query;

namespace IoTSharp.EntityFrameworkCore.Taos.Query.Internal
{
    public class TaosSqlExpressionFactory : SqlExpressionFactory
    {
        public TaosSqlExpressionFactory(SqlExpressionFactoryDependencies dependencies) : base(dependencies)
        {
        }
    }
}
