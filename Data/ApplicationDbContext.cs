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
        public DbSet<Models.FarmPayment> FarmPayments { get; set; }
        public DbSet<Models.FarmAccountAdjustment> FarmAccountAdjustments { get; set; }
        public DbSet<Models.OutsideCustomer> OutsideCustomers { get; set; }
        public DbSet<Models.OutsideEntry> OutsideEntries { get; set; }
        public DbSet<Models.StripCustomer> StripCustomers { get; set; }
        public DbSet<Models.TreasuryEntry> TreasuryEntries { get; set; }
        public DbSet<Models.ExpenseEntry> ExpenseEntries { get; set; }
        public DbSet<Models.StripInventoryEntry> StripInventoryEntries { get; set; }
        public DbSet<Models.Employee> Employees { get; set; }
        public DbSet<Models.EmployeeAdvance> EmployeeAdvances { get; set; }
        public DbSet<Models.EmployeeCustody> EmployeeCustodies { get; set; }
        public DbSet<Models.EmployeeCustodyMovement> EmployeeCustodyMovements { get; set; }
        public DbSet<Models.PayrollEntry> PayrollEntries { get; set; }
        public DbSet<Models.FridgeEntry> FridgeEntries { get; set; }
        public DbSet<Models.CapitalAdjustment> CapitalAdjustments { get; set; }
        public DbSet<Models.CapitalSetting> CapitalSettings { get; set; }

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
                entity.Property(e => e.FarmAdditionStatement).HasMaxLength(500);
                entity.Property(e => e.FarmDiscountStatement).HasMaxLength(500);

                entity.HasMany(e => e.Transactions)
                    .WithOne(t => t.FarmMealBatch)
                    .HasForeignKey(t => t.FarmMealBatchId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Models.FarmPayment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FarmName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Statement).IsRequired().HasMaxLength(500);
                entity.HasIndex(e => new { e.FarmName, e.Date, e.Id });
                entity.HasIndex(e => e.TreasuryEntryId)
                    .IsUnique()
                    .HasFilter("[TreasuryEntryId] IS NOT NULL");
                entity.HasOne(e => e.TreasuryEntry)
                    .WithMany()
                    .HasForeignKey(e => e.TreasuryEntryId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Models.FarmAccountAdjustment>(entity =>
            {
                entity.HasKey(adjustment => adjustment.Id);
                entity.Property(adjustment => adjustment.FarmName).IsRequired().HasMaxLength(200);
                entity.Property(adjustment => adjustment.Statement).IsRequired().HasMaxLength(500);
                entity.HasIndex(adjustment => new { adjustment.FarmName, adjustment.Date, adjustment.Id });
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
                entity.HasIndex(e => e.TransactionId);

                entity.HasOne(e => e.Transaction)
                    .WithMany(t => t.TreasuryEntries)
                    .HasForeignKey(e => e.TransactionId)
                    .OnDelete(DeleteBehavior.SetNull);
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

                entity.HasIndex(e => e.PayrollEntryId)
                    .IsUnique()
                    .HasFilter("[PayrollEntryId] IS NOT NULL");

                entity.HasOne(e => e.PayrollEntry)
                    .WithOne(e => e.ExpenseEntry)
                    .HasForeignKey<Models.ExpenseEntry>(e => e.PayrollEntryId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.TreasuryEntryId)
                    .IsUnique()
                    .HasFilter("[TreasuryEntryId] IS NOT NULL");

                entity.HasOne(e => e.TreasuryEntry)
                    .WithMany()
                    .HasForeignKey(e => e.TreasuryEntryId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Models.Employee>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.JobTitle).HasMaxLength(100);
                entity.Property(e => e.Phone).HasMaxLength(20);
                entity.HasIndex(e => e.Name);
            });

            modelBuilder.Entity<Models.EmployeeAdvance>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Employee)
                    .WithMany(e => e.Advances)
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.PayrollEntry)
                    .WithMany(e => e.Advances)
                    .HasForeignKey(e => e.PayrollEntryId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(e => new { e.EmployeeId, e.Date });
                entity.HasIndex(e => e.PayrollEntryId);
            });

            modelBuilder.Entity<Models.EmployeeCustody>(entity =>
            {
                entity.HasKey(custody => custody.Id);
                entity.Property(custody => custody.Statement).IsRequired().HasMaxLength(500);
                entity.HasIndex(custody => new { custody.EmployeeId, custody.Date, custody.Id });
                entity.HasIndex(custody => custody.TreasuryEntryId)
                    .IsUnique()
                    .HasFilter("[TreasuryEntryId] IS NOT NULL");
                entity.HasOne(custody => custody.Employee)
                    .WithMany(employee => employee.Custodies)
                    .HasForeignKey(custody => custody.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(custody => custody.TreasuryEntry)
                    .WithMany()
                    .HasForeignKey(custody => custody.TreasuryEntryId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Models.EmployeeCustodyMovement>(entity =>
            {
                entity.HasKey(movement => movement.Id);
                entity.Property(movement => movement.Statement).IsRequired().HasMaxLength(500);
                entity.HasIndex(movement => new { movement.EmployeeCustodyId, movement.Date, movement.Id });
                entity.HasOne(movement => movement.EmployeeCustody)
                    .WithMany(custody => custody.Movements)
                    .HasForeignKey(movement => movement.EmployeeCustodyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Models.PayrollEntry>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Employee)
                    .WithMany(e => e.PayrollEntries)
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.EmployeeId, e.Year, e.Month }).IsUnique();
                entity.HasIndex(e => new { e.Year, e.Month });
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

            modelBuilder.Entity<Models.CapitalAdjustment>(entity =>
            {
                entity.HasKey(adjustment => adjustment.Id);
                entity.Property(adjustment => adjustment.Statement).IsRequired().HasMaxLength(500);
                entity.HasIndex(adjustment => new { adjustment.Date, adjustment.Id });
            });

            modelBuilder.Entity<Models.CapitalSetting>(entity =>
            {
                entity.HasKey(setting => setting.Id);
                entity.Property(setting => setting.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<Models.FridgeEntry>(entity =>
            {
                entity.HasKey(entry => entry.Id);
                entity.Property(entry => entry.Statement).IsRequired().HasMaxLength(500);
                entity.HasIndex(entry => new { entry.ClientId, entry.Date, entry.Id });
                entity.HasIndex(entry => entry.StripInventoryEntryId)
                    .IsUnique()
                    .HasFilter("[StripInventoryEntryId] IS NOT NULL");

                entity.HasOne(entry => entry.Client)
                    .WithMany(client => client.FridgeEntries)
                    .HasForeignKey(entry => entry.ClientId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(entry => entry.StripInventoryEntry)
                    .WithMany()
                    .HasForeignKey(entry => entry.StripInventoryEntryId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(entry => entry.StripCustomer)
                    .WithMany()
                    .HasForeignKey(entry => entry.StripCustomerId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(entry => entry.TreasuryEntry)
                    .WithMany()
                    .HasForeignKey(entry => entry.TreasuryEntryId)
                    .OnDelete(DeleteBehavior.SetNull);
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
