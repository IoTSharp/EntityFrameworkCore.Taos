using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace IoTSharp.EntityFrameworkCore.Taos.Storage.Internal
{
    public class TaosEFCommand : RelationalCommand
    {
        public TaosEFCommand(RelationalCommandBuilderDependencies dependencies, string commandText, IReadOnlyList<IRelationalParameter> parameters) : base(dependencies, commandText, parameters)
        {
        }

        public override int ExecuteNonQuery(RelationalCommandParameterObject parameterObject)
        {
            return base.ExecuteNonQuery(parameterObject);
        }
        public override Task<int> ExecuteNonQueryAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken = default)
        {
            return base.ExecuteNonQueryAsync(parameterObject, cancellationToken);
        }
        public override RelationalDataReader ExecuteReader(RelationalCommandParameterObject parameterObject)
        {
            return base.ExecuteReader(parameterObject);
        }
        public override Task<RelationalDataReader> ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken = default)
        {
            return base.ExecuteReaderAsync(parameterObject, cancellationToken);
        }
        public override object ExecuteScalar(RelationalCommandParameterObject parameterObject)
        {
            return base.ExecuteScalar(parameterObject);
        }
        public override Task<object> ExecuteScalarAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken = default)
        {
            return base.ExecuteScalarAsync(parameterObject, cancellationToken);
        }
        public override DbCommand CreateDbCommand(RelationalCommandParameterObject parameterObject, Guid commandId, DbCommandMethod commandMethod)
        {
            return base.CreateDbCommand(parameterObject, commandId, commandMethod);
        }
    }
}
