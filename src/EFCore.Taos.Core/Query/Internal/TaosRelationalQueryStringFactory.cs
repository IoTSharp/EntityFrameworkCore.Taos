using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Query.Internal;

namespace IoTSharp.EntityFrameworkCore.Taos.Query.Internal
{
    public class TaosRelationalQueryStringFactory : RelationalQueryStringFactory
    {
        public override string Create(DbCommand command)
        {
            return base.Create(command);
        }
    }
}
