using System;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MasterDbContext))]
    [Migration("20260802160000_AddFirmManagedClients_Master")]
    public partial class AddFirmManagedClients_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ManagedByFirmTenantId",
                table: "Tenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ManagedByFirmTenantId",
                table: "Tenants",
                column: "ManagedByFirmTenantId",
                filter: "[ManagedByFirmTenantId] IS NOT NULL");

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                table: "FirmClientAssignments",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Origin",
                table: "FirmClientAssignments");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_ManagedByFirmTenantId",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ManagedByFirmTenantId",
                table: "Tenants");
        }
    }
}
