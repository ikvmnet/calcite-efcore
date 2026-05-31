using System.Diagnostics;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Apache.Calcite.EntityFrameworkCore.Diagnostics
{

    /// <summary>
    /// Event IDs for Calcite events that correspond to messages logged to an <see cref="ILogger" /> and events sent to a <see cref="DiagnosticSource" />.
    /// </summary>
    public static class CalciteEventId
    {

        enum Id
        {

            // Core events
            UnexpectedConnectionTypeWarning = CoreEventId.ProviderBaseId,

            // Transaction events
            TransactionIgnoredWarning = CoreEventId.ProviderBaseId + 100,

            // Migration events
            MigrationOperationIgnoredWarning = CoreEventId.ProviderBaseId + 200,
            MigrationTableFeatureIgnoredWarning = CoreEventId.ProviderBaseId + 201,

        }

        static readonly string InfrastructurePrefix = DbLoggerCategory.Infrastructure.Name + ".";
        static readonly string TransactionPrefix = DbLoggerCategory.Database.Transaction.Name + ".";

        static EventId MakeCoreId(Id id) => new((int)id, InfrastructurePrefix + id);

        public static readonly EventId UnexpectedConnectionTypeWarning = MakeCoreId(Id.UnexpectedConnectionTypeWarning);

        static EventId MakeTransactionId(Id id) => new((int)id, TransactionPrefix + id);

        /// <summary>
        /// A transaction operation was requested, but ignored because Calcite does not support transactions.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This event is in the <see cref="DbLoggerCategory.Database.Transaction" /> category.
        ///     </para>
        ///     <para>
        ///         This event uses the <see cref="EventData" /> payload when used with a <see cref="DiagnosticSource" />.
        ///     </para>
        /// </remarks>
        public static readonly EventId TransactionIgnoredWarning = MakeTransactionId(Id.TransactionIgnoredWarning);

        static readonly string MigrationPrefix = DbLoggerCategory.Migrations.Name + ".";
        static EventId MakeMigrationId(Id id) => new((int)id, MigrationPrefix + id);

        /// <summary>
        /// A migration operation was requested, but ignored because Calcite does not support it.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This event is in the <see cref="DbLoggerCategory.Migrations" /> category.
        ///     </para>
        ///     <para>
        ///         This event uses the <see cref="EventData" /> payload when used with a <see cref="DiagnosticSource" />.
        ///     </para>
        /// </remarks>
        public static readonly EventId MigrationOperationIgnoredWarning = MakeMigrationId(Id.MigrationOperationIgnoredWarning);

        /// <summary>
        /// A table sub-feature (e.g. primary key constraint, foreign keys) was requested, but ignored because Calcite does not support it.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This event is in the <see cref="DbLoggerCategory.Migrations" /> category.
        ///     </para>
        ///     <para>
        ///         This event uses the <see cref="EventData" /> payload when used with a <see cref="DiagnosticSource" />.
        ///     </para>
        /// </remarks>
        public static readonly EventId MigrationTableFeatureIgnoredWarning = MakeMigrationId(Id.MigrationTableFeatureIgnoredWarning);

    }

}
