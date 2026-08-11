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
var schema = EfCoreSchema.Create(parentSchema, "mySchema", contextFactory);

// Custom factory
var customFactory = new CustomRexTranslatorFactory();
var schema = EfCoreSchema.Create(parentSchema, "mySchema", contextFactory, customFactory);
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
	  "factory": "Apache.Calcite.EntityFrameworkCore.Adapter.EfCoreSchemaFactory",
	  "operand": {
		"contextType": "MyNamespace.MyDbContext, MyAssembly",
		"rexTranslatorFactory": "MyNamespace.CustomRexTranslatorFactory, MyAssembly"
	  }
	}
  ]
}
```

### Operand Keys

- **`contextType`** (required): Assembly-qualified name of the `DbContext` subclass
- **`rexTranslatorFactory`** (optional): Assembly-qualified name of an `IRexToLinqTranslatorFactory` implementation

Both types must have a parameterless constructor.

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
   - `EfCoreSchemaFactory.create()` reads the `operand` map
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
	  "factory": "Apache.Calcite.EntityFrameworkCore.Adapter.EfCoreSchemaFactory",
	  "operand": {
		"contextType": "MyApp.MyDbContext, MyApp",
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
