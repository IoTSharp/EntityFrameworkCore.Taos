// Copyright (c)  Maikebing. All rights reserved.
// Licensed under the MIT License, See License.txt in the project root for license information.

using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.EntityFrameworkCore.Utilities;

namespace IoTSharp.EntityFrameworkCore.Taos.Update.Internal
{
    public class TaosModificationCommandBatch : SingularModificationCommandBatch
    {
        public TaosModificationCommandBatch(ModificationCommandBatchFactoryDependencies dependencies) : base(dependencies)
        {
        }
        public override void Execute(IRelationalConnection connection)
        {
            if (StoreCommand is null)
            {
                throw new InvalidOperationException(RelationalStrings.ModificationCommandBatchNotComplete);
            }

            try
            {
                var parameterObject = new RelationalCommandParameterObject(
                        connection,
                        StoreCommand.ParameterValues,
                        null,
                        Dependencies.CurrentContext.Context,
                        Dependencies.Logger, CommandSource.SaveChanges);
                using var dataReader = StoreCommand.RelationalCommand.ExecuteReader(
                    parameterObject);

                Consume(dataReader);
            }
            catch (Exception ex) when (ex is not DbUpdateException and not OperationCanceledException)
            {
                throw new DbUpdateException(
                    RelationalStrings.UpdateStoreException,
                    ex,
                    ModificationCommands.SelectMany(c => c.Entries).ToList());
            }
            //base.Execute(connection);
        }
        public async override Task ExecuteAsync(IRelationalConnection connection, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            if (StoreCommand is null)
            {
                throw new InvalidOperationException(RelationalStrings.ModificationCommandBatchNotComplete);
            }

            try
            {
                var dataReader = await StoreCommand.RelationalCommand.ExecuteReaderAsync(
                    new RelationalCommandParameterObject(
                        connection,
                        StoreCommand.ParameterValues,
                        null,
                        Dependencies.CurrentContext.Context,
                        Dependencies.Logger, CommandSource.SaveChanges),
                    cancellationToken).ConfigureAwait(false);

                await using var _ = dataReader.ConfigureAwait(false);

                await ConsumeAsync(dataReader, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not DbUpdateException and not OperationCanceledException)
            {
                throw new DbUpdateException(
                    RelationalStrings.UpdateStoreException,
                    ex,
                    ModificationCommands.SelectMany(c => c.Entries).ToList());
            }
            //return base.ExecuteAsync(connection, cancellationToken);
        }

        public override void Complete(bool moreBatchesExpected)
        {
            if (StoreCommand is not null)
            {
                throw new InvalidOperationException(RelationalStrings.ModificationCommandBatchAlreadyComplete);
            }

            //_areMoreBatchesExpected = moreBatchesExpected;

            // Some database have a mode where autocommit is off, and so executing a command outside of an explicit transaction implicitly
            // creates a new transaction (which needs to be explicitly committed).
            // The below is a hook for allowing providers to turn autocommit on, in case it's off.
            if (!RequiresTransaction)
            {
                UpdateSqlGenerator.PrependEnsureAutocommit(SqlBuilder);
            }

            RelationalCommandBuilder.Append(SqlBuilder.ToString());
            var relationCommand = RelationalCommandBuilder.Build();
            StoreCommand = new RawSqlCommand(relationCommand, ParameterValues);
        }
        protected override void AddParameter(IColumnModification columnModification)
        {
            var direction = columnModification.Column switch
            {
                IStoreStoredProcedureParameter storedProcedureParameter => storedProcedureParameter.Direction,
                IStoreStoredProcedureReturnValue => ParameterDirection.Output,
                _ => ParameterDirection.Input
            };
            var attr = columnModification.Property.PropertyInfo.GetCustomAttribute<TaosColumnAttribute>();
            var isTag = attr?.IsTag ?? false;
            // For the case where the same modification has both current and original value parameters, and corresponds to an in/out parameter,
            // we only want to add a single parameter. This will happen below.
            if (columnModification.UseCurrentValueParameter
                && !(columnModification.UseOriginalValueParameter && direction == ParameterDirection.InputOutput))
            {

                AddParameterCore(
                    columnModification.ParameterName, columnModification.UseCurrentValue
                        ? columnModification.Value
                        : direction == ParameterDirection.InputOutput
                            ? DBNull.Value
                            : null, isTag);
            }

            if (columnModification.UseOriginalValueParameter)
            {
                Check.DebugAssert(direction.HasFlag(ParameterDirection.Input), "direction.HasFlag(ParameterDirection.Input)");

                AddParameterCore(columnModification.OriginalParameterName, columnModification.OriginalValue, isTag);
            }

            void AddParameterCore(string name, object? value, bool isTag)
            {
                RelationalCommandBuilder.AddParameter(
                    name,
                    isTag ? $"${name}" : $"@{name}"/* Dependencies.SqlGenerationHelper.GenerateParameterName(name)*/,
                    columnModification.TypeMapping!,
                    columnModification.IsNullable,
                    direction);

                ParameterValues.Add(name, value);


                //_pendingParameters++;
            }
            //base.AddParameter(columnModification);
        }
    }
}
