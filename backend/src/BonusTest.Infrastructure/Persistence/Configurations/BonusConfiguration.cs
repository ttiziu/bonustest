using BonusTest.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BonusTest.Infrastructure.Persistence.Configurations;

public class BonusConfiguration : IEntityTypeConfiguration<Bonus>
{
    public void Configure(EntityTypeBuilder<Bonus> builder)
    {
        builder.ToTable("Bonus");
        builder.HasKey(x => x.BonusId);
        builder.Property(x => x.DepartmentCode).HasColumnType("char(5)");
        builder.Property(x => x.StateCode).HasColumnType("char(4)");
        builder.Property(x => x.ReasonCode).HasColumnType("char(6)");
        builder.Property(x => x.Value).HasPrecision(18, 4);
        builder.Property(x => x.MonthlySalary).HasPrecision(16, 2);
        builder.Property(x => x.TaxRate).HasPrecision(5, 2);
        builder.Property(x => x.GrossAmount).HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.NetAmount).HasPrecision(18, 2);
        builder.Property(x => x.CreatedBy).HasMaxLength(100);
        builder.Property(x => x.ModifiedBy).HasMaxLength(100);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => new { x.DepartmentCode, x.EmployeeNumber }).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentCode).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.State).WithMany().HasForeignKey(x => x.StateCode).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.BonusType).WithMany().HasForeignKey(x => x.BonusTypeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Reason).WithMany().HasForeignKey(x => x.ReasonCode).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.DepartmentCode, x.EmployeeNumber, x.Period }).IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [StateCode] <> '0203'");
    }
}
