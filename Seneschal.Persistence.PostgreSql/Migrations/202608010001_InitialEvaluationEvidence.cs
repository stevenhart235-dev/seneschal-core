using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Seneschal.Persistence.PostgreSql.Migrations;

public partial class InitialEvaluationEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "approvals",
            columns: table => new
            {
                id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                identity_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                capability_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                environment = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                resource_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                operation_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                correlation_mode = table.Column<int>(type: "integer", nullable: false),
                request_reason = table.Column<string>(type: "text", nullable: false),
                requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                resolved_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                consumed_by_decision_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_approvals", x => x.id));

        migrationBuilder.CreateTable(
            name: "evaluation_evidence",
            columns: table => new
            {
                id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                append_sequence = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                timestamp_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                identity_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                capability_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                environment = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                resource_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                decision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                effective_action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                approval_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                operation_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_evaluation_evidence", x => x.id));

        migrationBuilder.CreateIndex(name: "IX_approvals_scope",
            table: "approvals",
            columns: new[] { "identity_id", "capability_id", "environment", "resource_id", "operation_id" });
        migrationBuilder.CreateIndex(name: "IX_evaluation_evidence_append_sequence",
            table: "evaluation_evidence", column: "append_sequence", unique: true);
        migrationBuilder.CreateIndex(name: "IX_evaluation_evidence_capability_id",
            table: "evaluation_evidence", column: "capability_id");
        migrationBuilder.CreateIndex(name: "IX_evaluation_evidence_identity_id",
            table: "evaluation_evidence", column: "identity_id");
        migrationBuilder.CreateIndex(name: "IX_evaluation_evidence_timestamp_utc_append_sequence",
            table: "evaluation_evidence", columns: new[] { "timestamp_utc", "append_sequence" },
            descending: new[] { true, false });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "approvals");
        migrationBuilder.DropTable(name: "evaluation_evidence");
    }
}
