// Copyright (c)  Maikebing. All rights reserved.
// Licensed under the MIT License, See License.txt in the project root for license information.

using System.Data.Common;
using System.Linq;
using JetBrains.Annotations;
using IoTSharp.Data.Taos;
using Microsoft.EntityFrameworkCore.Infrastructure;
using IoTSharp.EntityFrameworkCore.Taos.Infrastructure.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics;

namespace IoTSharp.EntityFrameworkCore.Taos.Storage.Internal
{
    /// <summary>
    ///     <para>
    ///         This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///         the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///         any release. You should only use it directly in your code with extreme caution and knowing that
    ///         doing so can result in application failures when updating to a new Entity Framework Core release.
    ///     </para>
    ///     <para>
    ///         The service lifetime is <see cref="ServiceLifetime.Scoped" />. This means that each
    ///         <see cref="DbContext" /> instance will use its own instance of this service.
    ///         The implementation may depend on other services registered with any lifetime.
    ///         The implementation does not need to be thread-safe.
    ///     </para>
    /// </summary>
    public class TaosRelationalConnection : RelationalConnection, ITaosRelationalConnection
    {
        private readonly IRawSqlCommandBuilder _rawSqlCommandBuilder;

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public TaosRelationalConnection(
            [NotNull] RelationalConnectionDependencies dependencies,
            [NotNull] IRawSqlCommandBuilder rawSqlCommandBuilder)
            : base(dependencies)
        {
            Check.NotNull(rawSqlCommandBuilder, nameof(rawSqlCommandBuilder));

            _rawSqlCommandBuilder = rawSqlCommandBuilder;

            var optionsExtension = dependencies.ContextOptions.Extensions.OfType<TaosOptionsExtension>().FirstOrDefault();
            if (optionsExtension != null)
            {
                var relationalOptions = RelationalOptionsExtension.Extract(dependencies.ContextOptions);
                _connection = new TaosConnection(relationalOptions.ConnectionString);
            }
        }
        TaosConnection _connection;
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        protected override DbConnection CreateDbConnection()
        {
            var connection = new TaosConnection(ConnectionString);
            _connection = connection;
            return connection;
        }
        

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public virtual ITaosRelationalConnection CreateReadOnlyConnection()
        {
            var connectionStringBuilder = new TaosConnectionStringBuilder(ConnectionString);
            var contextOptions = new DbContextOptionsBuilder().UseTaos(connectionStringBuilder.ToString()).Options;
           // Dependencies.CurrentContext. = contextOptions;
            return new TaosRelationalConnection(Dependencies, _rawSqlCommandBuilder);
        }
        public override bool Open(bool errorsExpected = false)
        {
            _connection._nowdatabase = string.Empty;
            _connection.Open();
            bool result = _connection.State == System.Data.ConnectionState.Open;
            return result;
        }
        public override bool Close()
        {
            bool result = false;
            try
            {
                _connection.Close();
                result = _connection.State== System.Data.ConnectionState.Closed;
            }
            catch (Exception)
            {
            
            }
            return result;
        }
        public override void ReturnCommand(IRelationalCommand command)
        {
            base.ReturnCommand(command);
        }
    }
}
