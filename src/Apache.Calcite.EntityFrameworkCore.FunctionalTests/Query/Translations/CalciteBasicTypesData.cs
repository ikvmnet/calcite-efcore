using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.EntityFrameworkCore.TestModels.BasicTypesModel;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Apache.Calcite.EntityFrameworkCore.FunctionalTests.Query.Translations;

internal class CalciteBasicTypesData : ISetSource
{

    public IReadOnlyList<BasicTypesEntity> BasicTypesEntities { get; }
    public IReadOnlyList<NullableBasicTypesEntity> NullableBasicTypesEntities { get; }

    public CalciteBasicTypesData()
    {
        BasicTypesEntities = BasicTypesData.CreateBasicTypesEntities().Select(RoundTemporalFields).ToList();
        NullableBasicTypesEntities = BasicTypesData.CreateNullableBasicTypesEntities().Select(RoundTemporalFields).ToList();
    }

    public IQueryable<TEntity> Set<TEntity>()
        where TEntity : class
    {
        if (typeof(TEntity) == typeof(BasicTypesEntity))
            return (IQueryable<TEntity>)BasicTypesEntities.AsQueryable();

        if (typeof(TEntity) == typeof(NullableBasicTypesEntity))
            return (IQueryable<TEntity>)NullableBasicTypesEntities.AsQueryable();

        throw new InvalidOperationException("Invalid entity type: " + typeof(TEntity));
    }

    private static BasicTypesEntity RoundTemporalFields(BasicTypesEntity e)
    {
        e.DateTime = TruncateToMilliseconds(e.DateTime);
        e.DateTimeOffset = TruncateToMilliseconds(e.DateTimeOffset);
        e.TimeOnly = TruncateToMilliseconds(e.TimeOnly);
        e.TimeSpan = TruncateToMilliseconds(e.TimeSpan);
        return e;
    }

    private static NullableBasicTypesEntity RoundTemporalFields(NullableBasicTypesEntity e)
    {
        e.DateTime = e.DateTime.HasValue ? TruncateToMilliseconds(e.DateTime.Value) : null;
        e.DateTimeOffset = e.DateTimeOffset.HasValue ? TruncateToMilliseconds(e.DateTimeOffset.Value) : null;
        e.TimeOnly = e.TimeOnly.HasValue ? TruncateToMilliseconds(e.TimeOnly.Value) : null;
        e.TimeSpan = e.TimeSpan.HasValue ? TruncateToMilliseconds(e.TimeSpan.Value) : null;
        return e;
    }

    private static DateTime TruncateToMilliseconds(DateTime dt)
        => new DateTime(dt.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, dt.Kind);

    private static DateTimeOffset TruncateToMilliseconds(DateTimeOffset dto)
        => new DateTimeOffset(dto.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, dto.Offset);

    private static TimeOnly TruncateToMilliseconds(TimeOnly t)
        => new TimeOnly(t.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond);

    private static TimeSpan TruncateToMilliseconds(TimeSpan ts)
        => TimeSpan.FromTicks(ts.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond);

}
