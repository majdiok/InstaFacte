using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddRecurringEntryTemplates_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastRunAt",
                table: "JournalEntryTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRunDate",
                table: "JournalEntryTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceDayOfMonth",
                table: "JournalEntryTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecurrenceEndDate",
                table: "JournalEntryTemplates",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceFrequency",
                table: "JournalEntryTemplates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RecurrenceStartDate",
                table: "JournalEntryTemplates",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRunAt",
                table: "JournalEntryTemplates");

            migrationBuilder.DropColumn(
                name: "NextRunDate",
                table: "JournalEntryTemplates");

            migrationBuilder.DropColumn(
                name: "RecurrenceDayOfMonth",
                table: "JournalEntryTemplates");

            migrationBuilder.DropColumn(
                name: "RecurrenceEndDate",
                table: "JournalEntryTemplates");

            migrationBuilder.DropColumn(
                name: "RecurrenceFrequency",
                table: "JournalEntryTemplates");

            migrationBuilder.DropColumn(
                name: "RecurrenceStartDate",
                table: "JournalEntryTemplates");
        }
    }
}
