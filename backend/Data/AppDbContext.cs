using FineManagement.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FineManagement.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AssessmentRecord> AssessmentRecords => Set<AssessmentRecord>();
    public DbSet<AssessmentImportBatch> AssessmentImportBatches => Set<AssessmentImportBatch>();
    public DbSet<AssessmentImportRow> AssessmentImportRows => Set<AssessmentImportRow>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Departments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Manager).HasMaxLength(50);
            entity.Property(x => x.Remark).HasMaxLength(300);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.IsDeleted);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.ToTable("PaymentMethods");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.IsDeleted);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).HasMaxLength(50).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(30);
            entity.Property(x => x.Role).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.IsDeleted);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<AssessmentRecord>(entity =>
        {
            entity.ToTable("AssessmentRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AssessmentDateType).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.AssessmentDate)
                .HasConversion(
                    date => date.ToDateTime(TimeOnly.MinValue),
                    date => DateOnly.FromDateTime(date))
                .HasColumnType("date")
                .IsRequired();
            entity.Property(x => x.AssessmentEndDate)
                .HasConversion(new ValueConverter<DateOnly?, DateTime?>(
                    date => date.HasValue ? date.Value.ToDateTime(TimeOnly.MinValue) : null,
                    date => date.HasValue ? DateOnly.FromDateTime(date.Value) : null))
                .HasColumnType("date");
            entity.Property(x => x.DetailDate)
                .HasConversion(
                    date => date.ToDateTime(TimeOnly.MinValue),
                    date => DateOnly.FromDateTime(date))
                .HasColumnType("date")
                .IsRequired();
            entity.Property(x => x.PersonName).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Reason).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Remark).HasMaxLength(500);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(x => x.AssessmentDate);
            entity.HasIndex(x => x.DetailDate);
            entity.HasIndex(x => new { x.DepartmentId, x.AssessmentDate });
            entity.HasIndex(x => x.IsDeleted);
            entity.HasQueryFilter(x => !x.IsDeleted);
            entity.HasOne(x => x.Department).WithMany(x => x.AssessmentRecords)
                .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PaymentMethod).WithMany(x => x.AssessmentRecords)
                .HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.CreatedByUser).WithMany(x => x.CreatedAssessmentRecords)
                .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AssessmentImportBatch>(entity =>
        {
            entity.ToTable("AssessmentImportBatches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.IsDeleted);
            entity.HasQueryFilter(x => !x.IsDeleted);
            entity.HasOne(x => x.CreatedByUser).WithMany()
                .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AssessmentImportRow>(entity =>
        {
            entity.ToTable("AssessmentImportRows");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AssessmentDateType).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.AssessmentDate)
                .HasConversion(
                    date => date.ToDateTime(TimeOnly.MinValue),
                    date => DateOnly.FromDateTime(date))
                .HasColumnType("date")
                .IsRequired();
            entity.Property(x => x.AssessmentEndDate)
                .HasConversion(new ValueConverter<DateOnly?, DateTime?>(
                    date => date.HasValue ? date.Value.ToDateTime(TimeOnly.MinValue) : null,
                    date => date.HasValue ? DateOnly.FromDateTime(date.Value) : null))
                .HasColumnType("date");
            entity.Property(x => x.DetailDate)
                .HasConversion(
                    date => date.ToDateTime(TimeOnly.MinValue),
                    date => DateOnly.FromDateTime(date))
                .HasColumnType("date")
                .IsRequired();
            entity.Property(x => x.DepartmentName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PersonName).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Reason).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Remark).HasMaxLength(500);
            entity.HasIndex(x => new { x.BatchId, x.IsDeleted });
            entity.HasQueryFilter(x => !x.IsDeleted);
            entity.HasOne(x => x.Batch).WithMany(x => x.Rows)
                .HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Department).WithMany()
                .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PaymentMethod).WithMany()
                .HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ConvertDeletesToSoftDeletes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ConvertDeletesToSoftDeletes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ConvertDeletesToSoftDeletes()
    {
        var deletedEntries = ChangeTracker.Entries<ISoftDeletable>()
            .Where(entry => entry.State == EntityState.Deleted)
            .ToList();
        if (deletedEntries.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var entry in deletedEntries)
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = now;
            entry.Entity.UpdatedAt = now;
        }
    }
}
