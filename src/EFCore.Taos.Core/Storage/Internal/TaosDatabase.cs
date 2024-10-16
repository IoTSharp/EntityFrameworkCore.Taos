// Copyright (c)  Maikebing. All rights reserved.
// Licensed under the MIT License, See License.txt in the project root for license information.

using System;
using Microsoft.EntityFrameworkCore.Storage;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Update;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.EntityFrameworkCore.Query;
using System.Linq.Expressions;

namespace IoTSharp.EntityFrameworkCore.Taos.Storage.Internal
{
    public class TaosDatabase : RelationalDatabase
    {
        public TaosDatabase(DatabaseDependencies dependencies, RelationalDatabaseDependencies relationalDependencies) : base(dependencies, relationalDependencies)
        {
        }
        public override int SaveChanges(IList<IUpdateEntry> entries)
        {
            return base.SaveChanges(entries);
        }
        public override Task<int> SaveChangesAsync(IList<IUpdateEntry> entries, CancellationToken cancellationToken = default)
        {
            return base.SaveChangesAsync(entries, cancellationToken);
        }

        public override Func<QueryContext, TResult> CompileQuery<TResult>(Expression query, bool async)
        {
            return base.CompileQuery<TResult>(query, async);
        }
    }
}

