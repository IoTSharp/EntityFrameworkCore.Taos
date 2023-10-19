// Copyright (c)  Maikebing. All rights reserved.
// Licensed under the MIT License, See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using IoTSharp.Data.Taos;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

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
    public class TaosDatabaseCreator : RelationalDatabaseCreator
    {
        // ReSharper disable once InconsistentNaming
        private const int Taos_CANTOPEN = 14;

        private readonly ITaosRelationalConnection _connection;
        private readonly IRawSqlCommandBuilder _rawSqlCommandBuilder;

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public TaosDatabaseCreator(
            [NotNull] RelationalDatabaseCreatorDependencies dependencies,
            [NotNull] ITaosRelationalConnection connection,
            [NotNull] IRawSqlCommandBuilder rawSqlCommandBuilder)
            : base(dependencies)
        {
            _connection = connection;
            _rawSqlCommandBuilder = rawSqlCommandBuilder;
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public override void Create()
        {
            Dependencies.Connection.Open();

            _rawSqlCommandBuilder
                .Build($"create database {Dependencies.Connection.DbConnection.Database};")
                .ExecuteNonQuery(new RelationalCommandParameterObject(
                        Dependencies.Connection,
                        null,
                        null,
                        null,
                        Dependencies.CommandLogger));

            Dependencies.Connection.Close();
        }
        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public override bool Exists()
        {
            bool _exists = false;
            try
            {
                using (var tc = new TaosConnection(Dependencies.Connection.ConnectionString))
                {
                    tc.Open();
                    _exists = tc.DatabaseExists(Dependencies.Connection.DbConnection.Database);
                }
            }
            catch (Exception)
            {
            }
            return _exists;
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public override bool HasTables()
        {
            var count = 0;
            try
            {
                using (var tc = new TaosConnection(Dependencies.Connection.ConnectionString))
                {
                    tc.Open();
                    tc.ChangeDatabase(Dependencies.Connection.DbConnection.Database);
                    count = tc.CreateCommand("SHOW TABLES").ExecuteReader().ToJson().Count();
                }
            }
            catch (Exception)
            {
            }
            return count != 0;
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public override void Delete()
        {
            Dependencies.Connection.Open();
            try
            {
                _rawSqlCommandBuilder
                          .Build($"DROP DATABASE    {Dependencies.Connection.DbConnection.Database};")
                          .ExecuteNonQuery(new RelationalCommandParameterObject(
                        Dependencies.Connection,
                        null,
                        null,
                        null,
                        Dependencies.CommandLogger));

            }
            catch
            {
                // any exceptions here can be ignored
            }
            finally
            {
                Dependencies.Connection.Close();
            }
        }
    }
}

