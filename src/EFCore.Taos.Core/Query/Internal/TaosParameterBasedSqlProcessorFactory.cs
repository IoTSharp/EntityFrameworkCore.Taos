﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Query;

namespace IoTSharp.EntityFrameworkCore.Taos.Query.Internal
{
    public class TaosParameterBasedSqlProcessorFactory : IRelationalParameterBasedSqlProcessorFactory
    {
        private RelationalParameterBasedSqlProcessorDependencies _dependencies;

        public TaosParameterBasedSqlProcessorFactory(RelationalParameterBasedSqlProcessorDependencies dependencies)
                => _dependencies = dependencies;

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual RelationalParameterBasedSqlProcessor Create(bool useRelationalNulls)
            => new TaosParameterBasedSqlProcessor(_dependencies, useRelationalNulls);
    }
}
