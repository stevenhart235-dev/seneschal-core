using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Seneschal.Persistence.PostgreSql.Migrations;

public partial class CompleteApprovalPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "version", table: "approvals", type: "bigint",
            nullable: false, defaultValue: 0L);
        migrationBuilder.AddCheckConstraint(
            name: "CK_approvals_status", table: "approvals",
            sql: "status >= 0 AND status <= 3");
        migrationBuilder.CreateIndex(
            name: "IX_approvals_status_requested_at_id",
            table: "approvals",
            columns: new[] { "status", "requested_at", "id" },
            descending: new[] { false, true, false });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_approvals_status_requested_at_id", table: "approvals");
        migrationBuilder.DropCheckConstraint(
            name: "CK_approvals_status", table: "approvals");
        migrationBuilder.DropColumn(name: "version", table: "approvals");
    }
}
