using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orchitect.Domain.Inventory.Ticketing;

namespace Orchitect.Persistence.Configurations.Inventory;

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
