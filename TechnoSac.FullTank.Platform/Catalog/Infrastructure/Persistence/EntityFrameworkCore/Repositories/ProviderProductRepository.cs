using TechnoSac.FullTank.Platform.Catalog.Domain.Model.Aggregates;
using TechnoSac.FullTank.Platform.Catalog.Domain.Repositories;
using TechnoSac.FullTank.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using TechnoSac.FullTank.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using Microsoft.EntityFrameworkCore;

namespace TechnoSac.FullTank.Platform.Catalog.Infrastructure.Persistence.EntityFrameworkCore.Repositories;

public class ProviderProductRepository(AppDbContext context)
    : BaseRepository<ProviderProduct>(context), IProviderProductRepository
{
    public async Task<IEnumerable<ProviderProduct>> FindByProviderIdAsync(int providerId,
        CancellationToken cancellationToken)
    {
        return await Context.Set<ProviderProduct>()
            .Where(product => product.ProviderId == providerId)
            .ToListAsync(cancellationToken);
    }
}
