using System;
using AttendanceSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260829160000_AddWorkerDeviceVerification")]
    public partial class AddWorkerDeviceVerification : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RequireWorkerDeviceVerification = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "attendance_authorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptType = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_authorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_attendance_authorizations_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_enrollment_authorizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_enrollment_authorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_enrollment_authorizations_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "employee_webauthn_credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<byte[]>(type: "bytea", nullable: false),
                    PublicKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    UserHandle = table.Column<byte[]>(type: "bytea", nullable: false),
                    SignCount = table.Column<long>(type: "bigint", nullable: false),
                    CredentialType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_webauthn_credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_employee_webauthn_credentials_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webauthn_flow_states",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentAuthorizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OptionsJson = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webauthn_flow_states", x => x.Id);
                    table.ForeignKey(
                        name: "FK_webauthn_flow_states_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "system_settings",
                columns: new[] { "Id", "RequireWorkerDeviceVerification", "UpdatedAt" },
                values: new object[] { 1, false, new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_authorizations_EmployeeId_ExpiresAtUtc",
                table: "attendance_authorizations",
                columns: new[] { "EmployeeId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_authorizations_TokenHash",
                table: "attendance_authorizations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_enrollment_authorizations_EmployeeId_ExpiresAtUtc",
                table: "device_enrollment_authorizations",
                columns: new[] { "EmployeeId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_device_enrollment_authorizations_TokenHash",
                table: "device_enrollment_authorizations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_webauthn_credentials_CredentialId",
                table: "employee_webauthn_credentials",
                column: "CredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_employee_active_webauthn_credential",
                table: "employee_webauthn_credentials",
                column: "EmployeeId",
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_webauthn_flow_states_EmployeeId_ExpiresAtUtc",
                table: "webauthn_flow_states",
                columns: new[] { "EmployeeId", "ExpiresAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "attendance_authorizations");
            migrationBuilder.DropTable(name: "device_enrollment_authorizations");
            migrationBuilder.DropTable(name: "employee_webauthn_credentials");
            migrationBuilder.DropTable(name: "system_settings");
            migrationBuilder.DropTable(name: "webauthn_flow_states");
        }
    }
}
