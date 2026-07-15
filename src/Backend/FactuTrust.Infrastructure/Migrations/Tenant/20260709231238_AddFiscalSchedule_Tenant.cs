using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations.Tenant
{
    /// <inheritdoc />
    public partial class AddFiscalSchedule_Tenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FiscalScheduleEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObligationType = table.Column<int>(type: "int", nullable: false),
                    ObligationLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: true),
                    PeriodQuarter = table.Column<int>(type: "int", nullable: true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstimatedAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepositDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponsibleUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponsibleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Observations = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastReminderAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReminderChannel = table.Column<int>(type: "int", nullable: true),
                    IsCancelled = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalScheduleEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FiscalScheduleAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalScheduleEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalScheduleAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalScheduleAttachments_FiscalScheduleEntries_FiscalScheduleEntryId",
                        column: x => x.FiscalScheduleEntryId,
                        principalTable: "FiscalScheduleEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FiscalScheduleHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalScheduleEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OldValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalScheduleHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalScheduleHistoryEntries_FiscalScheduleEntries_FiscalScheduleEntryId",
                        column: x => x.FiscalScheduleEntryId,
                        principalTable: "FiscalScheduleEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalScheduleAttachments_FiscalScheduleEntryId",
                table: "FiscalScheduleAttachments",
                column: "FiscalScheduleEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalScheduleEntries_DueDate",
                table: "FiscalScheduleEntries",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalScheduleEntries_FiscalYear",
                table: "FiscalScheduleEntries",
                column: "FiscalYear");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalScheduleEntries_ObligationType",
                table: "FiscalScheduleEntries",
                column: "ObligationType");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalScheduleEntries_ObligationType_FiscalYear_PeriodMonth_PeriodQuarter_SourceType_IsCancelled",
                table: "FiscalScheduleEntries",
                columns: new[] { "ObligationType", "FiscalYear", "PeriodMonth", "PeriodQuarter", "SourceType", "IsCancelled" });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalScheduleEntries_ResponsibleUserId",
                table: "FiscalScheduleEntries",
                column: "ResponsibleUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalScheduleEntries_SourceType_SourceId",
                table: "FiscalScheduleEntries",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalScheduleHistoryEntries_CreatedAt",
                table: "FiscalScheduleHistoryEntries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalScheduleHistoryEntries_FiscalScheduleEntryId",
                table: "FiscalScheduleHistoryEntries",
                column: "FiscalScheduleEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiscalScheduleAttachments");

            migrationBuilder.DropTable(
                name: "FiscalScheduleHistoryEntries");

            migrationBuilder.DropTable(
                name: "FiscalScheduleEntries");
        }
    }
}
