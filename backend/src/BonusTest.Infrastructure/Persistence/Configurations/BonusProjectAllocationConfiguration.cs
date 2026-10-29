using BonusTest.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BonusTest.Infrastructure.Persistence.Configurations;

public class BonusProjectAllocationConfiguration : IEntityTypeConfiguration<BonusProjectAllocation>
{
    public void Configure(EntityTypeBuilder<BonusProjectAllocation> builder)
    {
        builder.ToTable("BonusProjectAllocation");
        builder.HasKey(x => x.BonusProjectAllocationId);
        builder.Property(x => x.ProjectName).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.Hours).HasPrecision(8, 2);
        builder.Property(x => x.GrossAmount).HasPrecision(18, 2);
        builder.HasOne<Bonus>().WithMany(x => x.ProjectAllocations)
            .HasForeignKey(x => x.BonusId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<EmployeeProjectHours>().WithMany()
            .HasForeignKey(x => x.EmployeeProjectHoursId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => new { x.BonusId, x.EmployeeProjectHoursId }).IsUnique();
    }
}
