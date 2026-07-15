using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactuTrust.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStorefrontModule_Master : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StorefrontOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuestFullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    GuestEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    GuestPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DeliveryStreet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeliveryStreetLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeliveryCity = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeliveryPostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DeliveryGovernorate = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeliveryCountry = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GuestNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IpAddressHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantDispatchResultsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorefrontOutboxInboxEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceVersion = table.Column<int>(type: "int", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontOutboxInboxEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorefrontProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Tagline = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DescriptionMarkdown = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    BrandPrimaryColorHex = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    BrandSecondaryColorHex = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    PublicLogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PublicCoverImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FacadeTheme = table.Column<int>(type: "int", nullable: false),
                    PublicContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PublicContactPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PublicContactWhatsApp = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    StreetPositionIndex = table.Column<int>(type: "int", nullable: true),
                    OrderSubmissionEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SuspensionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConsentVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ConsentAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsentAcceptedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorefrontProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StorefrontPublishingConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorefrontProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TermsVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IpAddressHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontPublishingConsents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorefrontOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorefrontOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorefrontProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitPriceAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorefrontOrderItems_StorefrontOrders_StorefrontOrderId",
                        column: x => x.StorefrontOrderId,
                        principalTable: "StorefrontOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StorefrontProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorefrontProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DescriptionSanitized = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PriceAmount = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PriceCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PublicImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImageHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CategoryLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StockDisplayStatus = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceVersion = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorefrontProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorefrontProducts_StorefrontProfiles_StorefrontProfileId",
                        column: x => x.StorefrontProfileId,
                        principalTable: "StorefrontProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontOrderItems_StorefrontOrderId",
                table: "StorefrontOrderItems",
                column: "StorefrontOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontOrderItems_TenantId",
                table: "StorefrontOrderItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontOrders_Status",
                table: "StorefrontOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontOrders_SubmittedAt",
                table: "StorefrontOrders",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontOutboxInboxEntries_TenantId_AggregateType_AggregateId_SourceVersion",
                table: "StorefrontOutboxInboxEntries",
                columns: new[] { "TenantId", "AggregateType", "AggregateId", "SourceVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontProducts_IsVisible_StorefrontProfileId",
                table: "StorefrontProducts",
                columns: new[] { "IsVisible", "StorefrontProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontProducts_StorefrontProfileId_Slug",
                table: "StorefrontProducts",
                columns: new[] { "StorefrontProfileId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontProducts_StorefrontProfileId_SourceProductId",
                table: "StorefrontProducts",
                columns: new[] { "StorefrontProfileId", "SourceProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontProfiles_Slug",
                table: "StorefrontProfiles",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontProfiles_Status_Category",
                table: "StorefrontProfiles",
                columns: new[] { "Status", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontProfiles_StreetPositionIndex",
                table: "StorefrontProfiles",
                column: "StreetPositionIndex",
                unique: true,
                filter: "[StreetPositionIndex] IS NOT NULL AND [Status] = 2");

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontProfiles_TenantId",
                table: "StorefrontProfiles",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontPublishingConsents_StorefrontProfileId",
                table: "StorefrontPublishingConsents",
                column: "StorefrontProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_StorefrontPublishingConsents_TenantId",
                table: "StorefrontPublishingConsents",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StorefrontOrderItems");

            migrationBuilder.DropTable(
                name: "StorefrontOutboxInboxEntries");

            migrationBuilder.DropTable(
                name: "StorefrontProducts");

            migrationBuilder.DropTable(
                name: "StorefrontPublishingConsents");

            migrationBuilder.DropTable(
                name: "StorefrontOrders");

            migrationBuilder.DropTable(
                name: "StorefrontProfiles");
        }
    }
}
