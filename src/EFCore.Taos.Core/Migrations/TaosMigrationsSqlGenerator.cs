// Copyright (c)  Maikebing. All rights reserved.
// Licensed under the MIT License, See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using IoTSharp.EntityFrameworkCore.Taos.Internal;
using IoTSharp.EntityFrameworkCore.Taos.Metadata.Internal;
using IoTSharp.EntityFrameworkCore.Taos.Storage.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using Microsoft.Extensions.DependencyInjection;
using IoTSharp.Data.Taos;
using System.Text;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Xml.Linq;
using System.Reflection;
using IoTSharp.EntityFrameworkCore.Taos;

namespace Microsoft.EntityFrameworkCore.Migrations
{
    /// <summary>
    ///     <para>
    ///         Taos-specific implementation of <see cref="MigrationsSqlGenerator" />.
    ///     </para>
    ///     <para>
    ///         The service lifetime is <see cref="ServiceLifetime.Scoped" />. This means that each
    ///         <see cref="DbContext" /> instance will use its own instance of this service.
    ///         The implementation may depend on other services registered with any lifetime.
    ///         The implementation does not need to be thread-safe.
    ///     </para>
    /// </summary>
    public class TaosMigrationsSqlGenerator : MigrationsSqlGenerator
    {
        private readonly IMigrationsAnnotationProvider _migrationsAnnotations;
        private readonly TaosConnectionStringBuilder _taosConnectionStringBuilder;
        /// <summary>
        ///     Creates a new <see cref="TaosMigrationsSqlGenerator" /> instance.
        /// </summary>
        /// <param name="dependencies"> Parameter object containing dependencies for this service. </param>
        /// <param name="migrationsAnnotations"> Provider-specific Migrations annotations to use. </param>
        public TaosMigrationsSqlGenerator(
            [NotNull] MigrationsSqlGeneratorDependencies dependencies,
            [NotNull] IMigrationsAnnotationProvider migrationsAnnotations, IRelationalConnection connection)
            : base(dependencies)
        {
            _migrationsAnnotations = migrationsAnnotations;
            _taosConnectionStringBuilder = new TaosConnectionStringBuilder(connection.ConnectionString);
        }


        private bool IsSpatialiteColumn(AddColumnOperation operation, IModel model)
            => TaosTypeMappingSource.IsSpatialiteType(
                operation.ColumnType
                ?? GetColumnType(
                    operation.Schema,
                    operation.Table,
                    operation.Name,
                    operation,
                    model));

        /// <summary>
        /// Builds commands for the given <see cref="InsertDataOperation" /> by making calls on the given
        /// <see cref="MigrationCommandListBuilder" />, and then terminates the final command.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        /// <param name="terminate"> Indicates whether or not to terminate the command after generating SQL for the operation. </param>
        protected override void Generate(
            InsertDataOperation operation,
            IModel model,
            MigrationCommandListBuilder builder,
            bool terminate = true)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            var sqlBuilder = new StringBuilder();


            builder.Append(sqlBuilder.ToString());

            if (terminate)
                builder.EndCommand();
        }

        private IReadOnlyList<MigrationOperation> RewriteOperations(
            IReadOnlyList<MigrationOperation> migrationOperations,
            IModel model)
        {
            var operations = new List<MigrationOperation>();
            foreach (var operation in migrationOperations)
            {
                if (operation is AddForeignKeyOperation foreignKeyOperation)
                {
                    var table = migrationOperations
                        .OfType<CreateTableOperation>()
                        .FirstOrDefault(o => o.Name == foreignKeyOperation.Table);
                    table.Schema = _taosConnectionStringBuilder.DataBase;
                    if (table != null)
                    {
                        table.ForeignKeys.Add(foreignKeyOperation);
                    }
                    else
                    {
                        operations.Add(operation);
                    }
                }
                else if (operation is CreateTableOperation createTableOperation)
                {
                    var spatialiteColumns = new Stack<AddColumnOperation>();
                    createTableOperation.Schema = _taosConnectionStringBuilder.DataBase;
                    for (var i = createTableOperation.Columns.Count - 1; i >= 0; i--)
                    {
                        var addColumnOperation = createTableOperation.Columns[i];

                        if (IsSpatialiteColumn(addColumnOperation, model))
                        {
                            spatialiteColumns.Push(addColumnOperation);
                            createTableOperation.Columns.RemoveAt(i);
                        }
                    }

                    operations.Add(operation);
                    operations.AddRange(spatialiteColumns);
                }
                else
                {
                    operations.Add(operation);
                }
            }

            return operations;
        }

        /// <summary>
        ///     Builds commands for the given <see cref="AlterDatabaseOperation" /> by making calls on the given
        ///     <see cref="MigrationCommandListBuilder" />.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        protected override void Generate(AlterDatabaseOperation operation, IModel model, MigrationCommandListBuilder builder)
        {
            if (operation[TaosAnnotationNames.InitSpatialMetaData] as bool? != true
                || operation.OldDatabase[TaosAnnotationNames.InitSpatialMetaData] as bool? == true)
            {
                return;
            }

            builder
                .Append("SELECT InitSpatialMetaData()")
                .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }

        protected override void Generate(SqlOperation operation, IModel model, MigrationCommandListBuilder builder)
        {
            base.Generate(operation, model, builder);
        }

        /// <summary>
        ///     Builds commands for the given <see cref="AddColumnOperation" /> by making calls on the given
        ///     <see cref="MigrationCommandListBuilder" />.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        /// <param name="terminate"> Indicates whether or not to terminate the command after generating SQL for the operation. </param>
        protected override void Generate(AddColumnOperation operation, IModel model, MigrationCommandListBuilder builder, bool terminate)
        {
            if (!IsSpatialiteColumn(operation, model))
            {
                base.Generate(operation, model, builder, terminate);

                return;
            }

            var stringTypeMapping = Dependencies.TypeMappingSource.GetMapping(typeof(string));
            var longTypeMapping = Dependencies.TypeMappingSource.GetMapping(typeof(long));

            var srid = operation[TaosAnnotationNames.Srid] as int? ?? 0;
            var dimension = operation[TaosAnnotationNames.Dimension] as string;

            var geometryType = operation.ColumnType
                ?? GetColumnType(
                    operation.Schema,
                    operation.Table,
                    operation.Name,
                    operation,
                    model);
            if (!string.IsNullOrEmpty(dimension))
            {
                geometryType += dimension;
            }

            builder
                .Append("SELECT AddGeometryColumn(")
                .Append(stringTypeMapping.GenerateSqlLiteral(operation.Table))
                .Append(", ")
                .Append(stringTypeMapping.GenerateSqlLiteral(operation.Name))
                .Append(", ")
                .Append(longTypeMapping.GenerateSqlLiteral(srid))
                .Append(", ")
                .Append(stringTypeMapping.GenerateSqlLiteral(geometryType))
                .Append(", -1, ")
                .Append(operation.IsNullable ? "0" : "1")
                .Append(")");

            if (terminate)
            {
                builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
                EndStatement(builder);
            }
            else
            {
                Debug.Fail("I have a bad feeling about this. Geometry columns don't compose well.");
            }
        }


        /// <summary>
        ///     Builds commands for the given <see cref="DropIndexOperation" />
        ///     by making calls on the given <see cref="MigrationCommandListBuilder" />.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        /// <param name="terminate"> Indicates whether or not to terminate the command after generating SQL for the operation. </param>
        protected override void Generate(
            DropIndexOperation operation,
            IModel model,
            MigrationCommandListBuilder builder,
            bool terminate)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            builder
                .Append("DROP INDEX ")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name));

            if (terminate)
            {
                builder
                    .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator)
                    .EndCommand();
            }
        }


        /// <summary>
        ///     Builds commands for the given <see cref="RenameTableOperation" />
        ///     by making calls on the given <see cref="MigrationCommandListBuilder" />.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        protected override void Generate(RenameTableOperation operation, IModel model, MigrationCommandListBuilder builder)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            if (operation.NewName != null
                && operation.NewName != operation.Name)
            {
                builder
                    .Append("ALTER TABLE ")
                    .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
                    .Append(" RENAME TO ")
                    .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.NewName))
                    .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator)
                    .EndCommand();
            }
        }

        /// <summary>
        ///     Builds commands for the given <see cref="RenameTableOperation" />
        ///     by making calls on the given <see cref="MigrationCommandListBuilder" />.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        protected override void Generate(RenameColumnOperation operation, IModel model, MigrationCommandListBuilder builder)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            builder
                .Append("ALTER TABLE ")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table))
                .Append(" RENAME COLUMN ")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
                .Append(" TO ")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.NewName))
                .AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator)
                .EndCommand();
        }

        /// <summary>
        ///     Builds commands for the given <see cref="CreateTableOperation" /> by making calls on the given
        ///     <see cref="MigrationCommandListBuilder" />.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        /// <param name="terminate"> Indicates whether or not to terminate the command after generating SQL for the operation. </param>
        protected override void Generate(
            CreateTableOperation operation,
            IModel model,
            MigrationCommandListBuilder builder,
            bool terminate = true)
        {

            //var entityTypeWithNullPk
            //    = model.GetEntityTypes()
            //        .FirstOrDefault(et => !((IConventionEntityType)et).IsKeyless && et.BaseType == null && et.FindPrimaryKey() == null);


            var table = model?.GetRelationalModel().FindTable(operation.Name, operation.Schema);
            var mt = table.EntityTypeMappings.FirstOrDefault().EntityType.ClrType;
            var tabInfo = mt.GetCustomAttribute<TaosAttribute>();
            var isSupertable = tabInfo?.IsSuperTable ?? false;
            //IColumn column = table?.FindColumn(operation.Name);

            var tableName = operation.Name;
            if (!string.IsNullOrEmpty(tabInfo.TableName))
            {
                tableName = tabInfo.TableName;
            }

            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));
            operation.Schema = _taosConnectionStringBuilder.DataBase;
            builder
                .Append($"CREATE {(isSupertable ? "STABLE" : "TABLE")} IF NOT EXISTS ")
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(tableName, operation.Schema))
                .AppendLine(" ");

            CreateTableColumns(operation, model, builder);

            if (terminate)
            {
                builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
                EndStatement(builder);
            }
        }

        protected override void ColumnDefinition(AddColumnOperation operation, IModel model, MigrationCommandListBuilder builder)
        {
            var info = operation.ClrType.GetCustomAttribute<TaosColumnAttribute>();
            var columnName = operation.Name;
            if (info != null && !string.IsNullOrWhiteSpace(info.ColumnName))
            {
                columnName = info.ColumnName;
            }
            builder
                .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(columnName))
                .Append(" ")
                .Append(operation.ColumnType ?? GetColumnType(operation.Schema, operation.Table, operation.Name, operation, model));
            switch (operation.ColumnType)
            {
                case "BINARY":
                case "NCHAR":
                case "VARCHAR":
                case "GEOMETRY":
                case "VARBINARY":
                    builder.Append($"({operation.MaxLength})");
                    break;
                default:
                    break;
            }
        }
        //protected override string GetColumnType(string schema, string table, string name, ColumnOperation operation, IModel model)
        //{
        //    return base.GetColumnType(schema, table, name, operation, model);
        //}
        /// <summary>
        ///     Generates a SQL fragment for the column definitions in an <see cref="CreateTableOperation" />.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to add the SQL fragment. </param>
        protected override void CreateTableColumns(
            CreateTableOperation operation,
            IModel model,
            MigrationCommandListBuilder builder)
        {
            Check.NotNull(operation, nameof(operation));
            Check.NotNull(builder, nameof(builder));

            CreateTableColumnsWithComments(operation, model, builder);
        }

        private void CreateTableColumnsWithComments(
            CreateTableOperation operation,
            IModel model,
            MigrationCommandListBuilder builder)
        {
            var table = model?.GetRelationalModel().FindTable(operation.Name, null);
            var mt = table.EntityTypeMappings.FirstOrDefault().EntityType.ClrType;
            var propInfos = mt.GetProperties()
                .Select(s => (propertyInfo: s, Info: s.GetCustomAttribute<TaosColumnAttribute>()))
                .Where(w => w.Info != null)
                .ToList();

            var opertionTagsGroup = operation.Columns.GroupBy(w =>
            {
                var propInfo = propInfos.FirstOrDefault(f => f.propertyInfo.Name == w.Name);

                return propInfo.Info?.IsTag ?? false;

            }).ToList();

            var othertagsOpertions = opertionTagsGroup.FirstOrDefault(w => !w.Key).ToList();
            if (othertagsOpertions != null && othertagsOpertions.Count() > 0)
            {
                builder.Append("(");
                using (builder.Indent())
                {
                    for (var i = 0; i < othertagsOpertions.Count; i++)
                    {
                        var column = othertagsOpertions[i];

                        if (i > 0)
                        {
                            builder.AppendLine();
                        }

                        this.ColumnDefinition(column, model, builder);

                        if (i != othertagsOpertions.Count - 1)
                        {
                            builder.AppendLine(",");
                        }
                    }

                    builder.AppendLine();
                }

                builder.Append(")");
            }

            var tagsOpertions = opertionTagsGroup.FirstOrDefault(w => w.Key).ToList();
            if (tagsOpertions != null && tagsOpertions.Count() > 0)
            {
                builder.Append(" TAGS (");
                using (builder.Indent())
                {
                    builder.AppendLine();
                    for (var i = 0; i < tagsOpertions.Count; i++)
                    {
                        var column = tagsOpertions[i];

                        if (i > 0)
                        {
                            builder.AppendLine();
                        }

                        this.ColumnDefinition(column, model, builder);

                        if (i != tagsOpertions.Count - 1)
                        {
                            builder.AppendLine(",");
                        }
                    }

                    builder.AppendLine();
                }

                builder.Append(")");
            }



        }

        /// <summary>
        ///     Generates a SQL fragment for a column definition for the given column metadata.
        /// </summary>
        /// <param name="schema"> The schema that contains the table, or <c>null</c> to use the default schema. </param>
        /// <param name="table"> The table that contains the column. </param>
        /// <param name="name"> The column name. </param>
        /// <param name="operation"> The column metadata. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to add the SQL fragment. </param>
        protected override void ColumnDefinition(
            string schema,
            string table,
            string name,
            ColumnOperation operation,
            IModel model,
            MigrationCommandListBuilder builder)
        {
            base.ColumnDefinition(schema, table, name, operation, model, builder);

            //var inlinePk = operation[TaosAnnotationNames.InlinePrimaryKey] as bool?;
            //if (inlinePk == true)
            //{
            //    var inlinePkName = operation[
            //        TaosAnnotationNames.InlinePrimaryKeyName] as string;
            //    if (!string.IsNullOrEmpty(inlinePkName))
            //    {
            //        builder
            //            .Append(" CONSTRAINT ")
            //            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(inlinePkName));
            //    }

            //    builder.Append(" PRIMARY KEY");
            //    var autoincrement = operation[TaosAnnotationNames.Autoincrement] as bool?
            //        // NB: Migrations scaffolded with version 1.0.0 don't have the prefix. See #6461
            //        ?? operation[TaosAnnotationNames.LegacyAutoincrement] as bool?;
            //    if (autoincrement == true)
            //    {
            //        builder.Append(" AUTOINCREMENT");
            //    }
            //}
        }

        #region Invalid migration operations

        /// <summary>
        ///     Throws <see cref="NotSupportedException" /> since this operation requires table rebuilds, which
        ///     are not yet supported.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        /// <param name="terminate"> Indicates whether or not to terminate the command after generating SQL for the operation. </param>
        protected override void Generate(
            AddForeignKeyOperation operation, IModel model, MigrationCommandListBuilder builder, bool terminate = true)
            => throw new NotSupportedException(
                TaosStrings.InvalidMigrationOperation(operation.GetType().ShortDisplayName()));

        /// <summary>
        ///     Throws <see cref="NotSupportedException" /> since this operation requires table rebuilds, which
        ///     are not yet supported.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        /// <param name="terminate"> Indicates whether or not to terminate the command after generating SQL for the operation. </param>
        protected override void Generate(
            AddPrimaryKeyOperation operation, IModel model, MigrationCommandListBuilder builder, bool terminate = true)
            => throw new NotSupportedException(
                TaosStrings.InvalidMigrationOperation(operation.GetType().ShortDisplayName()));

        /// <summary>
        ///     Throws <see cref="NotSupportedException" /> since this operation requires table rebuilds, which
        ///     are not yet supported.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        protected override void Generate(AddUniqueConstraintOperation operation, IModel model, MigrationCommandListBuilder builder)
            => throw new NotSupportedException(
                TaosStrings.InvalidMigrationOperation(operation.GetType().ShortDisplayName()));



        /// <summary>
        ///     Throws <see cref="NotSupportedException" /> since this operation requires table rebuilds, which
        ///     are not yet supported.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        /// <param name="terminate"> Indicates whether or not to terminate the command after generating SQL for the operation. </param>
        protected override void Generate(
            DropColumnOperation operation, IModel model, MigrationCommandListBuilder builder, bool terminate = true)
            => throw new NotSupportedException(
                TaosStrings.InvalidMigrationOperation(operation.GetType().ShortDisplayName()));

        /// <summary>
        ///     Throws <see cref="NotSupportedException" /> since this operation requires table rebuilds, which
        ///     are not yet supported.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        /// <param name="terminate"> Indicates whether or not to terminate the command after generating SQL for the operation. </param>
        protected override void Generate(
            DropForeignKeyOperation operation, IModel model, MigrationCommandListBuilder builder, bool terminate = true)
            => throw new NotSupportedException(
                TaosStrings.InvalidMigrationOperation(operation.GetType().ShortDisplayName()));

        /// <summary>
        ///     Throws <see cref="NotSupportedException" /> since this operation requires table rebuilds, which
        ///     are not yet supported.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        /// <param name="terminate"> Indicates whether or not to terminate the command after generating SQL for the operation. </param>
        protected override void Generate(
            DropPrimaryKeyOperation operation, IModel model, MigrationCommandListBuilder builder, bool terminate = true)
            => throw new NotSupportedException(
                TaosStrings.InvalidMigrationOperation(operation.GetType().ShortDisplayName()));

        /// <summary>
        ///     Throws <see cref="NotSupportedException" /> since this operation requires table rebuilds, which
        ///     are not yet supported.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        protected override void Generate(DropUniqueConstraintOperation operation, IModel model, MigrationCommandListBuilder builder)
            => throw new NotSupportedException(
                TaosStrings.InvalidMigrationOperation(operation.GetType().ShortDisplayName()));

        /// <summary>
        ///     Throws <see cref="NotSupportedException" /> since this operation requires table rebuilds, which
        ///     are not yet supported.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        protected override void Generate(DropCheckConstraintOperation operation, IModel model, MigrationCommandListBuilder builder)
            => throw new NotSupportedException(
                TaosStrings.InvalidMigrationOperation(operation.GetType().ShortDisplayName()));

        /// <summary>
        ///     Throws <see cref="NotSupportedException" /> since this operation requires table rebuilds, which
        ///     are not yet supported.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        protected override void Generate(AlterColumnOperation operation, IModel model, MigrationCommandListBuilder builder)
            => throw new NotSupportedException(
                TaosStrings.InvalidMigrationOperation(operation.GetType().ShortDisplayName()));

        /// <summary>
        ///     Generates a SQL fragment for a computed column definition for the given column metadata.
        /// </summary>
        /// <param name="schema"> The schema that contains the table, or <c>null</c> to use the default schema. </param>
        /// <param name="table"> The table that contains the column. </param>
        /// <param name="name"> The column name. </param>
        /// <param name="operation"> The column metadata. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to add the SQL fragment. </param>
        protected override void ComputedColumnDefinition(
            string schema,
            string table,
            string name,
            ColumnOperation operation,
            IModel model,
            MigrationCommandListBuilder builder)
            => throw new NotSupportedException(TaosStrings.ComputedColumnsNotSupported);

        #endregion

        #region Ignored schema operations

        /// <summary>
        ///     Ignored, since schemas are not supported by Taos and are silently ignored to improve testing compatibility.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        protected override void Generate(EnsureSchemaOperation operation, IModel model, MigrationCommandListBuilder builder)
        {
        }

        /// <summary>
        ///     Ignored, since schemas are not supported by Taos and are silently ignored to improve testing compatibility.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        protected override void Generate(DropSchemaOperation operation, IModel model, MigrationCommandListBuilder builder)
        {
        }

        #endregion

        #region Sequences not supported

        /// <summary>
        ///     Throws <see cref="NotSupportedException" /> since Taos does not support sequences.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        protected override void Generate(RestartSequenceOperation operation, IModel model, MigrationCommandListBuilder builder)
            => throw new NotSupportedException(TaosStrings.SequencesNotSupported);

        /// <summary>
        ///     Throws <see cref="NotSupportedException" /> since Taos does not support sequences.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        protected override void Generate(CreateSequenceOperation operation, IModel model, MigrationCommandListBuilder builder)
            => throw new NotSupportedException(TaosStrings.SequencesNotSupported);

        /// <summary>
        ///     Throws <see cref="NotSupportedException" /> since Taos does not support sequences.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        protected override void Generate(RenameSequenceOperation operation, IModel model, MigrationCommandListBuilder builder)
            => throw new NotSupportedException(TaosStrings.SequencesNotSupported);

        /// <summary>
        ///     Throws <see cref="NotSupportedException" /> since Taos does not support sequences.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        protected override void Generate(AlterSequenceOperation operation, IModel model, MigrationCommandListBuilder builder)
            => throw new NotSupportedException(TaosStrings.SequencesNotSupported);

        /// <summary>
        ///     Throws <see cref="NotSupportedException" /> since Taos does not support sequences.
        /// </summary>
        /// <param name="operation"> The operation. </param>
        /// <param name="model"> The target model which may be <c>null</c> if the operations exist without a model. </param>
        /// <param name="builder"> The command builder to use to build the commands. </param>
        protected override void Generate(DropSequenceOperation operation, IModel model, MigrationCommandListBuilder builder)
            => throw new NotSupportedException(TaosStrings.SequencesNotSupported);

        #endregion

    }
}
