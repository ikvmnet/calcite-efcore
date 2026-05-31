using System;

using Apache.Calcite.EntityFrameworkCore.Properties;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Apache.Calcite.EntityFrameworkCore.Diagnostics.Internal
{

    public static class CalciteLoggerExtensions
    {

        public static void UnexpectedConnectionTypeWarning(this IDiagnosticsLogger<DbLoggerCategory.Infrastructure> diagnostics, Type connectionType)
        {
            var definition = CalciteResources.LogUnexpectedConnectionType(diagnostics);

            if (diagnostics.ShouldLog(definition))
            {
                definition.Log(diagnostics, connectionType.ShortDisplayName());
            }

            if (diagnostics.NeedsEventData(definition, out var diagnosticSourceEnabled, out var simpleLogEnabled))
            {
                var eventData = new UnexpectedConnectionTypeEventData(definition, UnexpectedConnectionTypeWarning, connectionType);
                diagnostics.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
            }
        }

        static string UnexpectedConnectionTypeWarning(EventDefinitionBase definition, EventData payload)
        {
            var d = (EventDefinition<string>)definition;
            var p = (UnexpectedConnectionTypeEventData)payload;
            return d.GenerateMessage(p.ConnectionType.ShortDisplayName());
        }

        public static void TransactionIgnoredWarning(this IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> diagnostics)
        {
            var definition = CalciteResources.LogTransactionsNotSupported(diagnostics);

            if (diagnostics.ShouldLog(definition))
                definition.Log(diagnostics);

            if (diagnostics.NeedsEventData(definition, out var diagnosticSourceEnabled, out var simpleLogEnabled))
            {
                var eventData = new EventData(definition, (d, _) => ((EventDefinition)d).GenerateMessage());
                diagnostics.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
            }
        }

        public static void MigrationTableFeatureIgnoredWarning(this IDiagnosticsLogger<DbLoggerCategory.Migrations> diagnostics, string featureName, string tableName)
        {
            var definition = CalciteResources.LogMigrationTableFeatureIgnored(diagnostics);

            if (diagnostics.ShouldLog(definition))
                definition.Log(diagnostics, featureName, tableName);

            if (diagnostics.NeedsEventData(definition, out var diagnosticSourceEnabled, out var simpleLogEnabled))
            {
                var eventData = new EventData(definition, (d, _) => ((EventDefinition<string, string>)d).GenerateMessage(featureName, tableName));
                diagnostics.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
            }
        }

        public static void MigrationOperationIgnoredWarning(this IDiagnosticsLogger<DbLoggerCategory.Migrations> diagnostics, string operationName)
        {
            var definition = CalciteResources.LogMigrationOperationIgnored(diagnostics);

            if (diagnostics.ShouldLog(definition))
                definition.Log(diagnostics, operationName);

            if (diagnostics.NeedsEventData(definition, out var diagnosticSourceEnabled, out var simpleLogEnabled))
            {
                var eventData = new EventData(definition, (d, _) => ((EventDefinition<string>)d).GenerateMessage(operationName));
                diagnostics.DispatchEventData(definition, eventData, diagnosticSourceEnabled, simpleLogEnabled);
            }
        }

    }

}
