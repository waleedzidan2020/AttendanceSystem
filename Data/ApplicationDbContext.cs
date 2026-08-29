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
    public DbSet<EmployeeWebAuthnCredential> EmployeeWebAuthnCredentials => Set<EmployeeWebAuthnCredential>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<DeviceEnrollmentAuthorization> DeviceEnrollmentAuthorizations => Set<DeviceEnrollmentAuthorization>();
    public DbSet<WebAuthnFlowState> WebAuthnFlowStates => Set<WebAuthnFlowState>();
    public DbSet<AttendanceAuthorization> AttendanceAuthorizations => Set<AttendanceAuthorization>();

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

        builder.Entity<EmployeeWebAuthnCredential>(entity =>
        {
            entity.ToTable("employee_webauthn_credentials");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CredentialId).IsRequired();
            entity.Property(x => x.PublicKey).IsRequired();
            entity.Property(x => x.UserHandle).IsRequired();
            entity.Property(x => x.CredentialType).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => x.CredentialId).IsUnique();
            entity.HasIndex(x => x.EmployeeId).IsUnique().HasFilter("\"IsActive\" = TRUE").HasDatabaseName("ux_employee_active_webauthn_credential");
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("system_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequireWorkerDeviceVerification).HasDefaultValue(false);
            entity.HasData(new SystemSetting { Id = 1, RequireWorkerDeviceVerification = false, UpdatedAt = new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc) });
        });

        builder.Entity<DeviceEnrollmentAuthorization>(entity =>
        {
            entity.ToTable("device_enrollment_authorizations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.EmployeeId, x.ExpiresAtUtc });
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WebAuthnFlowState>(entity =>
        {
            entity.ToTable("webauthn_flow_states");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Purpose).HasMaxLength(50).IsRequired();
            entity.Property(x => x.OptionsJson).HasColumnType("text").IsRequired();
            entity.HasIndex(x => new { x.EmployeeId, x.ExpiresAtUtc });
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AttendanceAuthorization>(entity =>
        {
            entity.ToTable("attendance_authorizations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AttemptType).HasConversion<int>();
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.EmployeeId, x.ExpiresAtUtc });
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
