using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECMA.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "registrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    registered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancel_source = table.Column<int>(type: "integer", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    checked_in_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    checked_in_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_registrations", x => x.id);
                    table.CheckConstraint("ck_registrations_cancel_source", "cancel_source IS NULL OR cancel_source IN (0, 1, 2)");
                    table.CheckConstraint("ck_registrations_cancellation", "(status = 1) = (cancel_source IS NOT NULL AND cancelled_at IS NOT NULL AND cancelled_by IS NOT NULL AND cancel_reason IS NOT NULL) AND (status = 0) = (cancel_source IS NULL AND cancelled_at IS NULL AND cancelled_by IS NULL AND cancel_reason IS NULL)");
                    table.CheckConstraint("ck_registrations_check_in", "(checked_in_at IS NULL) = (checked_in_by IS NULL) AND (checked_in_at IS NULL OR status = 0)");
                    table.CheckConstraint("ck_registrations_code", "code ~ '^REG-[A-Z0-9]{12}$'");
                    table.CheckConstraint("ck_registrations_status", "status IN (0, 1)");
                    table.ForeignKey(
                        name: "fk_registrations_cancelled_by",
                        column: x => x.cancelled_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_registrations_checked_in_by",
                        column: x => x.checked_in_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_registrations_event",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_registrations_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_registrations_cancelled_by",
                table: "registrations",
                column: "cancelled_by");

            migrationBuilder.CreateIndex(
                name: "IX_registrations_checked_in_by",
                table: "registrations",
                column: "checked_in_by");

            migrationBuilder.CreateIndex(
                name: "ix_registrations_event_registered",
                table: "registrations",
                columns: new[] { "event_id", "registered_at" });

            migrationBuilder.CreateIndex(
                name: "ix_registrations_user_registered",
                table: "registrations",
                columns: new[] { "user_id", "registered_at" });

            migrationBuilder.CreateIndex(
                name: "uq_registrations_code",
                table: "registrations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_registrations_event_user",
                table: "registrations",
                columns: new[] { "event_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "registrations");
        }
    }
}
