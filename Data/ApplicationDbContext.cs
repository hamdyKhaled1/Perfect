using Microsoft.EntityFrameworkCore;

namespace HagImamAccounts.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Models.Client> Clients { get; set; }
        public DbSet<Models.Transaction> Transactions { get; set; }
        public DbSet<Models.FarmMealBatch> FarmMealBatches { get; set; }
        public DbSet<Models.OutsideCustomer> OutsideCustomers { get; set; }
        public DbSet<Models.OutsideEntry> OutsideEntries { get; set; }
        public DbSet<Models.StripCustomer> StripCustomers { get; set; }
        public DbSet<Models.TreasuryEntry> TreasuryEntries { get; set; }
        public DbSet<Models.ExpenseEntry> ExpenseEntries { get; set; }
        public DbSet<Models.StripInventoryEntry> StripInventoryEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Client configuration
            modelBuilder.Entity<Models.Client>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Phone).HasMaxLength(20);
                entity.HasIndex(e => e.Name);
            });

            // Transaction configuration
            modelBuilder.Entity<Models.Transaction>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Client)
                    .WithMany(c => c.Transactions)
                    .HasForeignKey(e => e.ClientId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ClientId);
                entity.HasIndex(e => e.Date);
                entity.HasIndex(e => e.FarmMealBatchId);
            });

            modelBuilder.Entity<Models.FarmMealBatch>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasMany(e => e.Transactions)
                    .WithOne(t => t.FarmMealBatch)
                    .HasForeignKey(t => t.FarmMealBatchId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Models.OutsideCustomer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Phone).HasMaxLength(20);
            });

            modelBuilder.Entity<Models.OutsideEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Statement).IsRequired().HasMaxLength(500);
                entity.HasOne(e => e.Customer)
                    .WithMany(c => c.Entries)
                    .HasForeignKey(e => e.OutsideCustomerId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(e => new { e.OutsideCustomerId, e.Date });
            });

            modelBuilder.Entity<Models.TreasuryEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Statement).IsRequired().HasMaxLength(500);
                entity.HasIndex(e => new { e.Account, e.Date, e.Id });
                entity.HasIndex(e => e.TransferId);
            });

            modelBuilder.Entity<Models.ExpenseEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Statement).IsRequired().HasMaxLength(500);
                entity.HasIndex(e => e.FarmMealBatchId)
                    .IsUnique()
                    .HasFilter("[FarmMealBatchId] IS NOT NULL");
                entity.HasOne(e => e.FarmMealBatch)
                    .WithMany()
                    .HasForeignKey(e => e.FarmMealBatchId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Models.StripInventoryEntry>(entity =>
            {
                entity.HasKey(entry => entry.Id);
                entity.Property(entry => entry.ReceivedBy).IsRequired().HasMaxLength(200);
                entity.Property(entry => entry.Statement).IsRequired().HasMaxLength(500);
                entity.HasIndex(entry => new { entry.CartonType, entry.Date, entry.Id });
                entity.HasOne(entry => entry.Customer)
                    .WithMany(customer => customer.Entries)
                    .HasForeignKey(entry => entry.StripCustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Models.StripCustomer>(entity =>
            {
                entity.HasKey(customer => customer.Id);
                entity.Property(customer => customer.Name).IsRequired().HasMaxLength(200);
                entity.Property(customer => customer.Phone).HasMaxLength(20);
                entity.HasIndex(customer => customer.Name);
            });

            // Seed some sample data
            modelBuilder.Entity<Models.Client>().HasData(
                new Models.Client
                {
                    Id = 1,
                    Name = "أحمد محمد",
                    Phone = "01012345678",
                    CreatedAt = new DateTime(2026, 1, 1),
                    IsActive = true
                },
                new Models.Client
                {
                    Id = 2,
                    Name = "محمود علي",
                    Phone = "01098765432",
                    CreatedAt = new DateTime(2026, 1, 1),
                    IsActive = true
                },
                new Models.Client
                {
                    Id = 3,
                    Name = "حسن إبراهيم",
                    Phone = "01155555555",
                    CreatedAt = new DateTime(2026, 1, 1),
                    IsActive = true
                }
            );
        }
    }
}
