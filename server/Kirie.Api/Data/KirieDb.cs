using Kirie.Api.Cranes;
using Kirie.Api.Weather;
using Microsoft.EntityFrameworkCore;

namespace Kirie.Api.Data;

public sealed class KirieDb(DbContextOptions<KirieDb> options) : DbContext(options)
{
    public DbSet<WeatherReading> Readings => Set<WeatherReading>();
    public DbSet<Crane> Cranes => Set<Crane>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<WeatherReading>(reading =>
        {
            reading.HasKey(r => r.CityId);
            reading.Property(r => r.CityId).HasMaxLength(40);
            reading.Property(r => r.Source).HasMaxLength(20);
        });

        model.Entity<Crane>(crane =>
        {
            crane.HasKey(c => c.Id);
            crane.Property(c => c.CityId).HasMaxLength(40);
            crane.Property(c => c.Colour).HasMaxLength(20);
            crane.Property(c => c.Folder).HasMaxLength(64);
            crane.HasIndex(c => new { c.CityId, c.FoldedAt });
        });

        // SQLite can't order or compare DateTimeOffset columns, so store them as
        // UTC ticks, which sort the same way the instants do.
        foreach (var entity in model.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(DateTimeOffset)))
            {
                property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, long>(
                    value => value.UtcTicks,
                    ticks => new DateTimeOffset(ticks, TimeSpan.Zero)));
            }
        }
    }
}
