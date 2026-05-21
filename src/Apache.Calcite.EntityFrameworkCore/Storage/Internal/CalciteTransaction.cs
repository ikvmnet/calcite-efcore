using System;
using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal
{

    public class CalciteTransaction : RelationalTransaction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="transaction"></param>
        /// <param name="transactionId"></param>
        /// <param name="logger"></param>
        /// <param name="transactionOwned"></param>
        /// <param name="sqlGenerationHelper"></param>
        public CalciteTransaction(IRelationalConnection connection, DbTransaction transaction, Guid transactionId, IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger, bool transactionOwned, ISqlGenerationHelper sqlGenerationHelper) :
            base(connection, transaction, transactionId, logger, transactionOwned, sqlGenerationHelper)
        {

        }

    }

}
