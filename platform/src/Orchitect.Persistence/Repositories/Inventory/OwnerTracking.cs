using Microsoft.EntityFrameworkCore;
using Orchitect.Domain.Inventory.Identity;

namespace Orchitect.Persistence.Repositories.Inventory;

internal static class OwnerTracking
{
    /// <summary>
    /// Returns the tracked or stored owner matching <paramref name="owner"/>, updated with its values, or adds <paramref name="owner"/> when none exists.
    /// </summary>
    public static async Task<User> TrackOwnerAsync(
        this OrchitectDbContext context,
        User owner,
        CancellationToken cancellationToken = default)
    {
        var trackedOwner = context.Owners.Local.FirstOrDefault(
            o => o.OrganisationId == owner.OrganisationId &&
                 o.Name == owner.Name &&
                 o.Platform == owner.Platform);

        trackedOwner ??= await context.Owners.FirstOrDefaultAsync(
            o => o.OrganisationId == owner.OrganisationId &&
                 o.Name == owner.Name &&
                 o.Platform == owner.Platform,
            cancellationToken);

        if (trackedOwner is null)
        {
            context.Owners.Add(owner);
            return owner;
        }

        if (!ReferenceEquals(trackedOwner, owner))
        {
            context.Entry(trackedOwner).CurrentValues.SetValues(owner);
        }

        return trackedOwner;
    }
}
