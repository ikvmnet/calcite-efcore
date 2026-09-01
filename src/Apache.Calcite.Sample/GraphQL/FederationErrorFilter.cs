using Apache.Calcite.Sample.Federation;

using HotChocolate;
using HotChocolate.Execution;

namespace Apache.Calcite.Sample.GraphQL;

/// <summary>
/// Puts the real cause of a failed field back into the GraphQL response.
/// </summary>
/// <remarks>
/// HotChocolate reports an unhandled resolver exception as "Unexpected Execution Error" and drops the exception,
/// which in this sample throws away the only interesting part of the request. Every failure here is a report about
/// the provider, so the filter logs it and returns the unwrapped Calcite chain to the caller.
/// </remarks>
public sealed class FederationErrorFilter : IErrorFilter
{

    /// <inheritdoc />
    public IError OnError(IError error)
    {
        if (error.Exception is null)
            return error;

        // No logger is taken: filters are built from the schema service provider, which has no logging in it.
        return error
            .WithMessage(error.Exception.Message)
            .SetExtension("cause", CalciteErrors.Describe(error.Exception));
    }

}
