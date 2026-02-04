using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Model.Data.EntityConfigurations;

/// <summary>
/// EF Core configuration for WeatherForecast entity.
/// </summary>
public class WeatherForecastConfiguration : IEntityTypeConfiguration<WeatherForecast>
{
    public void Configure(EntityTypeBuilder<WeatherForecast> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .ValueGeneratedOnAdd();

        builder.Property(w => w.Date)
            .IsRequired();

        builder.Property(w => w.TemperatureC)
            .IsRequired();

        builder.Property(w => w.Summary)
            .HasMaxLength(200);

        // TemperatureF is computed property, not stored in database
        builder.Ignore(w => w.TemperatureF);
    }
}
