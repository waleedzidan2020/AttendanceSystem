using AttendanceSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<WorkSite> WorkSites => Set<WorkSite>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<AttendanceAttempt> AttendanceAttempts => Set<AttendanceAttempt>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        });

        builder.Entity<WorkSite>(entity =>
        {
            entity.ToTable("work_sites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Latitude).HasPrecision(10, 7);
            entity.Property(x => x.Longitude).HasPrecision(10, 7);
            entity.HasIndex(x => x.Name);
        });

        builder.Entity<Employee>(entity =>
        {
            entity.ToTable("employees");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EmployeeCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(30);
            entity.HasIndex(x => x.EmployeeCode).IsUnique();
            entity.HasIndex(x => x.WorkSiteId);
            entity.HasOne(x => x.WorkSite).WithMany(x => x.Employees).HasForeignKey(x => x.WorkSiteId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AttendanceRecord>(entity =>
        {
            entity.ToTable("attendance_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CheckInLatitude).HasPrecision(10, 7);
            entity.Property(x => x.CheckInLongitude).HasPrecision(10, 7);
            entity.Property(x => x.CheckInAccuracyMeters).HasPrecision(10, 2);
            entity.Property(x => x.CheckInDistanceMeters).HasPrecision(10, 2);
            entity.Property(x => x.CheckOutLatitude).HasPrecision(10, 7);
            entity.Property(x => x.CheckOutLongitude).HasPrecision(10, 7);
            entity.Property(x => x.CheckOutAccuracyMeters).HasPrecision(10, 2);
            entity.Property(x => x.CheckOutDistanceMeters).HasPrecision(10, 2);
            entity.Property(x => x.Status).HasConversion<int>();
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.WorkSiteId);
            entity.HasIndex(x => x.AttendanceDate);
            entity.HasIndex(x => new { x.EmployeeId, x.AttendanceDate });
            entity.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("\"CheckOutTimeUtc\" IS NULL").HasDatabaseName("ux_employee_open_attendance");
            entity.HasOne(x => x.Employee).WithMany(x => x.AttendanceRecords).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.WorkSite).WithMany(x => x.AttendanceRecords).HasForeignKey(x => x.WorkSiteId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AttendanceAttempt>(entity =>
        {
            entity.ToTable("attendance_attempts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SubmittedEmployeeCode).HasMaxLength(50);
            entity.Property(x => x.Latitude).HasPrecision(10, 7);
            entity.Property(x => x.Longitude).HasPrecision(10, 7);
            entity.Property(x => x.AccuracyMeters).HasPrecision(10, 2);
            entity.Property(x => x.DistanceMeters).HasPrecision(10, 2);
            entity.Property(x => x.AttemptType).HasConversion<int>();
            entity.Property(x => x.RejectReason).HasConversion<int>();
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(500);
            entity.HasIndex(x => x.RequestId).IsUnique();
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.WorkSiteId);
            entity.HasIndex(x => x.AttemptTimeUtc);
            entity.HasIndex(x => x.Accepted);
            entity.HasOne(x => x.Employee).WithMany(x => x.AttendanceAttempts).HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.WorkSite).WithMany(x => x.AttendanceAttempts).HasForeignKey(x => x.WorkSiteId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
