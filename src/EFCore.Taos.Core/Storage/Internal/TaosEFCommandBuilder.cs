using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Storage;

namespace IoTSharp.EntityFrameworkCore.Taos.Storage.Internal
{
    public class TaosEFCommandBuilder : RelationalCommandBuilder
    {
        public TaosEFCommandBuilder(RelationalCommandBuilderDependencies dependencies) : base(dependencies)
        {
        }
        public override IRelationalCommandBuilder AddParameter(IRelationalParameter parameter)
        {
            return base.AddParameter(parameter);
        }
        public override IRelationalCommand Build()
        {
            var sql = this.ToString();
            var cmd = new TaosEFCommand(Dependencies, sql, this.Parameters);
            
            //var bcmd = base.Build();
            return cmd;
        }
    }
}
