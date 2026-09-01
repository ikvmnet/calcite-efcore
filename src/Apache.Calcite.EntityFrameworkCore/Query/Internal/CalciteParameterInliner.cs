using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal;

/// <summary>
/// Rewrites a parameterized Calcite command into a statement that carries its own values.
/// </summary>
/// <remarks>
/// Calcite parameters are positional <c>?</c> markers: they have no name, so there is nothing to
/// declare ahead of the statement the way the relational default does for named parameters. To
/// produce SQL that runs as it stands, each marker is replaced by its value written as a literal by
/// the type mapping for that value.
/// </remarks>
public static class CalciteParameterInliner
{

    /// <summary>
    /// Returns the text of <paramref name="command"/> with every positional <c>?</c> placeholder
    /// replaced by the matching parameter value written as a Calcite SQL literal.
    /// </summary>
    /// <remarks>
    /// Placeholders are matched to <see cref="DbCommand.Parameters"/> by position, which is the
    /// order <c>CalciteCommand.AddDbParameters</c> establishes. Quoted strings, quoted identifiers,
    /// and comments are copied through untouched, so a <c>?</c> inside a value or inside a query tag
    /// is not mistaken for a placeholder. A placeholder with no parameter left to bind is left as it
    /// is rather than throwing, because this text is used for diagnostics.
    /// </remarks>
    /// <param name="command">The command whose text and parameter values are rendered.</param>
    /// <param name="typeMappingSource">
    /// The source consulted for the type mapping that writes each value; where it has no mapping for
    /// a value, a built-in rendering is used instead.
    /// </param>
    /// <returns></returns>
    public static string Inline(DbCommand command, IRelationalTypeMappingSource? typeMappingSource = null)
    {
        ArgumentNullException.ThrowIfNull(command);

        var sql = command.CommandText;
        if (command.Parameters.Count == 0 || sql.Contains('?') == false)
            return sql;

        var literals = new string[command.Parameters.Count];
        for (var i = 0; i < literals.Length; i++)
            literals[i] = GenerateLiteral(command.Parameters[i], typeMappingSource);

        return Inline(sql, literals);
    }

    /// <summary>
    /// Replaces each positional <c>?</c> placeholder in <paramref name="sql"/>, in order, with the
    /// literal at the matching index of <paramref name="literals"/>.
    /// </summary>
    /// <param name="sql"></param>
    /// <param name="literals"></param>
    /// <returns></returns>
    static string Inline(string sql, IReadOnlyList<string> literals)
    {
        var builder = new StringBuilder(sql.Length + 64);
        var next = 0;
        var i = 0;

        while (i < sql.Length)
        {
            var c = sql[i];

            // a quoted string or quoted identifier is copied through as it stands, honoring the
            // doubled delimiter that escapes one within it
            if (c is '\'' or '"')
            {
                builder.Append(c);
                i++;

                while (i < sql.Length)
                {
                    if (sql[i] == c)
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == c)
                        {
                            builder.Append(c, 2);
                            i += 2;
                            continue;
                        }

                        builder.Append(c);
                        i++;
                        break;
                    }

                    builder.Append(sql[i]);
                    i++;
                }

                continue;
            }

            // a line comment — query tags arrive as these — runs to the end of its line
            if (c is '-' && i + 1 < sql.Length && sql[i + 1] is '-')
            {
                while (i < sql.Length && sql[i] is not ('\r' or '\n'))
                {
                    builder.Append(sql[i]);
                    i++;
                }

                continue;
            }

            // a block comment runs to its terminator
            if (c is '/' && i + 1 < sql.Length && sql[i + 1] is '*')
            {
                builder.Append("/*");
                i += 2;

                while (i < sql.Length)
                {
                    if (sql[i] is '*' && i + 1 < sql.Length && sql[i + 1] is '/')
                    {
                        builder.Append("*/");
                        i += 2;
                        break;
                    }

                    builder.Append(sql[i]);
                    i++;
                }

                continue;
            }

            if (c is '?' && next < literals.Count)
            {
                builder.Append(literals[next]);
                next++;
                i++;
                continue;
            }

            builder.Append(c);
            i++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Writes the value of <paramref name="parameter"/> as a Calcite SQL literal.
    /// </summary>
    /// <param name="parameter"></param>
    /// <param name="typeMappingSource"></param>
    /// <returns></returns>
    static string GenerateLiteral(DbParameter parameter, IRelationalTypeMappingSource? typeMappingSource)
    {
        var value = parameter.Value;
        if (value is null or DBNull)
            return "NULL";

        // the parameter already holds a provider value, so the mapping for its own type writes it
        // without a converter in the way
        var mapping = typeMappingSource?.FindMapping(value.GetType());
        if (mapping != null)
            return mapping.GenerateProviderValueSqlLiteral(value);

        return GenerateDefaultLiteral(value);
    }

    /// <summary>
    /// Writes a value for which no type mapping was found, covering the types an ADO.NET parameter
    /// can carry.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    static string GenerateDefaultLiteral(object value) => value switch
    {
        bool b => b ? "TRUE" : "FALSE",
        byte[] bytes => "X'" + Convert.ToHexString(bytes) + "'",
        DateTime dateTime => "TIMESTAMP '" + dateTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "'",
        DateTimeOffset dateTimeOffset => "TIMESTAMP WITH TIME ZONE '" + dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.fff 'GMT'zzz", CultureInfo.InvariantCulture) + "'",
        DateOnly dateOnly => "DATE '" + dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "'",
        TimeOnly timeOnly => "TIME '" + timeOnly.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "'",
        char c => Quote(c.ToString()),
        string s => Quote(s),
        sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal =>
            ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture),
        _ => Quote(value.ToString() ?? ""),
    };

    /// <summary>
    /// Wraps <paramref name="value"/> in single quotes, doubling any it contains.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

}
