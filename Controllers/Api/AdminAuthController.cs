using AttendanceSystem.DTOs;
using AttendanceSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Controllers.Api;

[ApiController]
[Route("api/admin/auth")]
public class AdminAuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;

    public AdminAuthController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn)
    {
        _users = users;
        _signIn = signIn;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] AdminLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(
                new ApiResponse<object>(
                    false,
                    null,
                    "Email and password are required.",
                    "VALIDATION_ERROR"));
        }

        var email = request.Email.Trim();

        // يبحث في قاعدة البيانات
        // والـSeed Admin موجود فيها بالفعل
        var user = await _users.FindByEmailAsync(email);

        if (user is null)
        {
            return Unauthorized(
                new ApiResponse<object>(
                    false,
                    null,
                    "Invalid email or password.",
                    "UNAUTHORIZED"));
        }

        if (!user.IsActive)
        {
            return Unauthorized(
                new ApiResponse<object>(
                    false,
                    null,
                    "Account is inactive.",
                    "UNAUTHORIZED"));
        }

        var isAdmin =
            await _users.IsInRoleAsync(user, "Admin");

        if (!isAdmin)
        {
            return Unauthorized(
                new ApiResponse<object>(
                    false,
                    null,
                    "Admin access is required.",
                    "UNAUTHORIZED"));
        }

        var result =
            await _signIn.PasswordSignInAsync(
                user,
                request.Password,
                isPersistent: false,
                lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return Unauthorized(
                new ApiResponse<object>(
                    false,
                    null,
                    "Account is temporarily locked.",
                    "LOCKED_OUT"));
        }

        if (!result.Succeeded)
        {
            return Unauthorized(
                new ApiResponse<object>(
                    false,
                    null,
                    "Invalid email or password.",
                    "UNAUTHORIZED"));
        }

        return Ok(
            ApiResponse<AdminLoginResponse>.Ok(
                new AdminLoginResponse(user.FullName),
                "Login successful."));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();

        return Ok(
            new ApiResponse<object>(
                true,
                null,
                "Logout successful."));
    }
}