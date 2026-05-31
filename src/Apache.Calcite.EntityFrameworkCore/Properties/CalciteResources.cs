using Apache.Calcite.EntityFrameworkCore.Diagnostics;
using Apache.Calcite.EntityFrameworkCore.Diagnostics.Internal;
using Apache.Calcite.EntityFrameworkCore.Internal;

using Apache.Calcite.Data;

using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Apache.Calcite.EntityFrameworkCore.Properties
{

    public static class CalciteResources
    {

        /// <summary>
        /// An ADO.NET Connection object other than <see cref="CalciteConnection"/> was passed.
        /// </summary>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static EventDefinition<string> LogUnexpectedConnectionType(IDiagnosticsLogger logger)
        {
            var definition = ((CalciteLoggingDefinitions)logger.Definitions).LogUnexpectedConnectionType;
            if (definition == null)
                definition = NonCapturingLazyInitializer.EnsureInitialized(
                    ref ((CalciteLoggingDefinitions)logger.Definitions).LogUnexpectedConnectionType,
                    logger,
                    static logger => new EventDefinition<string>(
                        logger.Options,
                        CalciteEventId.UnexpectedConnectionTypeWarning,
                        LogLevel.Warning,
                        "CalciteEventId.UnexpectedConnectionTypeWarning",
                        level => LoggerMessage.Define<string>(
                            level,
                            CalciteEventId.UnexpectedConnectionTypeWarning,
                            CalciteStrings.LogUnexpectedConnectionType)));

            return (EventDefinition<string>)definition;
        }

        /// <summary>
        /// Transactions are not supported by the Calcite store.
        /// </summary>
        public static EventDefinition LogTransactionsNotSupported(IDiagnosticsLogger logger)
        {
            var definition = ((CalciteLoggingDefinitions)logger.Definitions).LogTransactionsNotSupported;
            if (definition == null)
                definition = NonCapturingLazyInitializer.EnsureInitialized(
                    ref ((CalciteLoggingDefinitions)logger.Definitions).LogTransactionsNotSupported,
                    logger,
                    static logger => new EventDefinition(
                        logger.Options,
                        CalciteEventId.TransactionIgnoredWarning,
                        LogLevel.Warning,
                        "CalciteEventId.TransactionIgnoredWarning",
                        level => LoggerMessage.Define(
                            level,
                            CalciteEventId.TransactionIgnoredWarning,
                            CalciteStrings.LogTransactionsNotSupported)));

            return (EventDefinition)definition;
        }

        /// <summary>
        /// A table feature (e.g. primary key constraint, foreign key) is not supported by the Calcite provider.
        /// </summary>
        public static EventDefinition<string, string> LogMigrationTableFeatureIgnored(IDiagnosticsLogger logger)
        {
            var definition = ((CalciteLoggingDefinitions)logger.Definitions).LogMigrationTableFeatureIgnored;
            if (definition == null)
                definition = NonCapturingLazyInitializer.EnsureInitialized(
                    ref ((CalciteLoggingDefinitions)logger.Definitions).LogMigrationTableFeatureIgnored,
                    logger,
                    static logger => new EventDefinition<string, string>(
                        logger.Options,
                        CalciteEventId.MigrationTableFeatureIgnoredWarning,
                        LogLevel.Warning,
                        "CalciteEventId.MigrationTableFeatureIgnoredWarning",
                        level => LoggerMessage.Define<string, string>(
                            level,
                            CalciteEventId.MigrationTableFeatureIgnoredWarning,
                            CalciteStrings.LogMigrationTableFeatureIgnored)));

            return (EventDefinition<string, string>)definition;
        }

        /// <summary>
        /// A migration operation is not supported by the Calcite provider.
        /// </summary>
        public static EventDefinition<string> LogMigrationOperationIgnored(IDiagnosticsLogger logger)
        {
            var definition = ((CalciteLoggingDefinitions)logger.Definitions).LogMigrationOperationIgnored;
            if (definition == null)
                definition = NonCapturingLazyInitializer.EnsureInitialized(
                    ref ((CalciteLoggingDefinitions)logger.Definitions).LogMigrationOperationIgnored,
                    logger,
                    static logger => new EventDefinition<string>(
                        logger.Options,
                        CalciteEventId.MigrationOperationIgnoredWarning,
                        LogLevel.Warning,
                        "CalciteEventId.MigrationOperationIgnoredWarning",
                        level => LoggerMessage.Define<string>(
                            level,
                            CalciteEventId.MigrationOperationIgnoredWarning,
                            CalciteStrings.LogMigrationOperationIgnored)));

            return (EventDefinition<string>)definition;
        }

    }

}
