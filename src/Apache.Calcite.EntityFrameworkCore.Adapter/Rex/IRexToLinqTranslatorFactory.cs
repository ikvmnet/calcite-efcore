namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rex
{

    /// <summary>
    /// Factory for creating <see cref="IRexToLinqTranslator"/> instances.
    /// </summary>
    /// <remarks>
    /// Implement this interface to provide custom translator creation logic.
    /// The factory is instantiated once and can be used to create translators with
    /// custom configuration, pooling, or caching strategies.
    /// </remarks>
    public interface IRexToLinqTranslatorFactory
    {

        /// <summary>
        /// Creates a new <see cref="IRexToLinqTranslator"/> instance.
        /// </summary>
        /// <returns>A configured translator instance.</returns>
        IRexToLinqTranslator Create();

    }

}
