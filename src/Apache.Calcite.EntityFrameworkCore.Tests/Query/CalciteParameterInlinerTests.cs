using System;
using System.Data.Common;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Query.Internal;
using Apache.Calcite.EntityFrameworkCore.Tests.ValueGeneration;

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

using Xunit;

namespace Apache.Calcite.EntityFrameworkCore.Tests.Query;

/// <summary>
/// Tests covering <see cref="CalciteParameterInliner"/> for a <see cref="Guid"/>, which Calcite
/// holds as a UUID and Entity Framework Core would otherwise write as a string.
/// </summary>
public class CalciteParameterInlinerTests
{

    static readonly Guid Id = Guid.Parse("5a2c9e7e-3f4b-4c8a-9d1e-6b0f2a7c4d31");

    /// <summary>
    /// Builds a command carrying the given values as positional parameters.
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="commandText"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    static DbCommand CreateCommand(CalciteConnection connection, string commandText, params object?[] values)
    {
        DbCommand command = connection.CreateCommand();
        command.CommandText = commandText;

        foreach (var value in values)
        {
            var parameter = command.CreateParameter();
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        return command;
    }

    [Fact]
    public void Writes_a_guid_as_a_uuid_literal()
    {
        using var connection = GuidKeyDbContext.CreateConnection();
        using var context = new GuidKeyDbContext(connection);
        var typeMappingSource = context.GetService<IRelationalTypeMappingSource>();

        using var command = CreateCommand(connection, "SELECT * FROM \"T\" WHERE \"Id\" = ?", Id);

        // the mapping the source finds for a Guid carries Entity Framework Core's default
        // conversion to a string; the value Calcite is handed is a UUID, so the literal is one
        Assert.Equal(
            "SELECT * FROM \"T\" WHERE \"Id\" = UUID '5a2c9e7e-3f4b-4c8a-9d1e-6b0f2a7c4d31'",
            CalciteParameterInliner.Inline(command, typeMappingSource));
    }

    [Fact]
    public void The_uuid_literal_means_what_the_bound_parameter_means()
    {
        using var connection = GuidKeyDbContext.CreateConnection();

        using var source = CreateCommand(connection, "?", Id);
        var literal = CalciteParameterInliner.Inline(source);

        // the literal is written from the canonical text, and the parameter is carried over as the
        // sixteen bytes in that same order, so Calcite has to see one value and not two
        connection.Open();
        using var command = CreateCommand(connection, $"VALUES (? = {literal})", Id);

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.True(Convert.ToBoolean(reader.GetValue(0)));
    }

}
