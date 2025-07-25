using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace PriceComparison.Infrastructure.Models;

public partial class PriceComparisonDbContext : DbContext
{
    public PriceComparisonDbContext()
    {
    }

    public PriceComparisonDbContext(DbContextOptions<PriceComparisonDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Chain> Chains { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<Store> Stores { get; set; }

    public virtual DbSet<StorePrice> StorePrices { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Server=DESKTOP-FVDE6H2;Database=PriceComparisonDB;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Categori__3214EC07947530F9");

            entity.Property(e => e.CategoryName).HasMaxLength(200);
            entity.Property(e => e.FullPath).HasMaxLength(1000);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Level).HasDefaultValue(1);

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent)
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("FK__Categorie__Paren__4222D4EF");
        });

        modelBuilder.Entity<Chain>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Chains__3214EC07316C643C");

            entity.HasIndex(e => e.ChainId, "UQ__Chains__AB20BAABE5FFFAFB").IsUnique();

            entity.Property(e => e.ChainId).HasMaxLength(50);
            entity.Property(e => e.ChainName).HasMaxLength(200);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Products__3214EC0726D57119");

            entity.HasIndex(e => e.ProductId, "UQ__Products__B40CC6CC04CED706").IsUnique();

            entity.Property(e => e.Barcode).HasMaxLength(50);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsWeighted).HasDefaultValue(false);
            entity.Property(e => e.ManufacturerName).HasMaxLength(200);
            entity.Property(e => e.ProductId).HasMaxLength(50);
            entity.Property(e => e.ProductName).HasMaxLength(500);
            entity.Property(e => e.QtyInPackage).HasColumnType("decimal(10, 4)");
            entity.Property(e => e.UnitOfMeasure).HasMaxLength(100);

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK__Products__Catego__48CFD27E");
        });

        modelBuilder.Entity<Store>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Stores__3214EC076AD821F1");

            entity.HasIndex(e => new { e.ChainId, e.StoreId }, "UQ_Stores_ChainStore").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.BikoretNo).HasMaxLength(10);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.StoreId).HasMaxLength(50);
            entity.Property(e => e.StoreName).HasMaxLength(200);
            entity.Property(e => e.SubChainId).HasMaxLength(50);
            entity.Property(e => e.SubChainName).HasMaxLength(200);

            entity.HasOne(d => d.Chain).WithMany(p => p.Stores)
                .HasForeignKey(d => d.ChainId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Stores__ChainId__3D5E1FD2");
        });

        modelBuilder.Entity<StorePrice>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StorePri__3214EC078BA7BC63");

            entity.HasIndex(e => new { e.StoreId, e.ProductId }, "UQ_StorePrices_StoreProduct").IsUnique();

            entity.Property(e => e.AllowDiscount).HasDefaultValue(true);
            entity.Property(e => e.CurrentPrice).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.FirstSeen).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ItemCode).HasMaxLength(50);
            entity.Property(e => e.ItemStatus).HasDefaultValue(1);
            entity.Property(e => e.LastUpdated).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.StockQuantity).HasMaxLength(50);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(10, 4)");

            entity.HasOne(d => d.Product).WithMany(p => p.StorePrices)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StorePric__Produ__5165187F");

            entity.HasOne(d => d.Store).WithMany(p => p.StorePrices)
                .HasForeignKey(d => d.StoreId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__StorePric__Store__5070F446");


        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Users__3214EC07");

            // אינדקסים
            entity.HasIndex(e => e.Phone, "IX_Users_Phone")
                  .HasFilter("([Phone] IS NOT NULL)");
            entity.HasIndex(e => e.Email, "IX_Users_Email")
                  .HasFilter("([Email] IS NOT NULL)");
            entity.HasIndex(e => e.IsActive, "IX_Users_IsActive");

            // הגדרות עמודות
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("(getdate())");

            // Constraint - לפחות טלפון או אימייל
            entity.HasCheckConstraint("CK_Users_PhoneOrEmail",
                "([Phone] IS NOT NULL AND [Phone] != '') OR ([Email] IS NOT NULL AND [Email] != '')");
        });


        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
