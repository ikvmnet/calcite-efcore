using System;

using java.util;

using Microsoft.EntityFrameworkCore;

using org.apache.calcite.schema;

namespace Apache.Calcite.EntityFrameworkCore.Adapter
{

    /// <summary>
    /// <see cref="SchemaFactory"/> implementation for <see cref="EfCoreSchema"/>.
    /// </summary>
    /// <remarks>
    /// This factory is used when registering an EF Core schema via a Calcite model JSON file.
    /// The <c>operand</c> map must contain a key <c>"contextType"</c> whose value is the
    /// assembly-qualified name of a <see cref="DbContext"/> subclass that has a public
    /// parameterless constructor.
    /// </remarks>
    public class EfCoreSchemaFactory : SchemaFactory
    {

        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static readonly EfCoreSchemaFactory Instance = new();

        private EfCoreSchemaFactory() { }

        /// <inheritdoc />
        public Schema create(SchemaPlus parentSchema, string name, Map operand)
        {
            var contextTypeName = operand?.get("contextType") as string
                ?? throw new ArgumentException("EfCoreSchemaFactory requires an operand 'contextType' with the assembly-qualified name of the DbContext subclass.");

            var contextType = Type.GetType(contextTypeName, throwOnError: true)!;

            if (!typeof(DbContext).IsAssignableFrom(contextType))
                throw new ArgumentException(
                    $"Type '{contextTypeName}' does not inherit from DbContext.");

            Func<DbContext> factory = () => (DbContext)Activator.CreateInstance(contextType)!;

            return EfCoreSchema.Create(parentSchema, name, factory);
        }

    }

}
