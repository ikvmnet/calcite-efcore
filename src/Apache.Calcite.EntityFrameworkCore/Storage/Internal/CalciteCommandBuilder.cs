using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal
{

    /// <inheritdoc />
    public class CalciteRelationalCommandBuilder : RelationalCommandBuilder
    {

        readonly List<IRelationalParameter> _parameters = [];
        readonly IndentedStringBuilder _commandTextBuilder = new();
        IndentedStringBuilder? _logCommandTextBuilder;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        public CalciteRelationalCommandBuilder(RelationalCommandBuilderDependencies dependencies) :
            base(dependencies)
        {

        }

        /// <inheritdoc />
        public override IRelationalCommand Build()
        {
            var commandText = _commandTextBuilder.ToString();
            var logCommandText = _logCommandTextBuilder?.ToString() ?? commandText;
            return new CalciteCommand(Dependencies, commandText, logCommandText, Parameters);
        }

        /// <summary>
        /// Gets the command text.
        /// </summary>
        public override string ToString() => _commandTextBuilder.ToString();

        /// <inheritdoc />
        public override IReadOnlyList<IRelationalParameter> Parameters => _parameters;

        /// <inheritdoc />
        /// <remarks>
        /// Overridden to ensure parameters are inserted into the collection in the order they are added.
        /// This is critical for Calcite which expects positional parameters.
        /// </remarks>
        public override IRelationalCommandBuilder AddParameter(IRelationalParameter parameter)
        {
            _parameters.Add(parameter);

            return this;
        }

        /// <inheritdoc />
        public override IRelationalCommandBuilder RemoveParameterAt(int index)
        {
            _parameters.RemoveAt(index);

            return this;
        }

        /// <inheritdoc />
        public override IRelationalCommandBuilder Append(string value, bool sensitive = false)
        {
            InitializeLogCommandTextBuilderIfNeeded(sensitive);
            _commandTextBuilder.Append(value);
            _logCommandTextBuilder?.Append(sensitive ? "?" : value);

            return this;
        }

        /// <inheritdoc />
        public override IRelationalCommandBuilder Append(FormattableString value, bool sensitive = false)
        {
            InitializeLogCommandTextBuilderIfNeeded(sensitive);
            _commandTextBuilder.Append(value);
            _logCommandTextBuilder?.Append(sensitive ? $"?" : value);

            return this;
        }

        /// <inheritdoc />
        public override IRelationalCommandBuilder AppendLine()
        {
            _commandTextBuilder.AppendLine();
            _logCommandTextBuilder?.AppendLine();

            return this;
        }

        /// <inheritdoc />
        public override IRelationalCommandBuilder IncrementIndent()
        {
            _commandTextBuilder.IncrementIndent();
            _logCommandTextBuilder?.IncrementIndent();

            return this;
        }

        /// <inheritdoc />
        public override IRelationalCommandBuilder DecrementIndent()
        {
            _commandTextBuilder.DecrementIndent();
            _logCommandTextBuilder?.DecrementIndent();

            return this;
        }

        /// <inheritdoc />
        public override int CommandTextLength => _commandTextBuilder.Length;

        void InitializeLogCommandTextBuilderIfNeeded(bool sensitive)
        {
            if (sensitive && _logCommandTextBuilder is null && !Dependencies.LoggingOptions.IsSensitiveDataLoggingEnabled)
            {
                _logCommandTextBuilder = _commandTextBuilder.Clone();
            }
        }

    }

}