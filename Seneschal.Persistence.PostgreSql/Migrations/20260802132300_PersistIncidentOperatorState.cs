using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Seneschal.Persistence.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class PersistIncidentOperatorState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "incident_operator_state",
                columns: table => new
                {
                    incident_id = table.Column<string>(type: "character varying(73)", maxLength: 73, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_operator_state", x => x.incident_id);
                    table.CheckConstraint("CK_incident_operator_state_status", "status >= 0 AND status <= 2");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "incident_operator_state");
        }
    }
}
