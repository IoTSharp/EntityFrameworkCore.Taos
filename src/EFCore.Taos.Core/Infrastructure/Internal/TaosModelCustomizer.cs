using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;

namespace IoTSharp.EntityFrameworkCore.Taos.Infrastructure.Internal
{
    public class TaosModelCustomizer : RelationalModelCustomizer
    {
        public TaosModelCustomizer(ModelCustomizerDependencies dependencies) : base(dependencies)
        {
        }

        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            var taostabs = context.GetType().GetProperties().Where(w =>
            {
                var isDbSet = w.PropertyType.IsGenericType && w.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>);
                if (isDbSet)
                {
                    isDbSet &= w.PropertyType.GenericTypeArguments.All(a => a.GetCustomAttribute<TaosAttribute>() != null);
                }


                return isDbSet;
            }).Select(s => s.PropertyType).ToList();
            foreach (var tab in taostabs)
            {
                foreach (var arg in tab.GenericTypeArguments)
                {
                    modelBuilder.Entity(arg);
                }
            }
            base.Customize(modelBuilder, context);

        }
    }
}
