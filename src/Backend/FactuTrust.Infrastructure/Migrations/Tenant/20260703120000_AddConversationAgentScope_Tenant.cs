using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant;

/// <summary>
/// Assistants experts par module : colonne AgentScope sur Conversations (0 = assistant global,
/// valeur des conversations existantes → sémantique préservée). Sert au cloisonnement des historiques.
/// </summary>
[DbContext(typeof(TenantDbContext))]
[Migration("20260703120000_AddConversationAgentScope_Tenant")]
public partial class AddConversationAgentScope_Tenant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AgentScope",
            table: "Conversations",
            type: "int",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AgentScope",
            table: "Conversations");
    }
}
