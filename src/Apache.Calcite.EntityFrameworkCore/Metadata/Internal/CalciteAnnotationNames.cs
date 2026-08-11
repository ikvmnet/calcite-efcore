namespace Apache.Calcite.EntityFrameworkCore.Metadata.Internal
{

    /// <summary>
    /// Constant annotation names used by the Calcite Entity Framework Core provider when storing
    /// provider-specific metadata on models, entity types, and properties.
    /// </summary>
    public static class CalciteAnnotationNames
    {

        /// <summary>
        /// The common prefix applied to all Calcite-specific annotation names.
        /// </summary>
        public const string Prefix = "calcite:";

        /// <summary>
        /// The annotation that stores the configured <see cref="Apache.Calcite.EntityFrameworkCore.Metadata.CalciteValueGenerationStrategy"/>.
        /// </summary>
        public const string ValueGenerationStrategy = Prefix + nameof(ValueGenerationStrategy);

        /// <summary>
        /// Represents the configuration key for entity sequence settings of the model.
        /// </summary>
        /// <summary>
        /// The name of the sequence entity to use as a source of value generation.
        /// </summary>
        /// <summary>
        /// The CLR type name of the entity that backs default per-entity HiLo sequences for the model.
        /// </summary>
        /// <summary>
        /// The name of the property on the default sequence backing entity that identifies a sequence row.
        /// </summary>
        /// <summary>
        /// The name of the property on the default sequence backing entity that holds the sequence value.
        /// </summary>
    }

}
