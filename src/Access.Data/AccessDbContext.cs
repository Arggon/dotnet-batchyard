using Microsoft.EntityFrameworkCore;

namespace Access.Data;

/// <summary>EF Core maps the tables created by the DbUp scripts in db/access (ADR 0007); it owns no migrations.</summary>
public sealed class AccessDbContext(DbContextOptions<AccessDbContext> options) : DbContext(options)
{
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<Assignment> Assignments => Set<Assignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Plan>(e =>
        {
            e.ToTable("Plans");
            e.Property(p => p.Id).ValueGeneratedNever();
            e.HasMany(p => p.Roles).WithMany().UsingEntity("PlanRoles",
                r => r.HasOne(typeof(Role)).WithMany().HasForeignKey("RoleId"),
                l => l.HasOne(typeof(Plan)).WithMany().HasForeignKey("PlanId"));
        });
        modelBuilder.Entity<Permission>(e =>
        {
            e.ToTable("Permissions");
            e.Property(p => p.Id).ValueGeneratedNever();
        });
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.Property(r => r.Id).ValueGeneratedNever();
            e.HasMany(r => r.Permissions).WithMany().UsingEntity("RolePermissions",
                r => r.HasOne(typeof(Permission)).WithMany().HasForeignKey("PermissionId"),
                l => l.HasOne(typeof(Role)).WithMany().HasForeignKey("RoleId"));
        });
        modelBuilder.Entity<Tenant>(e =>
        {
            e.ToTable("Tenants");
            e.Property(t => t.CreatedAt).HasColumnType("datetime2(3)");
        });
        modelBuilder.Entity<ApiClient>(e =>
        {
            e.ToTable("ApiClients");
            e.Property(c => c.CreatedAt).HasColumnType("datetime2(3)");
            e.HasMany(c => c.Assignments).WithOne().HasForeignKey(a => a.ClientId);
        });
        modelBuilder.Entity<Assignment>(e =>
        {
            e.ToTable("Assignments");
            e.HasKey(a => new { a.ClientId, a.RoleId });
        });
    }
}
