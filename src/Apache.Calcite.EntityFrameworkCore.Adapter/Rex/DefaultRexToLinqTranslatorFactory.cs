namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rex
{

    /// <summary>
    /// Default implementation of <see cref="IRexToLinqTranslatorFactory"/> that creates
    /// instances of <see cref="RexToLinqTranslator"/> with the default operator translation provider.
    /// </summary>
    public sealed class DefaultRexToLinqTranslatorFactory : IRexToLinqTranslatorFactory
    {

        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static readonly DefaultRexToLinqTranslatorFactory Instance = new();

        private DefaultRexToLinqTranslatorFactory() { }

        /// <inheritdoc />
        public IRexToLinqTranslator Create()
        {
            return RexToLinqTranslator.Default;
        }

    }

}
