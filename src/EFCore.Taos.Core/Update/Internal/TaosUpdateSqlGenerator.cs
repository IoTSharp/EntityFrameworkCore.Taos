// Copyright (c)  Maikebing. All rights reserved.
// Licensed under the MIT License, See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using IoTSharp.Data.Taos;
using IoTSharp.EntityFrameworkCore.Taos.Internal;
using IoTSharp.EntityFrameworkCore.Taos.Storage.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.EntityFrameworkCore.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Data;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using System.Reflection;
using System.Linq.Expressions;
using System.Net.WebSockets;

namespace IoTSharp.EntityFrameworkCore.Taos.Update.Internal
{
    /// <summary>
    ///     <para>
    ///         This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///         the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///         any release. You should only use it directly in your code with extreme caution and knowing that
    ///         doing so can result in application failures when updating to a new Entity Framework Core release.
    ///     </para>
    ///     <para>
    ///         The service lifetime is <see cref="ServiceLifetime.Singleton" />. This means a single instance
    ///         is used by many <see cref="DbContext" /> instances. The implementation must be thread-safe.
    ///         This service cannot depend on services registered as <see cref="ServiceLifetime.Scoped" />.
    ///     </para>
    /// </summary>
    public class TaosUpdateSqlGenerator : IUpdateSqlGenerator// UpdateSqlGenerator
    {

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public TaosUpdateSqlGenerator([NotNull] UpdateSqlGeneratorDependencies dependencies)
        {
            Dependencies = dependencies;
        }



        /// <summary>
        ///     Relational provider-specific dependencies for this service.
        /// </summary>
        protected UpdateSqlGeneratorDependencies Dependencies { get; }

        /// <summary>
        ///     Helpers for generating update SQL.
        /// </summary>
        protected ISqlGenerationHelper SqlGenerationHelper
            => Dependencies.SqlGenerationHelper;

        /// <inheritdoc />
        public ResultSetMapping AppendInsertOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition,
            out bool requiresTransaction)
            => AppendInsertReturningOperation(commandStringBuilder, command, commandPosition, out requiresTransaction);

        /// <inheritdoc />
        public ResultSetMapping AppendInsertOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition)
            => AppendInsertOperation(commandStringBuilder, command, commandPosition, out _);

        /// <summary>
        ///     Appends SQL for inserting a row to the commands being built, via an INSERT containing an optional RETURNING clause to retrieve
        ///     any database-generated values.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="command">The command that represents the delete operation.</param>
        /// <param name="commandPosition">The ordinal of this command in the batch.</param>
        /// <param name="requiresTransaction">Returns whether the SQL appended must be executed in a transaction to work correctly.</param>
        /// <returns>The <see cref="ResultSetMapping" /> for the command.</returns>
        public ResultSetMapping AppendInsertReturningOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition,
            out bool requiresTransaction)
        {
            var operations = command.ColumnModifications;

            var writeOperations = operations.Where(o => o.IsWrite).ToList();
            var readOperations = operations.Where(o => o.IsRead).ToList();

            AppendInsertCommand(commandStringBuilder, command, writeOperations, readOperations);

            requiresTransaction = false;

            return readOperations.Count > 0 ? ResultSetMapping.LastInResultSet : ResultSetMapping.NoResults;
        }

        /// <inheritdoc />
        public ResultSetMapping AppendUpdateOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition,
            out bool requiresTransaction)
            => AppendUpdateReturningOperation(commandStringBuilder, command, commandPosition, out requiresTransaction);

        /// <inheritdoc />
        public ResultSetMapping AppendUpdateOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition)
            => AppendUpdateOperation(commandStringBuilder, command, commandPosition, out _);

        /// <summary>
        ///     Appends SQL for updating a row to the commands being built, via an UPDATE containing an RETURNING clause to retrieve any
        ///     database-generated values or for concurrency checking.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="command">The command that represents the delete operation.</param>
        /// <param name="commandPosition">The ordinal of this command in the batch.</param>
        /// <param name="requiresTransaction">Returns whether the SQL appended must be executed in a transaction to work correctly.</param>
        /// <returns>The <see cref="ResultSetMapping" /> for the command.</returns>
        protected ResultSetMapping AppendUpdateReturningOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition,
            out bool requiresTransaction)
        {
            var name = command.TableName;
            var schema = command.Schema;
            var operations = command.ColumnModifications;

            var writeOperations = operations.Where(o => o.IsWrite).ToList();
            var conditionOperations = operations.Where(o => o.IsCondition).ToList();
            var readOperations = operations.Where(o => o.IsRead).ToList();

            requiresTransaction = false;

            var anyReadOperations = readOperations.Count > 0;

            AppendUpdateCommand(
                commandStringBuilder, name, schema, writeOperations, readOperations, conditionOperations,
                appendReturningOneClause: !anyReadOperations);

            return anyReadOperations
                ? ResultSetMapping.LastInResultSet
                : ResultSetMapping.LastInResultSet | ResultSetMapping.ResultSetWithRowsAffectedOnly;
        }

        /// <inheritdoc />
        public ResultSetMapping AppendDeleteOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition,
            out bool requiresTransaction)
            => AppendDeleteReturningOperation(commandStringBuilder, command, commandPosition, out requiresTransaction);

        /// <inheritdoc />
        public ResultSetMapping AppendDeleteOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition)
            => AppendDeleteOperation(commandStringBuilder, command, commandPosition, out _);

        /// <summary>
        ///     Appends SQL for deleting a row to the commands being built, via a DELETE containing a RETURNING clause for concurrency checking.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="command">The command that represents the delete operation.</param>
        /// <param name="commandPosition">The ordinal of this command in the batch.</param>
        /// <param name="requiresTransaction">Returns whether the SQL appended must be executed in a transaction to work correctly.</param>
        /// <returns>The <see cref="ResultSetMapping" /> for the command.</returns>
        protected ResultSetMapping AppendDeleteReturningOperation(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition,
            out bool requiresTransaction)
        {
            var name = command.TableName;
            var schema = command.Schema;
            var conditionOperations = command.ColumnModifications.Where(o => o.IsCondition).ToList();

            requiresTransaction = false;

            AppendDeleteCommand(
                commandStringBuilder, name, schema, Array.Empty<IColumnModification>(), conditionOperations, appendReturningOneClause: true);

            return ResultSetMapping.LastInResultSet | ResultSetMapping.ResultSetWithRowsAffectedOnly;
        }

        /// <summary>
        ///     Appends a SQL command for inserting a row to the commands being built.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="name">The name of the table.</param>
        /// <param name="schema">The table schema, or <see langword="null" /> to use the default schema.</param>
        /// <param name="writeOperations">The operations with the values to insert for each column.</param>
        /// <param name="readOperations">The operations for column values to be read back.</param>
        protected void AppendInsertCommand(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            IReadOnlyList<IColumnModification> writeOperations,
            IReadOnlyList<IColumnModification> readOperations)
        {
            var tableMap = command.Table.EntityTypeMappings.FirstOrDefault(f =>
            {
                var attr = f.TypeBase.ClrType.GetCustomAttribute<TaosAttribute>();
                if (attr != null && !string.IsNullOrEmpty(attr.TableName))
                {
                    return attr.TableName == command.TableName;
                }
                return f.TypeBase. ClrType.Name == command.TableName;
            });
            var isSupperTable = false;
            var subTableName = string.Empty;

            var propertyAttrMaps = tableMap.TypeBase.ClrType.GetProperties().Select(s => (PropertyInfo: s, Attr: s.GetCustomAttribute<TaosColumnAttribute>())).ToList();
            if (tableMap != null)
            {
                var taosAttr = tableMap.TypeBase.ClrType.GetCustomAttribute<TaosAttribute>();
                if (taosAttr != null)
                {
                    isSupperTable = taosAttr.IsSuperTable;
                    if (isSupperTable)
                    {
                        var obj = ((InternalEntityEntry)command.Entries.FirstOrDefault())?.Entity;
                        if (obj != null)
                        {
                            var subTableNameMap = propertyAttrMaps.FirstOrDefault(f => f.Attr != null && f.Attr.IsSubTableName);
                            if (subTableNameMap.Attr != null)
                            {
                                subTableName = subTableNameMap.PropertyInfo.GetValue(obj)?.ToString();
                            }
                        }
                    }
                }
            }
            commandStringBuilder.Append("INSERT INTO ");

            var (updateEntity, clrType, attr) = command.Entries
                .Select(f => (UpdateEntity: f, ClrType: f.EntityType.ClrType, Attr: f.EntityType.ClrType.GetCustomAttribute<TaosAttribute>()))
                .Where(w => w.Attr.TableName == command.TableName)
                .FirstOrDefault();

            commandStringBuilder.Append("\"");
            SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, (isSupperTable ? subTableName : command.TableName), command.Schema);
            commandStringBuilder.Append("\"");
            if (isSupperTable)
            {
                commandStringBuilder.Append($"  USING {command.TableName} ");
            }

            var tagHeadStringBuilder = new StringBuilder();
            var tagContentStringBuilder = new StringBuilder();
            var valueHeadStringBuilder = new StringBuilder();
            var valueContentStringBuilder = new StringBuilder();

            if (writeOperations.Count > 0)
            {
                valueHeadStringBuilder.Append("(");
                valueContentStringBuilder.Append("(");
                foreach (var operation in writeOperations)
                {
                    var (pinfo, pattr) = propertyAttrMaps.First(f => f.Attr.ColumnName == operation.ColumnName || f.PropertyInfo.Name == operation.ColumnName);
                    if (!pattr.IsTag)
                    {
                        if (valueHeadStringBuilder[valueHeadStringBuilder.Length - 1] != '(')
                        {
                            valueHeadStringBuilder.Append(',');
                            valueContentStringBuilder.Append(',');
                        }
                        valueHeadStringBuilder.Append(!string.IsNullOrWhiteSpace(pattr.ColumnName) ? pattr.ColumnName : pinfo.Name);
                        valueContentStringBuilder.Append(SqlGenerationHelper.GenerateParameterNamePlaceholder(operation.ParameterName));
                    }
                    else
                    {
                        if (tagHeadStringBuilder.Length != 0)
                        {
                            tagHeadStringBuilder.Append(',');
                            tagContentStringBuilder.Append(',');
                        }
                        else
                        {
                            tagHeadStringBuilder.Append('(');
                            tagContentStringBuilder.Append('(');
                        }
                        tagHeadStringBuilder.Append(!string.IsNullOrWhiteSpace(pattr.ColumnName) ? pattr.ColumnName : pinfo.Name);
                        tagContentStringBuilder.Append("$" + operation.ParameterName);
                    }
                }
                if (tagHeadStringBuilder.Length == 0)
                {
                    tagHeadStringBuilder.Append(')');
                    tagContentStringBuilder.Append(')');
                }
                valueHeadStringBuilder.Append(")");
                valueContentStringBuilder.Append(")");
            }
            if (tagHeadStringBuilder.Length > 0)
            {
                tagHeadStringBuilder.Append(")");
                tagContentStringBuilder.Append(")");
            }
            //AppendInsertCommandHeader(commandStringBuilder, command, writeOperations);
            //AppendValuesHeader(commandStringBuilder, writeOperations);
            //AppendValues(commandStringBuilder, command, writeOperations); 
            //AppendReturningClause(commandStringBuilder, readOperations);

            if (tagHeadStringBuilder.Length > 0)
            {
                commandStringBuilder
                    .Append(" ")
                    .Append(tagHeadStringBuilder)
                    .Append(" TAGS ")
                    .Append(tagContentStringBuilder)
                    .Append(" ")
                    ;
            }

            commandStringBuilder
                    .Append(" ")
                    .Append(valueHeadStringBuilder)
                    .Append(" VALUES ")
                    .Append(valueContentStringBuilder)
                    .Append(" ")
                    ;

            commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);
        }

        /// <summary>
        ///     Appends a SQL command for updating a row to the commands being built.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="name">The name of the table.</param>
        /// <param name="schema">The table schema, or <see langword="null" /> to use the default schema.</param>
        /// <param name="writeOperations">The operations for each column.</param>
        /// <param name="readOperations">The operations for column values to be read back.</param>
        /// <param name="conditionOperations">The operations used to generate the <c>WHERE</c> clause for the update.</param>
        /// <param name="appendReturningOneClause">Whether to append an additional constant of 1 to be read back.</param>
        protected void AppendUpdateCommand(
            StringBuilder commandStringBuilder,
            string name,
            string? schema,
            IReadOnlyList<IColumnModification> writeOperations,
            IReadOnlyList<IColumnModification> readOperations,
            IReadOnlyList<IColumnModification> conditionOperations,
            bool appendReturningOneClause = false)
        {
            AppendUpdateCommandHeader(commandStringBuilder, name, schema, writeOperations);
            AppendWhereClause(commandStringBuilder, conditionOperations);
            AppendReturningClause(commandStringBuilder, readOperations, appendReturningOneClause ? "1" : null);
            commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);
        }

        /// <summary>
        ///     Appends a SQL command for deleting a row to the commands being built.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="name">The name of the table.</param>
        /// <param name="schema">The table schema, or <see langword="null" /> to use the default schema.</param>
        /// <param name="readOperations">The operations for column values to be read back.</param>
        /// <param name="conditionOperations">The operations used to generate the <c>WHERE</c> clause for the delete.</param>
        /// <param name="appendReturningOneClause">Whether to append an additional constant of 1 to be read back.</param>
        protected void AppendDeleteCommand(
            StringBuilder commandStringBuilder,
            string name,
            string? schema,
            IReadOnlyList<IColumnModification> readOperations,
            IReadOnlyList<IColumnModification> conditionOperations,
            bool appendReturningOneClause = false)
        {
            AppendDeleteCommandHeader(commandStringBuilder, name, schema);
            AppendWhereClause(commandStringBuilder, conditionOperations);
            AppendReturningClause(commandStringBuilder, readOperations, appendReturningOneClause ? "1" : null);
            commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);
        }

        /// <summary>
        ///     Appends a SQL fragment for starting an <c>INSERT</c>.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="name">The name of the table.</param>
        /// <param name="schema">The table schema, or <see langword="null" /> to use the default schema.</param>
        /// <param name="operations">The operations representing the data to be inserted.</param>
        protected void AppendInsertCommandHeader(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            IReadOnlyList<IColumnModification> operations)
        {
            var tableMap = command.Table.EntityTypeMappings.FirstOrDefault(f =>
            {
                var attr = f.TypeBase.ClrType.GetCustomAttribute<TaosAttribute>();
                if (attr != null && !string.IsNullOrEmpty(attr.TableName))
                {
                    return attr.TableName == command.TableName;
                }
                return f.TypeBase.ClrType.Name == command.TableName;
            });
            var isSupperTable = false;
            var subTableName = string.Empty;

            var propertyAttrMaps = tableMap.TypeBase.ClrType.GetProperties().Select(s => (PropertyInfo: s, Attr: s.GetCustomAttribute<TaosColumnAttribute>())).ToList();
            if (tableMap != null)
            {
                var taosAttr = tableMap.TypeBase.ClrType.GetCustomAttribute<TaosAttribute>();
                if (taosAttr != null)
                {
                    isSupperTable = taosAttr.IsSuperTable;
                    if (isSupperTable)
                    {
                        var obj = ((InternalEntityEntry)command.Entries.FirstOrDefault())?.Entity;
                        if (obj != null)
                        {
                            var subTableNameMap = propertyAttrMaps.FirstOrDefault(f => f.Attr != null && f.Attr.IsSubTableName);
                            if (subTableNameMap.Attr != null)
                            {
                                subTableName = subTableNameMap.PropertyInfo.GetValue(obj)?.ToString();
                            }
                        }
                    }
                }
            }
            commandStringBuilder.Append("INSERT INTO ");
            SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, command.TableName, command.Schema);

            if (operations.Count > 0)
            {
                commandStringBuilder
                    .Append(" (")
                    .AppendJoin(
                        operations,
                        SqlGenerationHelper,
                        (sb, o, helper) => helper.DelimitIdentifier(sb, o.ColumnName))
                    .Append(')');
            }
        }

        /// <summary>
        ///     Appends a SQL fragment for starting a <c>DELETE</c>.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="name">The name of the table.</param>
        /// <param name="schema">The table schema, or <see langword="null" /> to use the default schema.</param>
        protected void AppendDeleteCommandHeader(
            StringBuilder commandStringBuilder,
            string name,
            string? schema)
        {
            commandStringBuilder.Append("DELETE FROM ");
            SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, name, schema);
        }

        /// <summary>
        ///     Appends a SQL fragment for starting an <c>UPDATE</c>.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="name">The name of the table.</param>
        /// <param name="schema">The table schema, or <see langword="null" /> to use the default schema.</param>
        /// <param name="operations">The operations representing the data to be updated.</param>
        protected void AppendUpdateCommandHeader(
            StringBuilder commandStringBuilder,
            string name,
            string? schema,
            IReadOnlyList<IColumnModification> operations)
        {
            commandStringBuilder.Append("UPDATE ");
            SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, name, schema);
            commandStringBuilder.Append(" SET ")
                .AppendJoin(
                    operations,
                    (this, name, schema),
                    (sb, o, p) =>
                    {
                        var (g, n, s) = p;
                        g.SqlGenerationHelper.DelimitIdentifier(sb, o.ColumnName);
                        sb.Append(" = ");
                        AppendUpdateColumnValue(g.SqlGenerationHelper, o, sb, n, s);
                    });
        }

        /// <summary>
        ///     Appends a SQL fragment representing the value that is assigned to a column which is being updated.
        /// </summary>
        /// <param name="updateSqlGeneratorHelper">The update sql generator helper.</param>
        /// <param name="columnModification">The operation representing the data to be updated.</param>
        /// <param name="stringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="name">The name of the table.</param>
        /// <param name="schema">The table schema, or <see langword="null" /> to use the default schema.</param>
        protected void AppendUpdateColumnValue(
            ISqlGenerationHelper updateSqlGeneratorHelper,
            IColumnModification columnModification,
            StringBuilder stringBuilder,
            string name,
            string? schema)
        {
            if (!columnModification.UseCurrentValueParameter)
            {
                AppendSqlLiteral(stringBuilder, columnModification, name, schema);
            }
            else
            {
                updateSqlGeneratorHelper.GenerateParameterNamePlaceholder(
                    stringBuilder, columnModification.ParameterName);
            }
        }

        /// <inheritdoc />
        public ResultSetMapping AppendStoredProcedureCall(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            int commandPosition,
            out bool requiresTransaction)
        {
            Check.DebugAssert(command.StoreStoredProcedure is not null, "command.StoredProcedure is not null");

            var storedProcedure = command.StoreStoredProcedure;

            var resultSetMapping = ResultSetMapping.NoResults;

            foreach (var resultColumn in storedProcedure.ResultColumns)
            {
                resultSetMapping = ResultSetMapping.LastInResultSet;

                if (resultColumn == command.RowsAffectedColumn)
                {
                    resultSetMapping |= ResultSetMapping.ResultSetWithRowsAffectedOnly;
                }
                else
                {
                    resultSetMapping = ResultSetMapping.LastInResultSet;
                    break;
                }
            }

            Check.DebugAssert(
                storedProcedure.Parameters.Any() || storedProcedure.ResultColumns.Any(),
                "Stored procedure call with neither parameters nor result columns");

            commandStringBuilder.Append("CALL ");

            SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, storedProcedure.Name, storedProcedure.Schema);

            commandStringBuilder.Append('(');

            var first = true;

            // Only positional parameter style supported for now, see #28439

            // Note: the column modifications are already ordered according to the sproc parameter ordering
            // (see ModificationCommand.GenerateColumnModifications)
            for (var i = 0; i < command.ColumnModifications.Count; i++)
            {
                var columnModification = command.ColumnModifications[i];

                if (columnModification.Column is not IStoreStoredProcedureParameter parameter)
                {
                    continue;
                }

                if (first)
                {
                    first = false;
                }
                else
                {
                    commandStringBuilder.Append(", ");
                }

                Check.DebugAssert(columnModification.UseParameter, "Column modification matched a parameter, but UseParameter is false");

                SqlGenerationHelper.GenerateParameterNamePlaceholder(
                    commandStringBuilder, columnModification.UseOriginalValueParameter
                        ? columnModification.OriginalParameterName!
                        : columnModification.ParameterName!);

                if (parameter.Direction.HasFlag(ParameterDirection.Output))
                {
                    resultSetMapping |= ResultSetMapping.HasOutputParameters;
                }
            }

            commandStringBuilder.Append(')');

            commandStringBuilder.AppendLine(SqlGenerationHelper.StatementTerminator);

            requiresTransaction = true;

            return resultSetMapping;
        }

        /// <summary>
        ///     Appends a SQL fragment for a <c>VALUES</c>.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="operations">The operations for which there are values.</param>
        protected void AppendValuesHeader(
            StringBuilder commandStringBuilder,
            IReadOnlyList<IColumnModification> operations)
        {
            commandStringBuilder.AppendLine();
            commandStringBuilder.Append(operations.Count > 0 ? "VALUES " : "DEFAULT VALUES");
        }

        /// <summary>
        ///     Appends values after a <see cref="AppendValuesHeader" /> call.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="name">The name of the table.</param>
        /// <param name="schema">The table schema, or <see langword="null" /> to use the default schema.</param>
        /// <param name="operations">The operations for which there are values.</param>
        protected void AppendValues(
            StringBuilder commandStringBuilder,
            IReadOnlyModificationCommand command,
            IReadOnlyList<IColumnModification> operations)
        {
            if (operations.Count > 0)
            {
                commandStringBuilder
                    .Append('(')
                    .AppendJoin(
                        operations,
                        (this, command.TableName, command.Schema),
                        (sb, o, p) =>
                        {
                            if (o.IsWrite)
                            {
                                var (g, n, s) = p;
                                if (!o.UseCurrentValueParameter)
                                {
                                    AppendSqlLiteral(sb, o, n, s);
                                }
                                else
                                {
                                    g.SqlGenerationHelper.GenerateParameterNamePlaceholder(sb, o.ParameterName);
                                }
                            }
                            else
                            {
                                sb.Append("DEFAULT");
                            }
                        })
                    .Append(')');
            }
        }

        /// <summary>
        ///     Appends a clause used to return generated values from an INSERT or UPDATE statement.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="operations">The operations for column values to be read back.</param>
        /// <param name="additionalValues">Additional values to be read back.</param>
        protected void AppendReturningClause(
            StringBuilder commandStringBuilder,
            IReadOnlyList<IColumnModification> operations,
            string? additionalValues = null)
        {
            if (operations.Count > 0 || additionalValues is not null)
            {
                commandStringBuilder
                    .AppendLine()
                    .Append("RETURNING ")
                    .AppendJoin(
                        operations,
                        SqlGenerationHelper,
                        (sb, o, helper) => helper.DelimitIdentifier(sb, o.ColumnName));

                if (additionalValues is not null)
                {
                    if (operations.Count > 0)
                    {
                        commandStringBuilder.Append(", ");
                    }

                    commandStringBuilder.Append("1");
                }
            }
        }

        /// <summary>
        ///     Appends a <c>WHERE</c> clause.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="operations">The operations from which to build the conditions.</param>
        protected void AppendWhereClause(
            StringBuilder commandStringBuilder,
            IReadOnlyList<IColumnModification> operations)
        {
            if (operations.Count > 0)
            {
                commandStringBuilder
                    .AppendLine()
                    .Append("WHERE ")
                    .AppendJoin(operations, (sb, v) => AppendWhereCondition(sb, v, v.UseOriginalValueParameter), " AND ");
            }
        }

        /// <summary>
        ///     Appends a <c>WHERE</c> condition for the given column.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        /// <param name="columnModification">The column for which the condition is being generated.</param>
        /// <param name="useOriginalValue">
        ///     If <see langword="true" />, then the original value will be used in the condition, otherwise the current value will be used.
        /// </param>
        protected void AppendWhereCondition(
            StringBuilder commandStringBuilder,
            IColumnModification columnModification,
            bool useOriginalValue)
        {
            SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, columnModification.ColumnName);

            var parameterValue = useOriginalValue
                ? columnModification.OriginalValue
                : columnModification.Value;

            if (parameterValue == null)
            {
                commandStringBuilder.Append(" IS NULL");
            }
            else
            {
                commandStringBuilder.Append(" = ");
                if (!columnModification.UseParameter)
                {
                    AppendSqlLiteral(commandStringBuilder, columnModification, null, null);
                }
                else
                {
                    SqlGenerationHelper.GenerateParameterNamePlaceholder(
                        commandStringBuilder, useOriginalValue
                            ? columnModification.OriginalParameterName!
                            : columnModification.ParameterName!);
                }
            }
        }

        /// <summary>
        ///     Appends SQL text that defines the start of a batch.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be appended.</param>
        public void AppendBatchHeader(StringBuilder commandStringBuilder)
        {
        }

        /// <summary>
        ///     Prepends a SQL command for turning on autocommit mode in the database, in case it is off.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL should be prepended.</param>
        public void PrependEnsureAutocommit(StringBuilder commandStringBuilder)
        {
        }

        /// <inheritdoc />
        public string GenerateNextSequenceValueOperation(string name, string? schema)
        {
            var commandStringBuilder = new StringBuilder();
            AppendNextSequenceValueOperation(commandStringBuilder, name, schema);
            return commandStringBuilder.ToString();
        }

        /// <inheritdoc />
        public void AppendNextSequenceValueOperation(StringBuilder commandStringBuilder, string name, string? schema)
        {
            commandStringBuilder.Append("SELECT ");
            AppendObtainNextSequenceValueOperation(commandStringBuilder, name, schema);
        }

        /// <inheritdoc />
        public string GenerateObtainNextSequenceValueOperation(string name, string? schema)
        {
            var commandStringBuilder = new StringBuilder();
            AppendObtainNextSequenceValueOperation(commandStringBuilder, name, schema);
            return commandStringBuilder.ToString();
        }

        /// <inheritdoc />
        public void AppendObtainNextSequenceValueOperation(StringBuilder commandStringBuilder, string name, string? schema)
        {
            commandStringBuilder.Append("NEXT VALUE FOR ");
            SqlGenerationHelper.DelimitIdentifier(commandStringBuilder, name, schema);
        }

        /// <summary>
        ///     Appends the literal value for <paramref name="modification" /> to the command being built by
        ///     <paramref name="commandStringBuilder" />.
        /// </summary>
        /// <param name="commandStringBuilder">The builder to which the SQL fragment should be appended.</param>
        /// <param name="modification">The column modification whose literal should get appended.</param>
        /// <param name="tableName">The table name of the column, used when an exception is thrown.</param>
        /// <param name="schema">The schema of the column, used when an exception is thrown.</param>
        protected static void AppendSqlLiteral(
            StringBuilder commandStringBuilder,
            IColumnModification modification,
            string? tableName,
            string? schema)
        {
            if (modification.TypeMapping == null)
            {
                var columnName = modification.ColumnName;
                if (tableName != null)
                {
                    columnName = tableName + "." + columnName;

                    if (schema != null)
                    {
                        columnName = schema + "." + columnName;
                    }
                }

                throw new InvalidOperationException(
                    RelationalStrings.UnsupportedDataOperationStoreType(modification.ColumnType, columnName));
            }

            commandStringBuilder.Append(modification.TypeMapping.GenerateProviderValueSqlLiteral(modification.Value));
        }
    }
}
