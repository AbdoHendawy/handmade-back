using Handmade.Application.Abstractions.Persistence;
using Handmade.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Infrastructure.Persistence;

public sealed class HandmadeDbContext : DbContext, IApplicationDbContext
{
    public HandmadeDbContext(DbContextOptions<HandmadeDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HandmadeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
