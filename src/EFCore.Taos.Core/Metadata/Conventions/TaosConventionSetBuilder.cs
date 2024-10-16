// Copyright (c)  Maikebing. All rights reserved.
// Licensed under the MIT License, See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

using JetBrains.Annotations;

using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore.Metadata.Conventions
{
    /// <summary>
    ///     <para>
    ///         A builder for building conventions for Taos.
    ///     </para>
    ///     <para>
    ///         The service lifetime is <see cref="ServiceLifetime.Scoped" /> and multiple registrations
    ///         are allowed. This means that each <see cref="DbContext" /> instance will use its own
    ///         set of instances of this service.
    ///         The implementations may depend on other services registered with any lifetime.
    ///         The implementations do not need to be thread-safe.
    ///     </para>
    /// </summary>
    public class TaosConventionSetBuilder : RelationalConventionSetBuilder
    {
        /// <summary>
        ///     Creates a new <see cref="TaosConventionSetBuilder" /> instance.
        /// </summary>
        /// <param name="dependencies"> The core dependencies for this service. </param>
        /// <param name="relationalDependencies"> The relational dependencies for this service. </param>
        public TaosConventionSetBuilder(
            [NotNull] ProviderConventionSetBuilderDependencies dependencies,
            [NotNull] RelationalConventionSetBuilderDependencies relationalDependencies)
            : base(dependencies, relationalDependencies)
        {

        }
        public override ConventionSet CreateConventionSet()
        {
            //var conventionSet = base.CreateConventionSet();// new ConventionSet();
            var conventionSet = new ConventionSet();

            //conventionSet.Add(new RelationalColumnAttributeConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new RelationalColumnCommentAttributeConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new RelationalTableAttributeConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new RelationalTableCommentAttributeConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new RelationalDbFunctionAttributeConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new RelationalPropertyJsonPropertyNameAttributeConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new RelationalNavigationJsonPropertyNameAttributeConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new TableSharingConcurrencyTokenConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new TableNameFromDbSetConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new PropertyOverridesConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new CheckConstraintConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new StoredProcedureConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new TableValuedDbFunctionConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new StoreGenerationConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new EntitySplittingConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new EntityTypeHierarchyMappingConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new SequenceUniquificationConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new SharedTableConvention(Dependencies, RelationalDependencies));
            //conventionSet.Add(new RelationalMapToJsonConvention(Dependencies, RelationalDependencies));

            //conventionSet.Replace<ValueGenerationConvention>(
            //    new RelationalValueGenerationConvention(Dependencies, RelationalDependencies));
            //conventionSet.Replace<QueryFilterRewritingConvention>(
            //    new RelationalQueryFilterRewritingConvention(Dependencies, RelationalDependencies));
            //conventionSet.Replace<RuntimeModelConvention>(new RelationalRuntimeModelConvention(Dependencies, RelationalDependencies));


            conventionSet.Add(new TaosAttributeConvention(Dependencies));
            conventionSet.Add(new TaosColumnAttributePropertyAttributeConvention(Dependencies));

            return conventionSet;
        }
    }
}
