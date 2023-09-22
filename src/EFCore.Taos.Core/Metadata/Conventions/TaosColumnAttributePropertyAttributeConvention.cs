// Copyright (c)  Maikebing. All rights reserved.
// Licensed under the MIT License, See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;

using IoTSharp.EntityFrameworkCore.Taos;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore.Metadata.Conventions
{
    /// <summary>
    /// IPropertyAddedConvention, IConvention, IPropertyFieldChangedConvention
    /// </summary>
    public class TaosColumnAttributePropertyAttributeConvention : PropertyAttributeConventionBase<TaosColumnAttribute>, IModelFinalizingConvention, IEntityTypeAddedConvention, IEntityTypeBaseTypeChangedConvention
    {
        public TaosColumnAttributePropertyAttributeConvention(ProviderConventionSetBuilderDependencies dependencies) : base(dependencies)
        {
        }

        public void ProcessEntityTypeAdded(IConventionEntityTypeBuilder entityTypeBuilder, IConventionContext<IConventionEntityTypeBuilder> context)
        {

            var entityType = entityTypeBuilder.Metadata;
            var members = entityType.GetRuntimeProperties().Values.Cast<MemberInfo>()
                //.Concat(entityType.GetRuntimeFields().Values)
                .Select(s =>
                {
                    var pmeta = entityType.FindProperty(s);
                    var attr = s.GetCustomAttribute<TaosColumnAttribute>();
                    if (pmeta == null && (attr != null && !attr.IsTableName))
                    {
                        pmeta = entityType.AddProperty(s);
                    }

                    return (Menber: s, Attr: attr, Meta: pmeta);
                });
            foreach (var m in members)
            {
                if (m.Attr.IsTableName)
                {
                    entityTypeBuilder.Ignore(m.Menber.GetSimpleMemberName(), fromDataAnnotation: true);
                }

            }
            var keyMembers = members.Where(w => w.Attr != null && w.Attr.ColumnType == TaosDataType.TIMESTAMP || w.Attr.IsTag);
            if (keyMembers != null && keyMembers.Count() > 0)
            {
                var keyProps = keyMembers.Select(s =>
                {
                    var name = string.IsNullOrWhiteSpace(s.Attr.ColumnName) ? s.Menber.Name : s.Attr.ColumnName;
                    return entityTypeBuilder.Metadata.FindProperty(name);
                }).Where(w => w != null).ToList();

                if (keyProps.Count > 0)
                {
                    entityTypeBuilder.PrimaryKey(keyProps, true);
                }
            }


        }


        public void ProcessEntityTypeBaseTypeChanged(IConventionEntityTypeBuilder entityTypeBuilder, IConventionEntityType newBaseType, IConventionEntityType oldBaseType, IConventionContext<IConventionEntityType> context)
        {
            if (oldBaseType == null)
            {
                return;
            }

        }

        protected override void ProcessPropertyAdded(IConventionPropertyBuilder propertyBuilder, TaosColumnAttribute attribute, MemberInfo clrMember, IConventionContext context)
        {

            var property = propertyBuilder.Metadata;
            //var member = property.GetIdentifyingMemberInfo();



            if (!string.IsNullOrWhiteSpace(attribute?.ColumnName))
            {
                if (attribute?.ColumnName == "value")
                {
                    throw new Exception("column name value not allow");
                }
                propertyBuilder.HasColumnName(attribute.ColumnName, true);
            }
            else if (property.Name == "value")
            {
                throw new Exception("column name value not allow");
            }

            propertyBuilder.HasColumnType(attribute.ColumnType.ToString(), true);
            if (attribute.ColumnLength > 0)
            {
                propertyBuilder.HasMaxLength(attribute.ColumnLength, true);
            }
            //switch (attribute.ColumnType)
            //{
            //    case TaosDataType.BINARY:
            //    case TaosDataType.NCHAR:
            //    case TaosDataType.VARCHAR:
            //    case TaosDataType.GEOMETRY:
            //    case TaosDataType.VARBINARY:
            //        propertyBuilder.HasColumnType($"{attribute.ColumnType}({attribute.ColumnLength})", true);
            //        break;
            //    default:
            //        propertyBuilder.HasColumnType(attribute.ColumnType.ToString(), true);
            //        break;
            //}
            if (attribute.IsTag)
            {
                propertyBuilder.IsRequired(true, true);
            }

        }

        public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
            
            
        }


    }
}
