using AttendanceSystem.Data;
using AttendanceSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public static class SeedData
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        IWebHostEnvironment env)
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        const string adminEmail = "waleedzidan@gmail.com";
        const string adminPassword = "0104016023ww";

        const string employeeCode1 = "EMP-1025";
        const string employeeCode2 = "EMP-1026";

        // =====================================================
        // Check if the complete seed already exists
        // =====================================================

        var adminExists =
            await userManager.FindByEmailAsync(adminEmail) is not null;

        var employee1Exists =
            await db.Employees.AnyAsync(x =>
                x.EmployeeCode == employeeCode1);

        var employee2Exists =
            await db.Employees.AnyAsync(x =>
                x.EmployeeCode == employeeCode2);

        var siteExists =
            await db.WorkSites.AnyAsync(x =>
                x.Name == "Main Site");

        // لو كل بيانات الـSeed موجودة خلاص اخرج
        if (adminExists &&
            employee1Exists &&
            employee2Exists &&
            siteExists)
        {
            return;
        }

        // =====================================================
        // Admin Role
        // =====================================================

        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            var roleResult =
                await roleManager.CreateAsync(
                    new IdentityRole<Guid>("Admin"));

            if (!roleResult.Succeeded)
            {
                throw new Exception(
                    "Unable to create Admin role: " +
                    string.Join(", ",
                        roleResult.Errors.Select(x => x.Description)));
            }
        }

        // =====================================================
        // Admin User
        // =====================================================

        var admin =
            await userManager.FindByEmailAsync(adminEmail);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Admin",
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var createResult =
                await userManager.CreateAsync(
                    admin,
                    adminPassword);

            if (!createResult.Succeeded)
            {
                throw new Exception(
                    "Unable to create admin: " +
                    string.Join(", ",
                        createResult.Errors.Select(x => x.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            var addRoleResult =
                await userManager.AddToRoleAsync(
                    admin,
                    "Admin");

            if (!addRoleResult.Succeeded)
            {
                throw new Exception(
                    "Unable to add Admin role: " +
                    string.Join(", ",
                        addRoleResult.Errors.Select(x => x.Description)));
            }
        }

        // =====================================================
        // Work Site
        // =====================================================

        var site =
            await db.WorkSites
                .FirstOrDefaultAsync(x =>
                    x.Name == "Main Site");

        if (site is null)
        {
            site = new WorkSite
            {
                Id = Guid.NewGuid(),
                Name = "Main Site",
                Description = "Default production site",
                Latitude = 24.0889000m,
                Longitude = 32.8998000m,
                AllowedRadiusMeters = 100,
                MaxAllowedAccuracyMeters = 50,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.WorkSites.Add(site);

            await db.SaveChangesAsync();
        }

        // =====================================================
        // Employees
        // =====================================================

        if (!await db.Employees.AnyAsync(x =>
                x.EmployeeCode == employeeCode1))
        {
            db.Employees.Add(
                new Employee
                {
                    Id = Guid.NewGuid(),
                    EmployeeCode = employeeCode1,
                    FullName = "Ahmed Mohamed",
                    WorkSiteId = site.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
        }

        if (!await db.Employees.AnyAsync(x =>
                x.EmployeeCode == employeeCode2))
        {
            db.Employees.Add(
                new Employee
                {
                    Id = Guid.NewGuid(),
                    EmployeeCode = employeeCode2,
                    FullName = "Mohamed Ali",
                    WorkSiteId = site.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
        }

        await db.SaveChangesAsync();
    }
}