using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlazorServerApp.Model.Entities;

namespace BlazorServerApp.Model.Data.EntityConfigurations;

/// <summary>
/// EF Core configuration for Person entity.
/// </summary>
public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Age)
            .IsRequired();

        builder.Property(p => p.IsTeacher)
            .IsRequired();

        builder.Property(p => p.StartDateTime)
            .IsRequired();

        builder.Property(p => p.EndDateTime)
            .IsRequired();

        builder.HasOne(p => p.Mentor)
            .WithMany()
            .HasForeignKey(p => p.MentorId);
    }
}
