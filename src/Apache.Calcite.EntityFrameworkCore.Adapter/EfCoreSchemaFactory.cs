using System;

using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;

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
    /// The <c>operand</c> map must contain either:
    /// <list type="bullet">
    ///   <item><c>"dbContextType"</c> - Assembly-qualified name of a <see cref="DbContext"/> subclass with a public parameterless constructor.</item>
    ///   <item><c>"dbContextFactory"</c> - Assembly-qualified name of an <see cref="IDbContextFactory"/> implementation with a parameterless constructor.</item>
    /// </list>
    /// <para>
    /// Optional configuration keys:
    /// <list type="bullet">
    ///   <item><c>"rexTranslatorFactory"</c> - Assembly-qualified name of an <see cref="IRexToLinqTranslatorFactory"/> implementation with a parameterless constructor.</item>
    /// </list>
    /// </para>
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
            var dbContextFactory = LoadDbContextFactory(operand);
            var translatorFactory = LoadRexTranslatorFactory(operand);

            // Pass no parent: Calcite's model handler registers the schema this method returns, so registering
            // it here as well would add it twice.
            return EfCoreSchema.Create(null, name, dbContextFactory, translatorFactory);
        }

        /// <summary>
        /// Loads a <see cref="IDbContextFactory"/> from the operand map.
        /// </summary>
        /// <param name="operand">The operand map containing configuration keys.</param>
        /// <returns>A context factory instance.</returns>
        static IDbContextFactory LoadDbContextFactory(Map operand)
        {
            if (operand == null)
                throw new ArgumentException("EfCoreSchemaFactory requires an operand map with 'dbContextFactory' or 'dbContextType'.");

            // Try loading a custom factory first
            var dbContextfactoryTypeName = operand.get("dbContextFactory") as string;
            if (!string.IsNullOrEmpty(dbContextfactoryTypeName))
            {
                var dbContextFactoryType = Type.GetType(dbContextfactoryTypeName, throwOnError: true)!;
                if (!typeof(IDbContextFactory).IsAssignableFrom(dbContextFactoryType))
                    throw new ArgumentException($"Type '{dbContextfactoryTypeName}' does not implement IDbContextFactory.");

                return (IDbContextFactory)Activator.CreateInstance(dbContextFactoryType)!;
            }

            // Fallback: load a context type directly and wrap it
            var dbContextTypeName = operand.get("dbContextType") as string;
            if (string.IsNullOrEmpty(dbContextTypeName))
                throw new ArgumentException("EfCoreSchemaFactory requires an operand 'dbContextFactory' or 'dbContextType'.");

            var dbContextType = Type.GetType(dbContextTypeName, throwOnError: true)!;
            if (!typeof(DbContext).IsAssignableFrom(dbContextType))
                throw new ArgumentException($"Type '{dbContextTypeName}' does not inherit from DbContext.");

            return new DelegateDbContextFactory(() => (DbContext)Activator.CreateInstance(dbContextType)!);
        }

        /// <summary>
        /// Loads a custom <see cref="IRexToLinqTranslatorFactory"/> from the operand map.
        /// </summary>
        /// <param name="operand">The operand map containing optional configuration keys.</param>
        /// <returns>A translator factory instance, or <see langword="null"/> to use the default translator.</returns>
        static IRexToLinqTranslatorFactory? LoadRexTranslatorFactory(Map operand)
        {
            if (operand == null)
                return null;

            var factoryTypeName = operand.get("rexTranslatorFactory") as string;
            if (string.IsNullOrEmpty(factoryTypeName))
                return null;

            var factoryType = Type.GetType(factoryTypeName, throwOnError: true)!;
            if (!typeof(IRexToLinqTranslatorFactory).IsAssignableFrom(factoryType))
                throw new ArgumentException($"Type '{factoryTypeName}' does not implement IRexToLinqTranslatorFactory.");

            return (IRexToLinqTranslatorFactory)Activator.CreateInstance(factoryType)!;
        }

    }

}
