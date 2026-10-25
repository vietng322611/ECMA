using ECMA.Domain.Entities;
using ECMA.Infrastructure.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ECMA.Infrastructure.Data;

public sealed class EcmaDbContext(DbContextOptions<EcmaDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<AgendaItem> AgendaItems => Set<AgendaItem>();
    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new EventConfiguration());
        modelBuilder.ApplyConfiguration(new AgendaItemConfiguration());
        modelBuilder.ApplyConfiguration(new RegistrationConfiguration());
    }
}