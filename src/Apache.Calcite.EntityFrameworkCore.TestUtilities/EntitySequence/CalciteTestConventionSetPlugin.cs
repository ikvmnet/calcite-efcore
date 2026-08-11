using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Apache.Calcite.EntityFrameworkCore.TestUtilities;

/// <summary>
/// Adds the entity-sequence strategy convention to the convention set. Test infrastructure only —
/// the provider's convention set knows nothing of entity sequences.
/// </summary>
public class CalciteTestConventionSetPlugin : IConventionSetPlugin
{

    readonly ProviderConventionSetBuilderDependencies _dependencies;
    readonly RelationalConventionSetBuilderDependencies _relationalDependencies;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="dependencies">The provider convention set dependencies.</param>
    /// <param name="relationalDependencies">The relational convention set dependencies.</param>
    public CalciteTestConventionSetPlugin(ProviderConventionSetBuilderDependencies dependencies, RelationalConventionSetBuilderDependencies relationalDependencies)
    {
        _dependencies = dependencies;
        _relationalDependencies = relationalDependencies;
    }

    /// <inheritdoc />
    public ConventionSet ModifyConventions(ConventionSet conventionSet)
    {
        conventionSet.Add(new CalciteValueGenerationStrategyConvention(_dependencies, _relationalDependencies));
        return conventionSet;
    }

}
