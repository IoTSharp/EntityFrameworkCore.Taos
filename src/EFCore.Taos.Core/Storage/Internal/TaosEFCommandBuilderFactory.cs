using Microsoft.EntityFrameworkCore.Storage;

namespace IoTSharp.EntityFrameworkCore.Taos.Storage.Internal
{
    public class TaosEFCommandBuilderFactory : RelationalCommandBuilderFactory
    {
        public TaosEFCommandBuilderFactory(RelationalCommandBuilderDependencies dependencies) : base(dependencies)
        {
        }
        public override IRelationalCommandBuilder Create()
        {
            return new TaosEFCommandBuilder(Dependencies);
        }
    }
}
