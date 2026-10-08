using FineManagement.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FineManagement.Api.Data;

/// <summary>
/// 应用数据库上下文。集中定义表结构、字段约束、关系、索引及逻辑删除过滤器。
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AssessmentRecord> AssessmentRecords => Set<AssessmentRecord>();
    public DbSet<AssessmentRecordChange> AssessmentRecordChanges => Set<AssessmentRecordChange>();
    public DbSet<AssessmentImportBatch> AssessmentImportBatches => Set<AssessmentImportBatch>();
    public DbSet<AssessmentImportRow> AssessmentImportRows => Set<AssessmentImportRow>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Departments", table => table.HasComment("部门信息表"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasComment("部门主键");
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired().HasComment("部门名称");
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired().HasComment("部门编码");
            entity.Property(x => x.SortOrder).HasComment("显示排序，数值越小越靠前");
            entity.Property(x => x.IsEnabled).HasComment("是否启用");
            entity.Property(x => x.Manager).HasMaxLength(50).HasComment("部门负责人");
            entity.Property(x => x.Remark).HasMaxLength(300).HasComment("备注");
            ConfigureAuditColumns(entity);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.IsDeleted);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.ToTable("PaymentMethods", table => table.HasComment("收款方式表"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasComment("收款方式主键");
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired().HasComment("收款方式名称");
            entity.Property(x => x.SortOrder).HasComment("显示排序，数值越小越靠前");
            entity.Property(x => x.IsEnabled).HasComment("是否启用");
            entity.Property(x => x.Description).HasMaxLength(300).HasComment("收款方式说明");
            ConfigureAuditColumns(entity);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.IsDeleted);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users", table => table.HasComment("系统用户表"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasComment("用户主键");
            entity.Property(x => x.Username).HasMaxLength(50).IsRequired().HasComment("登录用户名");
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired().HasComment("ASP.NET Identity 密码哈希");
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired().HasComment("用户姓名");
            entity.Property(x => x.Phone).HasMaxLength(30).HasComment("联系电话");
            entity.Property(x => x.Role).HasMaxLength(50).IsRequired().HasComment("系统角色");
            entity.Property(x => x.IsEnabled).HasComment("是否允许登录");
            entity.Property(x => x.LastLoginAt).HasComment("最后登录时间（UTC）");
            ConfigureAuditColumns(entity);
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.IsDeleted);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });

        modelBuilder.Entity<AssessmentRecord>(entity =>
        {
            entity.ToTable("AssessmentRecords", table => table.HasComment("正式考核记录表"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasComment("考核记录主键");
            entity.Property(x => x.AssessmentDateType).HasConversion<string>().HasMaxLength(20).IsRequired()
                .HasComment("单据日期类型：Fixed固定日期，Range日期区间");
            entity.Property(x => x.AssessmentDate)
                .HasConversion(
                    date => date.ToDateTime(TimeOnly.MinValue),
                    date => DateOnly.FromDateTime(date))
                .HasColumnType("date")
                .IsRequired()
                .HasComment("考核开始日期或固定日期");
            entity.Property(x => x.AssessmentEndDate)
                .HasConversion(new ValueConverter<DateOnly?, DateTime?>(
                    date => date.HasValue ? date.Value.ToDateTime(TimeOnly.MinValue) : null,
                    date => date.HasValue ? DateOnly.FromDateTime(date.Value) : null))
                .HasColumnType("date")
                .HasComment("考核结束日期，仅日期区间类型使用");
            entity.Property(x => x.DetailDate)
                .HasConversion(
                    date => date.ToDateTime(TimeOnly.MinValue),
                    date => DateOnly.FromDateTime(date))
                .HasColumnType("date")
                .IsRequired()
                .HasComment("考核明细到达财务并登记的日期");
            entity.Property(x => x.DepartmentId).HasComment("所属部门主键");
            entity.Property(x => x.PersonName).HasMaxLength(50).IsRequired().HasComment("被考核人员姓名，允许多个姓名");
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)").HasComment("考核总金额");
            entity.Property(x => x.Reason).HasMaxLength(300).IsRequired().HasComment("考核事由");
            entity.Property(x => x.Remark).HasMaxLength(500).HasComment("备注");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20)
                .HasComment("处理状态：Pending待处理，Completed已完成");
            entity.Property(x => x.PaymentMethodId).HasComment("收款方式主键，设置后视为已完成");
            entity.Property(x => x.CreatedByUserId).HasComment("创建记录的用户主键");
            ConfigureAuditColumns(entity);
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

        modelBuilder.Entity<AssessmentRecordChange>(entity =>
        {
            entity.ToTable("AssessmentRecordChanges", table => table.HasComment("考核记录操作及字段变更历史表"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasComment("变更日志主键");
            entity.Property(x => x.AssessmentRecordId).HasComment("所属考核记录主键");
            entity.Property(x => x.Action).HasMaxLength(20).IsRequired().HasComment("操作类型：Created、Updated、Imported、Deleted");
            entity.Property(x => x.ActorUserId).HasComment("操作用户主键快照");
            entity.Property(x => x.ActorName).HasMaxLength(100).IsRequired().HasComment("操作人显示名称快照");
            entity.Property(x => x.OccurredAt).HasComment("操作时间（UTC）");
            entity.Property(x => x.ChangesJson).HasColumnType("longtext").IsRequired().HasComment("字段变更明细 JSON");
            entity.HasIndex(x => new { x.AssessmentRecordId, x.OccurredAt });
            entity.HasOne(x => x.Record).WithMany().HasForeignKey(x => x.AssessmentRecordId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssessmentImportBatch>(entity =>
        {
            entity.ToTable("AssessmentImportBatches", table => table.HasComment("Excel 导入批次表"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasComment("导入批次主键");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20)
                .HasComment("批次状态：Pending待确认，Confirmed已确认，Cancelled已取消");
            entity.Property(x => x.CreatedByUserId).HasComment("发起导入的用户主键");
            entity.Property(x => x.ConfirmedAt).HasComment("确认导入时间（UTC）");
            ConfigureAuditColumns(entity);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.IsDeleted);
            entity.HasQueryFilter(x => !x.IsDeleted);
            entity.HasOne(x => x.CreatedByUser).WithMany()
                .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AssessmentImportRow>(entity =>
        {
            entity.ToTable("AssessmentImportRows", table => table.HasComment("Excel 导入待确认明细表"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasComment("暂存明细主键");
            entity.Property(x => x.BatchId).HasComment("所属导入批次主键");
            entity.Property(x => x.SourceRowNumber).HasComment("原 Excel 行号，手工添加时为0");
            entity.Property(x => x.AssessmentDateType).HasConversion<string>().HasMaxLength(20).IsRequired()
                .HasComment("单据日期类型：Fixed固定日期，Range日期区间");
            entity.Property(x => x.AssessmentDate)
                .HasConversion(
                    date => date.ToDateTime(TimeOnly.MinValue),
                    date => DateOnly.FromDateTime(date))
                .HasColumnType("date")
                .IsRequired()
                .HasComment("考核开始日期或固定日期");
            entity.Property(x => x.AssessmentEndDate)
                .HasConversion(new ValueConverter<DateOnly?, DateTime?>(
                    date => date.HasValue ? date.Value.ToDateTime(TimeOnly.MinValue) : null,
                    date => date.HasValue ? DateOnly.FromDateTime(date.Value) : null))
                .HasColumnType("date")
                .HasComment("考核结束日期，仅日期区间类型使用");
            entity.Property(x => x.DetailDate)
                .HasConversion(
                    date => date.ToDateTime(TimeOnly.MinValue),
                    date => DateOnly.FromDateTime(date))
                .HasColumnType("date")
                .IsRequired()
                .HasComment("当前明细分组对应的录入日期");
            entity.Property(x => x.DepartmentId).HasComment("已匹配的部门主键，未识别时为空");
            entity.Property(x => x.DepartmentName).HasMaxLength(100).IsRequired().HasComment("Excel 中识别出的部门名称");
            entity.Property(x => x.PersonName).HasMaxLength(50).IsRequired().HasComment("被考核人员姓名，允许多个姓名");
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)").HasComment("考核总金额");
            entity.Property(x => x.Reason).HasMaxLength(300).IsRequired().HasComment("考核事由");
            entity.Property(x => x.Remark).HasMaxLength(500).HasComment("备注");
            entity.Property(x => x.PaymentMethodId).HasComment("已匹配的收款方式主键");
            ConfigureAuditColumns(entity);
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
        // 将 EF 的删除状态改为更新状态，统一保留历史数据并记录删除时间。
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

    private static void ConfigureAuditColumns<TEntity>(EntityTypeBuilder<TEntity> entity)
        where TEntity : class, ISoftDeletable
    {
        entity.Property<DateTime>("CreatedAt").HasComment("创建时间（UTC）");
        entity.Property(x => x.UpdatedAt).HasComment("最后更新时间（UTC）");
        entity.Property(x => x.IsDeleted).HasComment("逻辑删除标记");
        entity.Property(x => x.DeletedAt).HasComment("逻辑删除时间（UTC）");
    }
}
