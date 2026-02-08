using Orchitect.Inventory.Domain.Ticketing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Orchitect.Inventory.Persistence.Configurations;

internal sealed class TicketingUserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("TicketingUsers");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).IsRequired();
        builder.Property(u => u.Name).IsRequired();
        builder.Property(u => u.Email).IsRequired();

        builder.HasIndex(u => u.Email);
    }
}
