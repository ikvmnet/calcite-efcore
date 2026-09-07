using System;

using Apache.Calcite.Data;
using Apache.Calcite.EntityFrameworkCore.Adapter.Rex;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Adapter;

/// <summary>
/// Names a <see cref="DbContext"/> as a Calcite schema on a <see cref="CalciteDataSourceBuilder"/>.
/// </summary>
/// <remarks>
/// The root schema belongs to the data source rather than to a connection, so a context is named once,
/// here, and every connection the data source opens sees it — where a schema was previously added to the
/// root of a connection that had already been opened.
/// </remarks>
public static class CalciteDataSourceBuilderExtensions
{

    /// <summary>
    /// Adds a schema serving the context <paramref name="contextFactory"/> produces.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name">The schema name, which is what SQL addresses the context's tables by.</param>
    /// <param name="contextFactory">Factory that produces a fresh <see cref="DbContext"/> on demand.</param>
    /// <param name="translatorFactory">Optional factory that creates Rex-to-LINQ translators. Pass <see langword="null"/> to use the default.</param>
    /// <returns>The builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="name"/> or <paramref name="contextFactory"/> is <see langword="null"/>.</exception>
    public static CalciteDataSourceBuilder AddEfCoreSchema(this CalciteDataSourceBuilder builder, string name, Func<DbContext> contextFactory, IRexToLinqTranslatorFactory? translatorFactory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(contextFactory);

        return builder.AddSchema(name, EfCoreSchema.Create(name, contextFactory, translatorFactory));
    }

    /// <summary>
    /// Adds a schema serving the context <paramref name="contextFactory"/> produces.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="name">The schema name, which is what SQL addresses the context's tables by.</param>
    /// <param name="contextFactory">Factory that produces <see cref="DbContext"/> instances.</param>
    /// <param name="translatorFactory">Optional factory that creates Rex-to-LINQ translators. Pass <see langword="null"/> to use the default.</param>
    /// <returns>The builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="name"/> or <paramref name="contextFactory"/> is <see langword="null"/>.</exception>
    public static CalciteDataSourceBuilder AddEfCoreSchema(this CalciteDataSourceBuilder builder, string name, IDbContextFactory contextFactory, IRexToLinqTranslatorFactory? translatorFactory = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(contextFactory);

        return builder.AddSchema(name, EfCoreSchema.Create(name, contextFactory, translatorFactory));
    }

}
