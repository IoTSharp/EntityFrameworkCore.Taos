using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace IoTSharp.EntityFrameworkCore.Taos.Query.Internal
{
    internal class TaosQueryContext : RelationalQueryContext
    {
        private QueryContextDependencies dependencies;

        public TaosQueryContext(QueryContextDependencies dependencies, RelationalQueryContextDependencies relationalDependencies) : base(dependencies, relationalDependencies)
        {

        }
        public override void AddParameter(string name, object value)
        {
            base.AddParameter(name, value);
        }
        public override void InitializeStateManager(bool standAlone = false)
        {
            base.InitializeStateManager(standAlone);
        }
        public override void SetNavigationIsLoaded(object entity, INavigationBase navigation)
        {
            base.SetNavigationIsLoaded(entity, navigation);
        }
        public override InternalEntityEntry StartTracking(IEntityType entityType, object entity, ValueBuffer valueBuffer)
        {
            var entry = base.StartTracking(entityType, entity, valueBuffer);
            return entry;
        }
        public override InternalEntityEntry TryGetEntry(IKey key, object[] keyValues, bool throwOnNullKey, out bool hasNullKey)
        {
            var entry = base.TryGetEntry(key, keyValues, throwOnNullKey, out hasNullKey);
            return entry;
        }
    }
}