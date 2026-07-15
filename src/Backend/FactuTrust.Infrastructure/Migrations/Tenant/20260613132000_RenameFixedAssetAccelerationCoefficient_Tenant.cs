using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <summary>
    /// Renomme FixedAssets.DegressiveCoefficient en AccelerationCoefficient.
    /// </summary>
    public partial class RenameFixedAssetAccelerationCoefficient_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DegressiveCoefficient",
                table: "FixedAssets",
                newName: "AccelerationCoefficient");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AccelerationCoefficient",
                table: "FixedAssets",
                newName: "DegressiveCoefficient");
        }
    }
}