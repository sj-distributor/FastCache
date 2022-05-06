using Microsoft.EntityFrameworkCore;
using TestApi.Entity;

namespace TestApi.DB;

public class MemoryDbContext : DbContext
{
    public MemoryDbContext(DbContextOptions<MemoryDbContext> options) : base(options)
    {
        
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseInMemoryDatabase($"user");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        typeof(MemoryDbContext).Assembly.GetTypes()
            .Where(x => typeof(IEntity).IsAssignableFrom(x) && x.IsClass)
            .ToList()
            .ForEach(x =>
            {
                modelBuilder.Model.AddEntityType(x);
            });
    }
}