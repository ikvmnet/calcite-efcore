using System.Text;

namespace Apache.Calcite.Sample.Federation;

/// <summary>
/// Renders the exceptions Calcite throws into something readable.
/// </summary>
/// <remarks>
/// Calcite hides the diagnosis twice over. A failure during implementation arrives as an
/// <c>IllegalStateException</c> saying only "Unable to implement", with the real cause attached as a
/// <em>suppressed</em> exception, and .NET prints neither suppressed exceptions nor the Java cause chain of a
/// wrapped throwable. Walking both is usually the whole diagnosis.
/// </remarks>
public static class CalciteErrors
{

    /// <summary>
    /// Describes an exception, following its .NET inner exceptions and then the Java cause and suppressed chains.
    /// </summary>
    /// <param name="exception">The exception to describe.</param>
    /// <returns>The rendered description.</returns>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var builder = new StringBuilder();

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            builder.AppendLine($"{current.GetType().FullName}: {current.Message}");

            if (current is java.lang.Throwable throwable)
                AppendJava(builder, throwable, 1);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Appends the cause and suppressed chains of a Java throwable.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="throwable">The throwable to walk.</param>
    /// <param name="depth">The current nesting depth.</param>
    static void AppendJava(StringBuilder builder, java.lang.Throwable throwable, int depth)
    {
        if (depth > 12)
            return;

        var indent = new string(' ', depth * 2);

        // IKVM surfaces java.lang.Throwable as System.Exception, so these come back needing a cast to be walked further.
        foreach (var suppressed in throwable.getSuppressed())
        {
            builder.AppendLine($"{indent}suppressed: {suppressed.GetType().FullName}: {suppressed.Message}");

            if (suppressed is java.lang.Throwable inner)
                AppendJava(builder, inner, depth + 1);
        }

        var cause = throwable.getCause();
        if (cause is not null && ReferenceEquals(cause, throwable) == false)
        {
            builder.AppendLine($"{indent}caused by: {cause.GetType().FullName}: {cause.Message}");

            if (cause is java.lang.Throwable innerCause)
                AppendJava(builder, innerCause, depth + 1);
        }
    }

}
