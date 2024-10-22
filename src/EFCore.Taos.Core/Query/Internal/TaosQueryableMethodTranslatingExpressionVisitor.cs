// Copyright (c)  Maikebing. All rights reserved.
// Licensed under the MIT License, See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using IoTSharp.EntityFrameworkCore.Taos.Internal;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Internal;
using System.Reflection;
using System.Net.NetworkInformation;

namespace IoTSharp.EntityFrameworkCore.Taos.Query.Internal
{
    public class TaosQueryableMethodTranslatingExpressionVisitor : RelationalQueryableMethodTranslatingExpressionVisitor
    {
        private bool _subquery = false;
        public TaosQueryableMethodTranslatingExpressionVisitor(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
            QueryCompilationContext queryCompilationContext)
            : base(dependencies, relationalDependencies, queryCompilationContext)
        {
        }

        protected TaosQueryableMethodTranslatingExpressionVisitor(
            TaosQueryableMethodTranslatingExpressionVisitor parentVisitor)
            : base(parentVisitor)
        {
        }

        protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
            => new TaosQueryableMethodTranslatingExpressionVisitor(this);

        protected override ShapedQueryExpression TranslateOrderBy(
            ShapedQueryExpression source, LambdaExpression keySelector, bool ascending)
        {
            var translation = base.TranslateOrderBy(source, keySelector, ascending);
            if (translation == null)
            {
                return null;
            }

            var orderingExpression = ((SelectExpression)translation.QueryExpression).Orderings.Last();
            var orderingExpressionType = GetProviderType(orderingExpression.Expression);
            if (orderingExpressionType == typeof(DateTimeOffset)
                || orderingExpressionType == typeof(decimal)
                || orderingExpressionType == typeof(TimeSpan)
                || orderingExpressionType == typeof(ulong))
            {
                throw new NotSupportedException(
                    TaosStrings.OrderByNotSupported(orderingExpressionType.ShortDisplayName()));
            }

            return translation;
        }

        protected override ShapedQueryExpression TranslateThenBy(ShapedQueryExpression source, LambdaExpression keySelector, bool ascending)
        {
            var translation = base.TranslateThenBy(source, keySelector, ascending);
            if (translation == null)
            {
                return null;
            }

            var orderingExpression = ((SelectExpression)translation.QueryExpression).Orderings.Last();
            var orderingExpressionType = GetProviderType(orderingExpression.Expression);
            if (orderingExpressionType == typeof(DateTimeOffset)
                || orderingExpressionType == typeof(decimal)
                || orderingExpressionType == typeof(TimeSpan)
                || orderingExpressionType == typeof(ulong))
            {
                throw new NotSupportedException(
                    TaosStrings.OrderByNotSupported(orderingExpressionType.ShortDisplayName()));
            }

            return translation;
        }

        internal static readonly MethodInfo LeftJoinMethodInfo = typeof(QueryableExtensions).GetTypeInfo().GetDeclaredMethods("LeftJoin").Single((MethodInfo mi) => mi.GetParameters().Length == 5);

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            ShapedQueryExpression CheckTranslated(ShapedQueryExpression? translated)
                => translated
                    ?? throw new InvalidOperationException(
                        TranslationErrorDetails == null
                            ? CoreStrings.TranslationFailed(methodCallExpression.Print())
                            : CoreStrings.TranslationFailedWithDetails(
                                methodCallExpression.Print(),
                                TranslationErrorDetails));

            var method = methodCallExpression.Method;
            if (method.DeclaringType == typeof(Queryable)
                || method.DeclaringType == typeof(QueryableExtensions))
            {
                var source = Visit(methodCallExpression.Arguments[0]);
                if (source is ShapedQueryExpression shapedQueryExpression)
                {
                    var genericMethod = method.IsGenericMethod ? method.GetGenericMethodDefinition() : null;
                    switch (method.Name)
                    {
                        case nameof(Queryable.All)
                            when genericMethod == QueryableMethods.All:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(TranslateAll(shapedQueryExpression, GetLambdaExpressionFromArgument(1)));

                        case nameof(Queryable.Any)
                            when genericMethod == QueryableMethods.AnyWithoutPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(TranslateAny(shapedQueryExpression, null));

                        case nameof(Queryable.Any)
                            when genericMethod == QueryableMethods.AnyWithPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(TranslateAny(shapedQueryExpression, GetLambdaExpressionFromArgument(1)));

                        case nameof(Queryable.AsQueryable)
                            when genericMethod == QueryableMethods.AsQueryable:
                            return source;

                        case nameof(Queryable.Average)
                            when QueryableMethods.IsAverageWithoutSelector(method):
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(TranslateAverage(shapedQueryExpression, null, methodCallExpression.Type));

                        case nameof(Queryable.Average)
                            when QueryableMethods.IsAverageWithSelector(method):
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(
                                TranslateAverage(shapedQueryExpression, GetLambdaExpressionFromArgument(1), methodCallExpression.Type));

                        case nameof(Queryable.Cast)
                            when genericMethod == QueryableMethods.Cast:
                            return CheckTranslated(TranslateCast(shapedQueryExpression, method.GetGenericArguments()[0]));

                        case nameof(Queryable.Concat)
                            when genericMethod == QueryableMethods.Concat:
                            {
                                var source2 = Visit(methodCallExpression.Arguments[1]);
                                if (source2 is ShapedQueryExpression innerShapedQueryExpression)
                                {
                                    return CheckTranslated(TranslateConcat(shapedQueryExpression, innerShapedQueryExpression));
                                }

                                break;
                            }

                        case nameof(Queryable.Contains)
                            when genericMethod == QueryableMethods.Contains:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(TranslateContains(shapedQueryExpression, methodCallExpression.Arguments[1]));

                        case nameof(Queryable.Count)
                            when genericMethod == QueryableMethods.CountWithoutPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.SingleOrDefault);
                            return CheckTranslated(TranslateCount(shapedQueryExpression, null));

                        case nameof(Queryable.Count)
                            when genericMethod == QueryableMethods.CountWithPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.SingleOrDefault);
                            return CheckTranslated(TranslateCount(shapedQueryExpression, GetLambdaExpressionFromArgument(1)));

                        case nameof(Queryable.DefaultIfEmpty)
                            when genericMethod == QueryableMethods.DefaultIfEmptyWithoutArgument:
                            return CheckTranslated(TranslateDefaultIfEmpty(shapedQueryExpression, null));

                        case nameof(Queryable.DefaultIfEmpty)
                            when genericMethod == QueryableMethods.DefaultIfEmptyWithArgument:
                            return CheckTranslated(TranslateDefaultIfEmpty(shapedQueryExpression, methodCallExpression.Arguments[1]));

                        case nameof(Queryable.Distinct)
                            when genericMethod == QueryableMethods.Distinct:
                            return CheckTranslated(TranslateDistinct(shapedQueryExpression));

                        case nameof(Queryable.ElementAt)
                            when genericMethod == QueryableMethods.ElementAt:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(
                                TranslateElementAtOrDefault(shapedQueryExpression, methodCallExpression.Arguments[1], false));

                        case nameof(Queryable.ElementAtOrDefault)
                            when genericMethod == QueryableMethods.ElementAtOrDefault:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.SingleOrDefault);
                            return CheckTranslated(
                                TranslateElementAtOrDefault(shapedQueryExpression, methodCallExpression.Arguments[1], true));

                        case nameof(Queryable.Except)
                            when genericMethod == QueryableMethods.Except:
                            {
                                var source2 = Visit(methodCallExpression.Arguments[1]);
                                if (source2 is ShapedQueryExpression innerShapedQueryExpression)
                                {
                                    return CheckTranslated(TranslateExcept(shapedQueryExpression, innerShapedQueryExpression));
                                }

                                break;
                            }

                        case nameof(Queryable.First)
                            when genericMethod == QueryableMethods.FirstWithoutPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(TranslateFirstOrDefault(shapedQueryExpression, null, methodCallExpression.Type, false));

                        case nameof(Queryable.First)
                            when genericMethod == QueryableMethods.FirstWithPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(
                                TranslateFirstOrDefault(
                                    shapedQueryExpression, GetLambdaExpressionFromArgument(1), methodCallExpression.Type, false));

                        case nameof(Queryable.FirstOrDefault)
                            when genericMethod == QueryableMethods.FirstOrDefaultWithoutPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.SingleOrDefault);
                            return CheckTranslated(TranslateFirstOrDefault(shapedQueryExpression, null, methodCallExpression.Type, true));

                        case nameof(Queryable.FirstOrDefault)
                            when genericMethod == QueryableMethods.FirstOrDefaultWithPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.SingleOrDefault);
                            return CheckTranslated(
                                TranslateFirstOrDefault(
                                    shapedQueryExpression, GetLambdaExpressionFromArgument(1), methodCallExpression.Type, true));

                        case nameof(Queryable.GroupBy)
                            when genericMethod == QueryableMethods.GroupByWithKeySelector:
                            return CheckTranslated(TranslateGroupBy(shapedQueryExpression, GetLambdaExpressionFromArgument(1), null, null));

                        case nameof(Queryable.GroupBy)
                            when genericMethod == QueryableMethods.GroupByWithKeyElementSelector:
                            return CheckTranslated(
                                TranslateGroupBy(
                                    shapedQueryExpression, GetLambdaExpressionFromArgument(1), GetLambdaExpressionFromArgument(2), null));

                        case nameof(Queryable.GroupBy)
                            when genericMethod == QueryableMethods.GroupByWithKeyElementResultSelector:
                            return CheckTranslated(
                                TranslateGroupBy(
                                    shapedQueryExpression, GetLambdaExpressionFromArgument(1), GetLambdaExpressionFromArgument(2),
                                    GetLambdaExpressionFromArgument(3)));

                        case nameof(Queryable.GroupBy)
                            when genericMethod == QueryableMethods.GroupByWithKeyResultSelector:
                            return CheckTranslated(
                                TranslateGroupBy(
                                    shapedQueryExpression, GetLambdaExpressionFromArgument(1), null, GetLambdaExpressionFromArgument(2)));

                        case nameof(Queryable.GroupJoin)
                            when genericMethod == QueryableMethods.GroupJoin:
                            {
                                if (Visit(methodCallExpression.Arguments[1]) is ShapedQueryExpression innerShapedQueryExpression)
                                {
                                    return CheckTranslated(
                                        TranslateGroupJoin(
                                            shapedQueryExpression,
                                            innerShapedQueryExpression,
                                            GetLambdaExpressionFromArgument(2),
                                            GetLambdaExpressionFromArgument(3),
                                            GetLambdaExpressionFromArgument(4)));
                                }

                                break;
                            }

                        case nameof(Queryable.Intersect)
                            when genericMethod == QueryableMethods.Intersect:
                            {
                                if (Visit(methodCallExpression.Arguments[1]) is ShapedQueryExpression innerShapedQueryExpression)
                                {
                                    return CheckTranslated(TranslateIntersect(shapedQueryExpression, innerShapedQueryExpression));
                                }

                                break;
                            }

                        case nameof(Queryable.Join)
                            when genericMethod == QueryableMethods.Join:
                            {
                                if (Visit(methodCallExpression.Arguments[1]) is ShapedQueryExpression innerShapedQueryExpression)
                                {
                                    return CheckTranslated(
                                        TranslateJoin(
                                            shapedQueryExpression, innerShapedQueryExpression, GetLambdaExpressionFromArgument(2),
                                            GetLambdaExpressionFromArgument(3), GetLambdaExpressionFromArgument(4)));
                                }

                                break;
                            }

                        case nameof(QueryableExtensions.LeftJoin)
                            when genericMethod == LeftJoinMethodInfo:
                            {
                                if (Visit(methodCallExpression.Arguments[1]) is ShapedQueryExpression innerShapedQueryExpression)
                                {
                                    return CheckTranslated(
                                        TranslateLeftJoin(
                                            shapedQueryExpression, innerShapedQueryExpression, GetLambdaExpressionFromArgument(2),
                                            GetLambdaExpressionFromArgument(3), GetLambdaExpressionFromArgument(4)));
                                }

                                break;
                            }

                        case nameof(Queryable.Last)
                            when genericMethod == QueryableMethods.LastWithoutPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(TranslateLastOrDefault(shapedQueryExpression, null, methodCallExpression.Type, false));

                        case nameof(Queryable.Last)
                            when genericMethod == QueryableMethods.LastWithPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(
                                TranslateLastOrDefault(
                                    shapedQueryExpression, GetLambdaExpressionFromArgument(1), methodCallExpression.Type, false));

                        case nameof(Queryable.LastOrDefault)
                            when genericMethod == QueryableMethods.LastOrDefaultWithoutPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.SingleOrDefault);
                            return CheckTranslated(TranslateLastOrDefault(shapedQueryExpression, null, methodCallExpression.Type, true));

                        case nameof(Queryable.LastOrDefault)
                            when genericMethod == QueryableMethods.LastOrDefaultWithPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.SingleOrDefault);
                            return CheckTranslated(
                                TranslateLastOrDefault(
                                    shapedQueryExpression, GetLambdaExpressionFromArgument(1), methodCallExpression.Type, true));

                        case nameof(Queryable.LongCount)
                            when genericMethod == QueryableMethods.LongCountWithoutPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(TranslateLongCount(shapedQueryExpression, null));

                        case nameof(Queryable.LongCount)
                            when genericMethod == QueryableMethods.LongCountWithPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(TranslateLongCount(shapedQueryExpression, GetLambdaExpressionFromArgument(1)));

                        case nameof(Queryable.Max)
                            when genericMethod == QueryableMethods.MaxWithoutSelector:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(TranslateMax(shapedQueryExpression, null, methodCallExpression.Type));

                        case nameof(Queryable.Max)
                            when genericMethod == QueryableMethods.MaxWithSelector:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(
                                TranslateMax(shapedQueryExpression, GetLambdaExpressionFromArgument(1), methodCallExpression.Type));

                        case nameof(Queryable.Min)
                            when genericMethod == QueryableMethods.MinWithoutSelector:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(TranslateMin(shapedQueryExpression, null, methodCallExpression.Type));

                        case nameof(Queryable.Min)
                            when genericMethod == QueryableMethods.MinWithSelector:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(
                                TranslateMin(shapedQueryExpression, GetLambdaExpressionFromArgument(1), methodCallExpression.Type));

                        case nameof(Queryable.OfType)
                            when genericMethod == QueryableMethods.OfType:
                            return CheckTranslated(TranslateOfType(shapedQueryExpression, method.GetGenericArguments()[0]));

                        case nameof(Queryable.OrderBy)
                            when genericMethod == QueryableMethods.OrderBy:
                            return CheckTranslated(TranslateOrderBy(shapedQueryExpression, GetLambdaExpressionFromArgument(1), true));

                        case nameof(Queryable.OrderByDescending)
                            when genericMethod == QueryableMethods.OrderByDescending:
                            return CheckTranslated(TranslateOrderBy(shapedQueryExpression, GetLambdaExpressionFromArgument(1), false));

                        case nameof(Queryable.Reverse)
                            when genericMethod == QueryableMethods.Reverse:
                            return CheckTranslated(TranslateReverse(shapedQueryExpression));

                        case nameof(Queryable.Select)
                            when genericMethod == QueryableMethods.Select:
                            return CheckTranslated(TranslateSelect(shapedQueryExpression, GetLambdaExpressionFromArgument(1)));

                        case nameof(Queryable.SelectMany)
                            when genericMethod == QueryableMethods.SelectManyWithoutCollectionSelector:
                            return CheckTranslated(TranslateSelectMany(shapedQueryExpression, GetLambdaExpressionFromArgument(1)));

                        case nameof(Queryable.SelectMany)
                            when genericMethod == QueryableMethods.SelectManyWithCollectionSelector:
                            return CheckTranslated(
                                TranslateSelectMany(
                                    shapedQueryExpression, GetLambdaExpressionFromArgument(1), GetLambdaExpressionFromArgument(2)));

                        case nameof(Queryable.Single)
                            when genericMethod == QueryableMethods.SingleWithoutPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(TranslateSingleOrDefault(shapedQueryExpression, null, methodCallExpression.Type, false));

                        case nameof(Queryable.Single)
                            when genericMethod == QueryableMethods.SingleWithPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(
                                TranslateSingleOrDefault(
                                    shapedQueryExpression, GetLambdaExpressionFromArgument(1), methodCallExpression.Type, false));

                        case nameof(Queryable.SingleOrDefault)
                            when genericMethod == QueryableMethods.SingleOrDefaultWithoutPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.SingleOrDefault);
                            return CheckTranslated(TranslateSingleOrDefault(shapedQueryExpression, null, methodCallExpression.Type, true));

                        case nameof(Queryable.SingleOrDefault)
                            when genericMethod == QueryableMethods.SingleOrDefaultWithPredicate:
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.SingleOrDefault);
                            return CheckTranslated(
                                TranslateSingleOrDefault(
                                    shapedQueryExpression, GetLambdaExpressionFromArgument(1), methodCallExpression.Type, true));

                        case nameof(Queryable.Skip)
                            when genericMethod == QueryableMethods.Skip:
                            return CheckTranslated(TranslateSkip(shapedQueryExpression, methodCallExpression.Arguments[1]));

                        case nameof(Queryable.SkipWhile)
                            when genericMethod == QueryableMethods.SkipWhile:
                            return CheckTranslated(TranslateSkipWhile(shapedQueryExpression, GetLambdaExpressionFromArgument(1)));

                        case nameof(Queryable.Sum)
                            when QueryableMethods.IsSumWithoutSelector(method):
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(TranslateSum(shapedQueryExpression, null, methodCallExpression.Type));

                        case nameof(Queryable.Sum)
                            when QueryableMethods.IsSumWithSelector(method):
                            shapedQueryExpression = shapedQueryExpression.UpdateResultCardinality(ResultCardinality.Single);
                            return CheckTranslated(
                                TranslateSum(shapedQueryExpression, GetLambdaExpressionFromArgument(1), methodCallExpression.Type));

                        case nameof(Queryable.Take)
                            when genericMethod == QueryableMethods.Take:
                            return CheckTranslated(TranslateTake(shapedQueryExpression, methodCallExpression.Arguments[1]));

                        case nameof(Queryable.TakeWhile)
                            when genericMethod == QueryableMethods.TakeWhile:
                            return CheckTranslated(TranslateTakeWhile(shapedQueryExpression, GetLambdaExpressionFromArgument(1)));

                        case nameof(Queryable.ThenBy)
                            when genericMethod == QueryableMethods.ThenBy:
                            return CheckTranslated(TranslateThenBy(shapedQueryExpression, GetLambdaExpressionFromArgument(1), true));

                        case nameof(Queryable.ThenByDescending)
                            when genericMethod == QueryableMethods.ThenByDescending:
                            return CheckTranslated(TranslateThenBy(shapedQueryExpression, GetLambdaExpressionFromArgument(1), false));

                        case nameof(Queryable.Union)
                            when genericMethod == QueryableMethods.Union:
                            {
                                if (Visit(methodCallExpression.Arguments[1]) is ShapedQueryExpression innerShapedQueryExpression)
                                {
                                    return CheckTranslated(TranslateUnion(shapedQueryExpression, innerShapedQueryExpression));
                                }

                                break;
                            }

                        case nameof(Queryable.Where)
                            when genericMethod == QueryableMethods.Where:
                            return CheckTranslated(TranslateWhere(shapedQueryExpression, GetLambdaExpressionFromArgument(1)));
                            LambdaExpression GetLambdaExpressionFromArgument(int argumentIndex)
                            {
                                var exp = methodCallExpression.Arguments[argumentIndex];
                                return (LambdaExpression)(exp is UnaryExpression unary && exp.NodeType == ExpressionType.Quote ? unary.Operand : exp);
                            }
                    }
                }
            }

            return _subquery
                ? QueryCompilationContext.NotTranslatedExpression
                : throw new InvalidOperationException(CoreStrings.TranslationFailed(methodCallExpression.Print()));
        }


        private static Type GetProviderType(SqlExpression expression)
            => (expression.TypeMapping?.Converter?.ProviderClrType
                ?? expression.TypeMapping?.ClrType
                ?? expression.Type).UnwrapNullableType();
    }
}
