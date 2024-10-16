using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace IoTSharp.EntityFrameworkCore.Taos.Query.Internal
{
    public class TaosSqlExpressionFactory : SqlExpressionFactory
    {
        public TaosSqlExpressionFactory(SqlExpressionFactoryDependencies dependencies) : base(dependencies)
        {
        }

        public TaosMatchExpression Match(SqlExpression match, SqlExpression pattern, RelationalTypeMapping stringTypeMapping)
        {
            return new TaosMatchExpression(match, pattern, stringTypeMapping);
        }
    }
}
