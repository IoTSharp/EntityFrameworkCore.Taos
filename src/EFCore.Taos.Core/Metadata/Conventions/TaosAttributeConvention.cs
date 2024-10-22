// Copyright (c)  Maikebing. All rights reserved.
// Licensed under the MIT License, See License.txt in the project root for license information.

using System.Reflection;

using IoTSharp.EntityFrameworkCore.Taos;

using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore.Metadata.Conventions
{
    public class TaosAttributeConvention : Microsoft.EntityFrameworkCore.Metadata.Conventions.TypeAttributeConventionBase<TaosAttribute>
    {
        public TaosAttributeConvention(ProviderConventionSetBuilderDependencies dependencies) : base(dependencies)
        {
        }

        protected override void ProcessEntityTypeAdded(IConventionEntityTypeBuilder entityTypeBuilder, TaosAttribute attribute, IConventionContext<IConventionEntityTypeBuilder> context)
        {
            var tableName = attribute.TableName;
            if (string.IsNullOrWhiteSpace(tableName))
            {
                var type = entityTypeBuilder.Metadata.ClrType;
                tableName = type.Name;
            }

            entityTypeBuilder.ToTable(tableName, fromDataAnnotation: true);
        }
        public override void ProcessEntityTypeAdded(IConventionEntityTypeBuilder entityTypeBuilder, IConventionContext<IConventionEntityTypeBuilder> context)
        {
            var type = entityTypeBuilder.Metadata.ClrType;

            var attributes = type.GetCustomAttributes<TaosAttribute>(true);
            foreach (var attribute in attributes)
            {
                ProcessEntityTypeAdded(entityTypeBuilder, attribute, context);
            }
        }
    }
}
