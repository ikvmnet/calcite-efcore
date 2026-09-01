using System;
using System.Text;

namespace Apache.Calcite.EntityFrameworkCore.BenchmarkUtilities;

/// <summary>
/// Renders an exception that came out of Calcite into something worth reading.
/// </summary>
/// <remarks>
/// Calcite's <c>EnumerableRelImplementor.implementRoot</c> reports a failure to implement a rel node as an
/// <c>IllegalStateException</c> whose real cause is attached as a <em>suppressed</em> exception. .NET's
/// <see cref="Exception.ToString"/> knows nothing about suppressed exceptions, so the one line that says what
/// actually went wrong is exactly the line a plain stack trace drops.
/// </remarks>
public static class CalciteDiagnostics
{

    /// <summary>
    /// Describes an exception, following both the CLR inner-exception chain and the Java suppressed-exception
    /// lists hanging off any Java throwable in it.
    /// </summary>
    /// <param name="exception">The exception to describe.</param>
    /// <returns>A one-line-per-cause description.</returns>
    public static string Describe(Exception? exception)
    {
        if (exception is null)
            return "(no exception)";

        var builder = new StringBuilder();

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (builder.Length > 0)
                builder.Append(" <- ");

            builder.Append(current.GetType().Name).Append(": ").Append(current.Message);
            AppendSuppressed(builder, current);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Appends the suppressed exceptions of a Java throwable, which is where Calcite files the actual cause.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="exception">The exception to inspect.</param>
    static void AppendSuppressed(StringBuilder builder, Exception exception)
    {
        if (exception is not java.lang.Throwable throwable)
            return;

        foreach (var suppressed in throwable.getSuppressed())
            builder.Append(" [suppressed: ").Append(suppressed.getClass().getName()).Append(": ").Append(suppressed.getMessage()).Append(']');
    }

}
