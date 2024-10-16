using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace IoTSharp.EntityFrameworkCore.Taos.Storage.Internal
{
    public class TaosEFCommand : RelationalCommand
    {
        private Stopwatch _stopwatch = new Stopwatch();
        private RelationalDataReader? _relationalReader;

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

            var connection = parameterObject.Connection;
            var context = parameterObject.Context;
            var readerColumns = parameterObject.ReaderColumns;
            var logger = parameterObject.Logger;
            var detailedErrorsEnabled = parameterObject.DetailedErrorsEnabled;

            var startTime = DateTimeOffset.UtcNow;

            var shouldLogCommandCreate = logger?.ShouldLogCommandCreate(startTime) == true;
            var shouldLogCommandExecute = logger?.ShouldLogCommandExecute(startTime) == true;

            // Guid.NewGuid is expensive, do it only if needed
            var commandId = shouldLogCommandCreate || shouldLogCommandExecute ? Guid.NewGuid() : default;

            var command = CreateDbCommand(parameterObject, commandId, DbCommandMethod.ExecuteReader);

            connection.Open();

            var readerOpen = false;
            DbDataReader reader;

            try
            {
                if (shouldLogCommandExecute)
                {
                    _stopwatch.Restart();

                    var interceptionResult = logger!.CommandReaderExecuting(
                        connection,
                        command,
                        context,
                        commandId,
                        connection.ConnectionId,
                        startTime,
                        parameterObject.CommandSource);

                    reader = interceptionResult.HasResult
                        ? interceptionResult.Result
                        : command.ExecuteReader();

                    reader = logger.CommandReaderExecuted(
                        connection,
                        command,
                        context,
                        commandId,
                        connection.ConnectionId,
                        reader,
                        startTime,
                        _stopwatch.Elapsed,
                        parameterObject.CommandSource);
                }
                else
                {
                    reader = command.ExecuteReader();
                }
            }
            catch (Exception exception)
            {
                if (Dependencies.ExceptionDetector.IsCancellation(exception))
                {
                    logger?.CommandCanceled(
                        connection,
                        command,
                        context,
                        DbCommandMethod.ExecuteReader,
                        commandId,
                        connection.ConnectionId,
                        startTime,
                        _stopwatch.Elapsed,
                        parameterObject.CommandSource);
                }
                else
                {
                    logger?.CommandError(
                        connection,
                        command,
                        context,
                        DbCommandMethod.ExecuteReader,
                        commandId,
                        connection.ConnectionId,
                        exception,
                        startTime,
                        _stopwatch.Elapsed,
                        parameterObject.CommandSource);
                }

                CleanupCommand(command, connection);

                throw;
            }

            try
            {
                if (readerColumns != null)
                {
                    reader = new BufferedDataReader(reader, detailedErrorsEnabled).Initialize(readerColumns);
                }

                _relationalReader ??= CreateRelationalDataReader();

                _relationalReader.Initialize(parameterObject.Connection, command, reader, commandId, logger);

                readerOpen = true;

                return _relationalReader;
            }
            finally
            {
                if (!readerOpen)
                {
                    CleanupCommand(command, connection);
                }
            }
            //var reader = base.ExecuteReader(parameterObject);
            //return reader;
        }
        public override async Task<RelationalDataReader> ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken = default)
        {
            var connection = parameterObject.Connection;
            var context = parameterObject.Context;
            var readerColumns = parameterObject.ReaderColumns;
            var logger = parameterObject.Logger;
            var detailedErrorsEnabled = parameterObject.DetailedErrorsEnabled;

            var startTime = DateTimeOffset.UtcNow;

            var shouldLogCommandCreate = logger?.ShouldLogCommandCreate(startTime) == true;
            var shouldLogCommandExecute = logger?.ShouldLogCommandExecute(startTime) == true;

            // Guid.NewGuid is expensive, do it only if needed
            var commandId = shouldLogCommandCreate || shouldLogCommandExecute ? Guid.NewGuid() : default;

            var command = CreateDbCommand(parameterObject, commandId, DbCommandMethod.ExecuteReader);

            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var readerOpen = false;
            DbDataReader reader;

            try
            {
                if (shouldLogCommandExecute)
                {
                    _stopwatch.Restart();

                    var interceptionResult = await logger!.CommandReaderExecutingAsync(
                            connection,
                            command,
                            context,
                            commandId,
                            connection.ConnectionId,
                            startTime,
                            parameterObject.CommandSource,
                            cancellationToken)
                        .ConfigureAwait(false);

                    reader = interceptionResult.HasResult
                        ? interceptionResult.Result
                        : await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

                    reader = await logger.CommandReaderExecutedAsync(
                            connection,
                            command,
                            context,
                            commandId,
                            connection.ConnectionId,
                            reader,
                            startTime,
                            _stopwatch.Elapsed,
                            parameterObject.CommandSource,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                if (logger != null)
                {
                    if (Dependencies.ExceptionDetector.IsCancellation(exception, cancellationToken))
                    {
                        await logger.CommandCanceledAsync(
                                connection,
                                command,
                                context,
                                DbCommandMethod.ExecuteReader,
                                commandId,
                                connection.ConnectionId,
                                startTime,
                                _stopwatch.Elapsed,
                                parameterObject.CommandSource,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await logger.CommandErrorAsync(
                                connection,
                                command,
                                context,
                                DbCommandMethod.ExecuteReader,
                                commandId,
                                connection.ConnectionId,
                                exception,
                                startTime,
                                _stopwatch.Elapsed,
                                parameterObject.CommandSource,
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                await CleanupCommandAsync(command, connection).ConfigureAwait(false);

                throw;
            }

            try
            {
                if (readerColumns != null)
                {
                    reader = await new BufferedDataReader(reader, detailedErrorsEnabled).InitializeAsync(readerColumns, cancellationToken)
                        .ConfigureAwait(false);
                }

                _relationalReader ??= CreateRelationalDataReader();

                _relationalReader.Initialize(parameterObject.Connection, command, reader, commandId, logger);

                readerOpen = true;

                return _relationalReader;
            }
            finally
            {
                if (!readerOpen)
                {
                    await CleanupCommandAsync(command, connection).ConfigureAwait(false);
                }
            }
            //return base.ExecuteReaderAsync(parameterObject, cancellationToken);
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
            var cmd = base.CreateDbCommand(parameterObject, commandId, commandMethod);
            return cmd;
        }
        public override void PopulateFrom(IRelationalCommandTemplate commandTemplate)
        {
            base.PopulateFrom(commandTemplate);
        }

        private static void CleanupCommand(DbCommand command, IRelationalConnection connection)
        {
            command.Parameters.Clear();
            command.Dispose();
            connection.Close();
        }

        private static async Task CleanupCommandAsync(DbCommand command, IRelationalConnection connection)
        {
            command.Parameters.Clear();
            await command.DisposeAsync().ConfigureAwait(false);
            await connection.CloseAsync().ConfigureAwait(false);
        }

    }
}
