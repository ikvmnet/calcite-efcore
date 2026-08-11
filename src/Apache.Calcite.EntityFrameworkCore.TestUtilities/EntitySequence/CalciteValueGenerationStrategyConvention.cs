using System.Linq;

using Apache.Calcite.EntityFrameworkCore.Extensions;
using Apache.Calcite.EntityFrameworkCore.Metadata;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Apache.Calcite.EntityFrameworkCore.TestUtilities
{

    public class CalciteValueGenerationStrategyConvention : IModelInitializedConvention, IModelFinalizingConvention
    {

        /// <summary>
        /// The name used for the implicit default entity sequence when no explicit sequence is configured.
        /// </summary>
        public const string DefaultSequenceName = "Default";

        public CalciteValueGenerationStrategyConvention(ProviderConventionSetBuilderDependencies dependencies, RelationalConventionSetBuilderDependencies relationalDependencies)
        {
            Dependencies = dependencies;
            RelationalDependencies = relationalDependencies;
        }

        protected virtual ProviderConventionSetBuilderDependencies Dependencies { get; }

        protected virtual RelationalConventionSetBuilderDependencies RelationalDependencies { get; }

        /// <inheritdoc/>
        public virtual void ProcessModelInitialized(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
            // The model-level default strategy is None. Users opt in by calling UseHiLoEntitySequence(...)
            // or HasDefaultEntitySequenceEntity<...>(...).
        }

        /// <inheritdoc />
        public virtual void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
        {
            var model = modelBuilder.Metadata;
            var modelStrategy = model.GetValueGenerationStrategy();

            // Resolve the default sequence name for this model: either the explicitly-set model-level name
            // or the implicit built-in default. The actual sequence object is materialized lazily below if
            // any property ends up referencing it.
            var defaultSequenceName = model.GetEntitySequenceName() ?? DefaultSequenceName;

            // Track whether the implicit default sequence is actually referenced by any property; if not,
            // we will not add it to the model.
            var defaultSequenceNeeded = false;

            foreach (var entityType in model.GetEntityTypes())
            {
                // Skip the default sequence backing entity itself - it must not get HiLo applied to its keys.
                if (IsDefaultBackingEntity(model, entityType))
                    continue;

                foreach (var property in entityType.GetDeclaredProperties())
                {
                    var strategy = property.GetValueGenerationStrategy();
                    if (strategy != CalciteValueGenerationStrategy.EntitySequenceHiLo)
                        continue;

                    // If the property has an explicit sequence name and it exists, leave it alone.
                    var explicitName = property.GetEntitySequenceName();
                    if (explicitName != null)
                    {
                        if (CalciteEntitySequence.FindEntitySequence(model, explicitName) == null)
                            continue;
                    }
                    else if (modelStrategy == CalciteValueGenerationStrategy.EntitySequenceHiLo)
                    {
                        // Inherit the default sequence name from the model. The sequence object will be
                        // materialized below if anything actually references it.
                        property.SetEntitySequenceName(defaultSequenceName, fromDataAnnotation: false);
                        defaultSequenceNeeded = true;
                    }
                    else
                    {
                        // Strategy is HiLo on the property but there is no model-level default and no
                        // explicit sequence name; nothing to bind to.
                        continue;
                    }

                    property.Builder.HasValueGenerationStrategy(strategy);
                }
            }

            // Materialize the implicit default sequence only if at least one property ended up referencing it
            // and it has not already been configured by the user.
            if (defaultSequenceNeeded
                && CalciteEntitySequence.FindEntitySequence(model, defaultSequenceName) == null)
            {
                TryAddDefaultSequence((IMutableModel)model, defaultSequenceName);
            }
        }

        static bool IsDefaultBackingEntity(IReadOnlyModel model, IReadOnlyEntityType entityType)
        {
            var backingType = model.GetDefaultEntitySequenceEntityType() ?? typeof(CalciteSequence);
            return entityType.ClrType == backingType;
        }

        static void TryAddDefaultSequence(IMutableModel model, string sequenceName)
        {
            var backingType = model.GetDefaultEntitySequenceEntityType();
            var nameProperty = model.GetDefaultEntitySequenceNameProperty();
            var valueProperty = model.GetDefaultEntitySequenceValueProperty();

            // Fall back to the built-in default backing entity when no custom one was registered.
            if (backingType == null)
            {
                backingType = typeof(CalciteSequence);
                nameProperty = nameof(CalciteSequence.Name);
                valueProperty = nameof(CalciteSequence.NextValue);
            }

            if (nameProperty == null || valueProperty == null)
                return;

            var entityType = model.FindEntityType(backingType) ?? model.AddEntityType(backingType);
            if (entityType == null)
                return;

            var nameProp = entityType.FindProperty(nameProperty);
            var valueProp = entityType.FindProperty(valueProperty);
            if (nameProp == null || valueProp == null)
                return;

            var sequence = CalciteEntitySequence.AddEntitySequence(
                model,
                sequenceName,
                entityType,
                valueProp,
                ConfigurationSource.Convention);

            sequence.KeyValue = sequenceName;
        }

        bool IsStrategyNoneNeeded(IReadOnlyProperty property, StoreObjectIdentifier storeObject)
        {
            return false;
        }

    }

}