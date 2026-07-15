using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    public partial class FixDeliveryNoteColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add PropertyCode if it doesn't exist
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DeliveryNoteLines')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[DeliveryNoteLines]') AND name = 'ProductCode')
                    BEGIN
                        ALTER TABLE [DeliveryNoteLines] ADD [ProductCode] nvarchar(50) NOT NULL DEFAULT N'';
                    END
                END
            ");

            // Add UnitPriceHT if it doesn't exist
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DeliveryNoteLines')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[DeliveryNoteLines]') AND name = 'UnitPriceHT')
                    BEGIN
                        ALTER TABLE [DeliveryNoteLines] ADD [UnitPriceHT] decimal(18,3) NOT NULL DEFAULT 0;
                    END
                END
            ");

            // Add VatRatePercent if it doesn't exist
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DeliveryNoteLines')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[DeliveryNoteLines]') AND name = 'VatRatePercent')
                    BEGIN
                        ALTER TABLE [DeliveryNoteLines] ADD [VatRatePercent] int NOT NULL DEFAULT 0;
                    END
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty
        }
    }
}
