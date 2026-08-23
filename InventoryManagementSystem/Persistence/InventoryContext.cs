using InventoryManagementSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementSystem.Persistence;

public class InventoryContext : DbContext
{
    public InventoryContext(DbContextOptions<InventoryContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Description).HasMaxLength(500);
            entity.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Sku).IsRequired().HasMaxLength(50);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Description).HasMaxLength(1000);
            entity.HasIndex(p => p.Sku).IsUnique();

            // Restrict, not cascade: deleting a category must not silently take its
            // products - and their entire stock history - with it.
            entity.HasOne(p => p.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.Property(m => m.Reason).HasMaxLength(500);

            // The aggregate that computes stock on hand filters by ProductId and
            // orders by OccurredAt, so index the pair rather than ProductId alone.
            entity.HasIndex(m => new { m.ProductId, m.OccurredAt });

            entity.HasOne(m => m.Product)
                  .WithMany(p => p.Movements)
                  .HasForeignKey(m => m.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Defence in depth. The service layer rejects a zero delta with a 400;
            // this stops one reaching the table by any other route.
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_StockMovement_QuantityDelta_NotZero",
                "QuantityDelta <> 0"));
        });
    }
}
