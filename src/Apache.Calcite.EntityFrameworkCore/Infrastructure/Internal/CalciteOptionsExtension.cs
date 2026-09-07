using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Apache.Calcite.EntityFrameworkCore.Extensions;

using Apache.Calcite.Data;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Apache.Calcite.EntityFrameworkCore.Infrastructure.Internal
{

    /// <summary>
    /// Records options applied to the Calcite context.
    /// </summary>
    public class CalciteOptionsExtension : RelationalOptionsExtension
    {

        DbContextOptionsExtensionInfo? _info;
        CalciteProviderFactory? _providerFactory;
        CalciteDataSource? _dataSource;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public CalciteOptionsExtension()
        {

        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="copyFrom"></param>
        protected CalciteOptionsExtension(CalciteOptionsExtension copyFrom) :
            base(copyFrom)
        {
            _providerFactory = copyFrom._providerFactory;
            _dataSource = copyFrom._dataSource;
        }

        /// <inheritdoc />
        public override DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

        /// <inheritdoc />
        protected override RelationalOptionsExtension Clone() => new CalciteOptionsExtension(this);

        /// <summary>
        /// Gets the <see cref="CalciteProviderFactory"/> that will be used to initialize new connections.
        /// </summary>
        public virtual CalciteProviderFactory? ProviderFactory => _providerFactory;

        /// <summary>
        /// Sets the <see cref="CalciteProviderFactory"/> that will be used to initialize new connections.
        /// </summary>
        /// <param name="providerFactory"></param>
        /// <returns></returns>
        public virtual CalciteOptionsExtension WithProviderFactory(CalciteProviderFactory providerFactory)
        {
            var clone = (CalciteOptionsExtension)Clone();
            clone._providerFactory = providerFactory;
            return clone;
        }

        /// <summary>
        /// Gets the <see cref="CalciteDataSource"/> new connections are drawn from, or <see langword="null"/>
        /// when connections are constructed from the connection string.
        /// </summary>
        /// <remarks>
        /// A data source holds the root schema and hands it to every connection it opens, which is how a
        /// context reaches schemas registered before it — an Entity Framework Core context named as a
        /// Calcite schema, among them.
        /// </remarks>
        public virtual CalciteDataSource? DataSource => _dataSource;

        /// <summary>
        /// Sets the <see cref="CalciteDataSource"/> new connections are drawn from.
        /// </summary>
        /// <param name="dataSource"></param>
        /// <returns></returns>
        public virtual CalciteOptionsExtension WithDataSource(CalciteDataSource dataSource)
        {
            var clone = (CalciteOptionsExtension)Clone();
            clone._dataSource = dataSource;
            return clone;
        }

        /// <inheritdoc />
        public override void ApplyServices(IServiceCollection services) => services.AddEntityFrameworkCalcite();

        sealed class ExtensionInfo(IDbContextOptionsExtension extension) : RelationalExtensionInfo(extension)
        {

            int? _serviceProviderHash;
            string? _logFragment;

            new CalciteOptionsExtension Extension => (CalciteOptionsExtension)base.Extension;

            /// <inheritdoc />
            public override bool IsDatabaseProvider => true;

            /// <inheritdoc />
            public override string LogFragment
            {
                get
                {
                    if (_logFragment == null)
                    {
                        var builder = new StringBuilder();
                        builder.Append(base.LogFragment);

                        if (Extension._providerFactory != null)
                            builder.Append("CalciteProviderFactory ");

                        if (Extension._dataSource != null)
                            builder.Append("CalciteDataSource ");

                        _logFragment = builder.ToString();
                    }

                    return _logFragment;
                }
            }

            /// <inheritdoc />
            public override int GetServiceProviderHashCode()
            {
                _serviceProviderHash ??= HashCode.Combine(
                    base.GetServiceProviderHashCode(),
                    3313,
                    Extension._providerFactory,
                    Extension._dataSource);

                return _serviceProviderHash.Value;
            }

            /// <inheritdoc />
            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            {
                debugInfo["Calcite:" + nameof(ProviderFactory)] = (Extension._providerFactory?.GetHashCode() ?? 0L).ToString(CultureInfo.InvariantCulture);
                debugInfo["Calcite:" + nameof(DataSource)] = (Extension._dataSource?.GetHashCode() ?? 0L).ToString(CultureInfo.InvariantCulture);
            }

        }

    }

}
