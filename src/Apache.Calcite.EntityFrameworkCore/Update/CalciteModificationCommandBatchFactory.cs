using System;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Update;

namespace Apache.Calcite.EntityFrameworkCore.Update
{

    class CalciteModificationCommandBatchFactory : IModificationCommandBatchFactory
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="options"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public CalciteModificationCommandBatchFactory(ModificationCommandBatchFactoryDependencies dependencies, IDbContextOptions options)
        {
            Dependencies = dependencies;
        }

        protected virtual ModificationCommandBatchFactoryDependencies Dependencies { get; }

        public virtual ModificationCommandBatch Create()
        {
            return new CalciteModificationCommandBatch(Dependencies);
        }

    }

}
