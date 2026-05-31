using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Apache.Calcite.EntityFrameworkCore.Diagnostics.Internal
{

    /// <inheritdoc />
    public class CalciteLoggingDefinitions : RelationalLoggingDefinitions
    {

        public EventDefinitionBase? LogUnexpectedConnectionType;

        public EventDefinitionBase? LogTransactionsNotSupported;

        public EventDefinitionBase? LogMigrationOperationIgnored;

        public EventDefinitionBase? LogMigrationTableFeatureIgnored;

    }

}
