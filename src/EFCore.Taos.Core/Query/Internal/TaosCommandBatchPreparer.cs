using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.EntityFrameworkCore.Update.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace IoTSharp.EntityFrameworkCore.Taos.Query.Internal
{
    public class TaosCommandBatchPreparer : CommandBatchPreparer //: ICommandBatchPreparer
    {
        private int _minBatchSize;

        public TaosCommandBatchPreparer(CommandBatchPreparerDependencies dependencies) : base(dependencies)
        {
            _minBatchSize =
                dependencies.Options.Extensions.OfType<RelationalOptionsExtension>().FirstOrDefault()?.MinBatchSize
                ?? 1;
        }
        public override IEnumerable<ModificationCommandBatch> BatchCommands(IList<IUpdateEntry> entries, IUpdateAdapter updateAdapter)
        {
            var parameterNameGenerator = Dependencies.ParameterNameGeneratorFactory.Create();
            var commands = CreateModificationCommands(entries, updateAdapter, parameterNameGenerator.GenerateNext);
            var commandSets = TopologicalSort(commands);

            for (var commandSetIndex = 0; commandSetIndex < commandSets.Count; commandSetIndex++)
            {
                var batches = CreateCommandBatches(
                    commandSets[commandSetIndex],
                    commandSetIndex < commandSets.Count - 1,
                    assertColumnModification: true,
                    parameterNameGenerator);

                foreach (var batch in batches)
                {
                    yield return batch;
                }
            }
            //return base.BatchCommands(entries, updateAdapter);
        }

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        private IEnumerable<ModificationCommandBatch> CreateCommandBatches(
            IEnumerable<IReadOnlyModificationCommand> commandSet,
            bool moreCommandSets,
            bool assertColumnModification,
            ParameterNameGenerator? parameterNameGenerator = null)
        {
            var batch = Dependencies.ModificationCommandBatchFactory.Create();

            foreach (var modificationCommand in commandSet)
            {
#if DEBUG
                if (assertColumnModification)
                {
                    (modificationCommand as ModificationCommand)?.AssertColumnsNotInitialized();
                }
#endif

                if (modificationCommand.EntityState == EntityState.Modified
                    && !modificationCommand.ColumnModifications.Any(m => m.IsWrite))
                {
                    continue;
                }

                if (!batch.TryAddCommand(modificationCommand))
                {
                    if (batch.ModificationCommands.Count == 1
                        || batch.ModificationCommands.Count >= _minBatchSize)
                    {
                        if (batch.ModificationCommands.Count > 1)
                        {
                            Dependencies.UpdateLogger.BatchReadyForExecution(
                                batch.ModificationCommands.SelectMany(c => c.Entries), batch.ModificationCommands.Count);
                        }

                        batch.Complete(moreBatchesExpected: true);

                        yield return batch;
                    }
                    else
                    {
                        Dependencies.UpdateLogger.BatchSmallerThanMinBatchSize(
                            batch.ModificationCommands.SelectMany(c => c.Entries), batch.ModificationCommands.Count, _minBatchSize);

                        foreach (var command in batch.ModificationCommands)
                        {
                            batch = StartNewBatch(parameterNameGenerator, command);
                            batch.Complete(moreBatchesExpected: true);

                            yield return batch;
                        }
                    }

                    batch = StartNewBatch(parameterNameGenerator, modificationCommand);
                }
            }

            if (batch.ModificationCommands.Count == 1
                || batch.ModificationCommands.Count >= _minBatchSize)
            {
                if (batch.ModificationCommands.Count > 1)
                {
                    Dependencies.UpdateLogger.BatchReadyForExecution(
                        batch.ModificationCommands.SelectMany(c => c.Entries), batch.ModificationCommands.Count);
                }

                batch.Complete(moreBatchesExpected: moreCommandSets);

                yield return batch;
            }
            else
            {
                Dependencies.UpdateLogger.BatchSmallerThanMinBatchSize(
                    batch.ModificationCommands.SelectMany(c => c.Entries), batch.ModificationCommands.Count, _minBatchSize);

                for (var commandIndex = 0; commandIndex < batch.ModificationCommands.Count; commandIndex++)
                {
                    var singleCommandBatch = StartNewBatch(parameterNameGenerator, batch.ModificationCommands[commandIndex]);
                    singleCommandBatch.Complete(
                        moreBatchesExpected: moreCommandSets || commandIndex < batch.ModificationCommands.Count - 1);

                    yield return singleCommandBatch;
                }
            }

            ModificationCommandBatch StartNewBatch(
                ParameterNameGenerator? parameterNameGenerator,
                IReadOnlyModificationCommand modificationCommand)
            {
                parameterNameGenerator?.Reset();
                var batch = Dependencies.ModificationCommandBatchFactory.Create();
                batch.TryAddCommand(modificationCommand);
                return batch;
            }
        }


        public override IEnumerable<ModificationCommandBatch> CreateCommandBatches(IEnumerable<IReadOnlyModificationCommand> commandSet, bool moreCommandSets)
        {
            return base.CreateCommandBatches(commandSet, moreCommandSets);
        }
        protected override IEnumerable<IReadOnlyModificationCommand> CreateModificationCommands(IList<IUpdateEntry> entries, IUpdateAdapter updateAdapter, Func<string> generateParameterName)
        {
            return base.CreateModificationCommands(entries, updateAdapter, generateParameterName);
        }
        public override IReadOnlyList<List<IReadOnlyModificationCommand>> TopologicalSort(IEnumerable<IReadOnlyModificationCommand> commands)
        {
            return base.TopologicalSort(commands);
        }
    }
}
