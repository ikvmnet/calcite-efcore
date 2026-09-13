# EF Core Adapter Configuration Guide

The EF Core Calcite Adapter is configurable through the factory pattern, following standard Calcite conventions. Users can provide custom Rex-to-LINQ translation logic by implementing a factory.

## Interfaces

### `IRexToLinqTranslatorFactory`
Factory interface for creating `IRexToLinqTranslator` instances.

**Default Implementation**: `DefaultRexToLinqTranslatorFactory`

**Usage**: Implement this interface to provide custom translator creation logic with dependency injection, caching, or custom configuration.

```csharp
public class CustomRexTranslatorFactory : IRexToLinqTranslatorFactory
{
	public IRexToLinqTranslator Create()
	{
		var customProvider = new CustomOperatorProvider();
		return new RexToLinqTranslator(customProvider);
	}
}
```

### `IRexToLinqTranslator`
Translates Calcite `RexNode` expressions into CLR `Expression` trees suitable for LINQ.

**Default Implementation**: `RexToLinqTranslator`

**Usage**: Subclass `RexToLinqTranslator` and override specific `Translate*` methods to customize translation logic. Pass your custom `ISqlOperatorTranslationProvider` to the constructor.

```csharp
public class CustomRexTranslator : RexToLinqTranslator
{
	public CustomRexTranslator(ISqlOperatorTranslationProvider operatorProvider)
		: base(operatorProvider)
	{
	}

	protected override Expression TranslateCall(RexCall call, EfCoreTranslationContext context)
	{
		// Custom logic for specific call kinds
		if (call.getKind() == SqlKind.MY_CUSTOM_KIND)
		{
			// Custom translation
			return /* ... */;
		}

		// Fall back to default implementation
		return base.TranslateCall(call, context);
	}
}
```

### `ISqlOperatorTranslationProvider`
Provides translation mappings from Calcite `SqlOperator` instances to `SqlOperatorTranslator` delegates.

**Default Implementation**: `SqlOperatorTranslationProvider`

**Usage**: Subclass `SqlOperatorTranslationProvider` and override `Build` to add or replace operator translations.

```csharp
public class CustomOperatorProvider : SqlOperatorTranslationProvider
{
	protected override void Build(Dictionary<SqlOperator, SqlOperatorTranslator> translators)
	{
		// Call base to retain standard mappings
		base.Build(translators);

		// Add or override operator translations
		translators[MyOperators.CUSTOM_CONCAT] = operands =>
			Expression.Call(typeof(string), nameof(string.Concat), null, operands);
	}
}
```

## Configuration via Code

### Direct API Usage

```csharp
// Default configuration
var dataSource = new CalciteDataSourceBuilder(connectionString)
    .AddEfCoreSchema("mySchema", contextFactory)
    .Build();

// Custom factory
var customFactory = new CustomRexTranslatorFactory();
var dataSource = new CalciteDataSourceBuilder(connectionString)
    .AddEfCoreSchema("mySchema", contextFactory, customFactory)
    .Build();

// Or build the schema alone, to register on a SchemaPlus of your own
var schema = EfCoreSchema.Create("mySchema", contextFactory, customFactory);
```

## Configuration via Calcite Model JSON

### Model JSON Structure

```json
{
  "version": "1.0",
  "defaultSchema": "EFCORE",
  "schemas": [
	{
	  "name": "EFCORE",
	  "type": "custom",
	  "factory": "Apache.Calcite.EntityFrameworkCore.Adapter.EfCoreSchemaFactory, Apache.Calcite.EntityFrameworkCore.Adapter",
	  "operand": {
		"dbContextFactory": "MyNamespace.MyDbContextFactory, MyAssembly",
		"rexTranslatorFactory": "MyNamespace.CustomRexTranslatorFactory, MyAssembly"
	  }
	}
  ]
}
```

### The `factory` Key

`factory` names `EfCoreSchemaFactory` itself, and it takes the assembly-qualified type name —
`Apache.Calcite.EntityFrameworkCore.Adapter.EfCoreSchemaFactory, Apache.Calcite.EntityFrameworkCore.Adapter`.
The assembly is not optional. Calcite resolves this value through `Class.forName`, and a CLR type is
not on the class path under its namespace alone; leaving the assembly off fails the model load with
`ClassNotFoundException`.

### Calcite 1.43's Model Class Allowlist

Calcite 1.43 vets every class a model JSON names — schema and table factories, UDFs, JDBC drivers,
dialect factories, lattice statistic providers — through `ClassNameFilter`. The allowlist comes from
the `calcite.model.classes.allowed` system property and is **empty by default, and an empty allowlist
rejects everything**, so on 1.43 a model that names a class fails to load with a `SecurityException`
until some pattern covers it.

**Using the EF Core provider, you do not need to do anything for this factory.** Loading either
`Apache.Calcite.EntityFrameworkCore` or `Apache.Calcite.EntityFrameworkCore.Adapter` appends
`Apache.Calcite.EntityFrameworkCore.` to the allowlist from a module initializer, and `UseCalcite`
lives in the provider, so configuring a context loads it well before any connection opens. Only this
library's own namespace is added, so Calcite's protection against the classes that make model files
dangerous in the first place — JNDI lookups, `Runtime`, script engines — is left exactly as Calcite
set it.

There is one case the module initializer cannot reach. A module initializer runs when its assembly
loads, and .NET loads an assembly on first use of a type in it — so if you open a `CalciteConnection`
directly from `Apache.Calcite.Data` against a model naming this factory, with nothing yet having
touched either assembly, Calcite runs its check *before* resolving the class, which is what would have
loaded us. Nothing of ours has run at that point. Name the namespace yourself before you connect:

```csharp
java.lang.System.setProperty("calcite.model.classes.allowed", "Apache.Calcite.EntityFrameworkCore.");
```

The property is one comma-separated string shared by the whole process, so it is **appended to, never
assigned**. A pattern you or another library already set is preserved.

To name a class of your own — your own `SchemaFactory`, a UDF, a `jdbcDriver` — set the property
before you open a connection. It is one comma-separated string for the whole process, so **include this
package's namespace alongside yours**; assigning the property after we have appended to it would drop
our entry and break loading this factory:

```csharp
java.lang.System.setProperty(
    "calcite.model.classes.allowed",
    "Apache.Calcite.EntityFrameworkCore.,MyApp.Calcite.,MyApp.OneFactory");
```

Setting it *before* anything loads this package is enough on its own — the module initializer appends
to whatever it finds rather than replacing it, so your patterns survive.

A pattern ending in `.` matches that namespace and everything beneath it; any other pattern must match
the value in the JSON exactly. The check runs against the raw string from the model, so an exact
pattern has to include the assembly suffix — which the namespace-prefix form avoids having to think
about. Calcite reads the property once, into a static field, the first time its configuration class
initializes, so anything you add has to be added before then.

The `operand` keys are not affected. Calcite filters only the keys it resolves itself; `dbContextType`,
`dbContextFactory` and `rexTranslatorFactory` are loaded by this factory through
`Activator.CreateInstance` and never reach `ClassNameFilter`.

None of this applies before Calcite 1.43, which has no such filter.

### Operand Keys

Exactly one of the two context keys is required. `dbContextFactory` is read first; `dbContextType`
is the fallback.

- **`dbContextFactory`**: Assembly-qualified name of an `IDbContextFactory` implementation. Use this
  whenever the context needs configuring — a connection string, a provider, a tracking behaviour —
  because the factory decides how each `DbContext` is built.
- **`dbContextType`**: Assembly-qualified name of a `DbContext` subclass. The schema constructs it
  directly, so it suits only a context that configures itself in its own `OnConfiguring`.
- **`rexTranslatorFactory`** (optional): Assembly-qualified name of an `IRexToLinqTranslatorFactory`
  implementation.

Every type named here is created with `Activator.CreateInstance`, so each needs a public
parameterless constructor — the `DbContext` itself included, where `dbContextType` is used.

Naming neither context key, or naming a type that does not implement the interface the key expects,
raises an `ArgumentException` while the model is loading.

## How It Works

1. **Schema owns the factory**:
   - `EfCoreSchema` stores an `IRexToLinqTranslatorFactory` instance
   - Factory is set during schema creation (defaults to `DefaultRexToLinqTranslatorFactory.Instance`)

2. **Convention accesses translator through factory**:
   - `EfCoreConvention` has a reference to its `EfCoreSchema`
   - `RexTranslator` property calls `_schema.TranslatorFactory.Create()` each time
   - This allows for per-query translator instances if needed

3. **Rel nodes use the translator**:
   - Each `EfCoreRel` implementation accesses: `convention.RexTranslator`
   - The translator is used for all Rex-to-LINQ translations

4. **SchemaFactory loads from JSON**:
   - `EfCoreSchemaFactory.create()` reads `dbContextFactory` or `dbContextType` from the `operand` map
   - If `rexTranslatorFactory` is specified, it's instantiated and passed to the schema
   - Otherwise, the default factory is used

## Complete Example

### 1. Custom Operator Provider

```csharp
public class CustomOperatorProvider : SqlOperatorTranslationProvider
{
	protected override void Build(Dictionary<SqlOperator, SqlOperatorTranslator> translators)
	{
		base.Build(translators);

		translators[MyOperators.JSON_EXTRACT] = operands =>
			Expression.Call(
				typeof(JsonExtensions),
				nameof(JsonExtensions.ExtractValue),
				null,
				operands[0],
				operands[1]);
	}
}
```

### 2. Custom Translator Factory

```csharp
public class CustomRexTranslatorFactory : IRexToLinqTranslatorFactory
{
	public IRexToLinqTranslator Create()
	{
		var provider = new CustomOperatorProvider();
		return new RexToLinqTranslator(provider);
	}
}
```

### 3. Model JSON Configuration

```json
{
  "schemas": [
	{
	  "name": "EFCORE",
	  "type": "custom",
	  "factory": "Apache.Calcite.EntityFrameworkCore.Adapter.EfCoreSchemaFactory, Apache.Calcite.EntityFrameworkCore.Adapter",
	  "operand": {
		"dbContextType": "MyApp.MyDbContext, MyApp",
		"rexTranslatorFactory": "MyApp.CustomRexTranslatorFactory, MyApp"
	  }
	}
  ]
}
```

### 4. Usage

Now when Calcite executes queries using `JSON_EXTRACT`, they'll be translated to your custom `JsonExtensions.ExtractValue` method.

## Benefits

✅ **Standard Calcite pattern** - Uses factory pattern like other Calcite adapters  
✅ **Flexible** - Factories can implement caching, pooling, or dependency injection  
✅ **Extensible** - Easy to add custom operators and translation logic  
✅ **Clean separation** - Operator provider is implementation detail of translator  
✅ **Type-safe** - All customizations strongly typed through interfaces  
✅ **JSON configurable** - Can be configured via Calcite model files  
✅ **Backward compatible** - Defaults work out of the box
