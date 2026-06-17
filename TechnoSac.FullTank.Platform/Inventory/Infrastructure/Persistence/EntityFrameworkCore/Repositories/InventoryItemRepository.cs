using TechnoSac.FullTank.Platform.Inventory.Domain.Model.Aggregates;
using TechnoSac.FullTank.Platform.Inventory.Domain.Repositories;
using TechnoSac.FullTank.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using TechnoSac.FullTank.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Microsoft.EntityFrameworkCore;

namespace TechnoSac.FullTank.Platform.Inventory.Infrastructure.Persistence.EntityFrameworkCore.Repositories;

public class InventoryItemRepository(AppDbContext context)
    : BaseRepository<InventoryItem>(context), IInventoryItemRepository
{
    public async Task<IEnumerable<InventoryItem>> FindByProviderIdAsync(int providerId,
        CancellationToken cancellationToken)
    {
        return await Context.Set<InventoryItem>()
            .Where(item => item.ProviderId == providerId)
            .ToListAsync(cancellationToken);
    }
}
