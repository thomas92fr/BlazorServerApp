using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BlazorServerApp.Model.Entities;

namespace BlazorServerApp.Model.Data.EntityConfigurations;

/// <summary>
/// EF Core configuration for User entity.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .ValueGeneratedOnAdd();

        builder.Property(u => u.UserName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Password)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasData(new User
        {
            Id = 1,
            UserName = "Admin",
            Password = "$2a$11$47W/33oCRWXmEhGFMlhmnON9HhnB8wMvkyNxMpUzagzVkVbOg.D3G" // "Password"
        });
    }
}
