using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenOrderSystem.Core.Migrations.V2
{
    /// <inheritdoc />
    public partial class InitialV2Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Configuration",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuration", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "CustomerRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NotificationPreferences = table.Column<int>(type: "int", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Flags = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Devices_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Devices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MediaGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortPriority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaGroups_MediaGroups_ParentId",
                        column: x => x.ParentId,
                        principalTable: "MediaGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OptionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortPriority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OptionGroups_OptionGroups_ParentId",
                        column: x => x.ParentId,
                        principalTable: "OptionGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OrderStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreviousStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NextStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AutoCompleteTime = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Flags = table.Column<int>(type: "int", nullable: false),
                    IsProtected = table.Column<bool>(type: "bit", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStages_OrderStages_NextStageId",
                        column: x => x.NextStageId,
                        principalTable: "OrderStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderStages_OrderStages_PreviousStageId",
                        column: x => x.PreviousStageId,
                        principalTable: "OrderStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortPriority = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductGroups_ProductGroups_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ProductGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VariantGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SortPriority = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VariantGroups_VariantGroups_ParentId",
                        column: x => x.ParentId,
                        principalTable: "VariantGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceBypassTokens",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssuedById = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUsedApiCallerIp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUsedClientReportedIp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceBypassTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceBypassTokens_AspNetUsers_IssuedById",
                        column: x => x.IssuedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceConfig",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceConfig", x => new { x.DeviceId, x.Key });
                    table.ForeignKey(
                        name: "FK_DeviceConfig_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Media",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Filepath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Extension = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MediaType = table.Column<int>(type: "int", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Hash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Media", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Media_MediaGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "MediaGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PriceDelta = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Flags = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Options_OptionGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "OptionGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<long>(type: "bigint", nullable: false),
                    OrderDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CurrentStageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrderComments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StageAdvanceTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Flags = table.Column<int>(type: "int", nullable: false),
                    AssignedTimeSlot = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OrderOrigin = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_CustomerRecords_CustomerRecordId",
                        column: x => x.CustomerRecordId,
                        principalTable: "CustomerRecords",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Orders_OrderStages_CurrentStageId",
                        column: x => x.CurrentStageId,
                        principalTable: "OrderStages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Keywords = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoverMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AlbumId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Flags = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_MediaGroups_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "MediaGroups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Products_Media_CoverMediaId",
                        column: x => x.CoverMediaId,
                        principalTable: "Media",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Products_ProductGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ProductGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OrderQueue",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderHeaderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledFor = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderQueue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderQueue_Orders_OrderHeaderId",
                        column: x => x.OrderHeaderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderStageHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderHeaderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExitedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStageHistory", x => new { x.OrderHeaderId, x.Id });
                    table.ForeignKey(
                        name: "FK_OrderStageHistory_Orders_OrderHeaderId",
                        column: x => x.OrderHeaderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductOptions",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefaultState = table.Column<int>(type: "int", nullable: false),
                    PriceDeltaOverride = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOptions", x => new { x.ProductId, x.OptionId });
                    table.ForeignKey(
                        name: "FK_ProductOptions_Options_OptionId",
                        column: x => x.OptionId,
                        principalTable: "Options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductOptions_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSnapshots_ProductSnapshots_PreviousSnapshotId",
                        column: x => x.PreviousSnapshotId,
                        principalTable: "ProductSnapshots",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductSnapshots_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Variants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StrikePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Sku = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BarcodeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Variants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Variants_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Variants_VariantGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "VariantGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderHeaderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VariantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    LineComments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SelectedOptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderLines_Orders_OrderHeaderId",
                        column: x => x.OrderHeaderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderLines_ProductSnapshots_ProductSnapshotId",
                        column: x => x.ProductSnapshotId,
                        principalTable: "ProductSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_ParentId",
                table: "Devices",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "UIX_DeviceHead_Key",
                table: "Devices",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceBypassTokens_IssuedById",
                table: "MaintenanceBypassTokens",
                column: "IssuedById");

            migrationBuilder.CreateIndex(
                name: "IX_Media_GroupId",
                table: "Media",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaGroups_ParentId",
                table: "MediaGroups",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_OptionGroups_ParentId",
                table: "OptionGroups",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Options_GroupId",
                table: "Options",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderHeaderId",
                table: "OrderLines",
                column: "OrderHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_ProductId",
                table: "OrderLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_ProductSnapshotId",
                table: "OrderLines",
                column: "ProductSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderQueue_OrderHeaderId",
                table: "OrderQueue",
                column: "OrderHeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderQueue_ScheduledFor",
                table: "OrderQueue",
                column: "ScheduledFor");

            migrationBuilder.CreateIndex(
                name: "IX_OrderHeader_AssignedTimeSlot",
                table: "Orders",
                column: "AssignedTimeSlot");

            migrationBuilder.CreateIndex(
                name: "IX_OrderHeader_StageAdvanceTime",
                table: "Orders",
                column: "StageAdvanceTime");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CurrentStageId",
                table: "Orders",
                column: "CurrentStageId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerRecordId",
                table: "Orders",
                column: "CustomerRecordId");

            migrationBuilder.CreateIndex(
                name: "UIX_OrderHeader_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderStages_NextStageId",
                table: "OrderStages",
                column: "NextStageId",
                unique: true,
                filter: "[NextStageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStages_PreviousStageId",
                table: "OrderStages",
                column: "PreviousStageId",
                unique: true,
                filter: "[PreviousStageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductGroups_ParentId",
                table: "ProductGroups",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductOptions_OptionId",
                table: "ProductOptions",
                column: "OptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_AlbumId",
                table: "Products",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CoverMediaId",
                table: "Products",
                column: "CoverMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_GroupId",
                table: "Products",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSnapshots_PreviousSnapshotId",
                table: "ProductSnapshots",
                column: "PreviousSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSnapshots_ProductId",
                table: "ProductSnapshots",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_VariantGroups_ParentId",
                table: "VariantGroups",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Variants_GroupId",
                table: "Variants",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Variants_ProductId",
                table: "Variants",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Configuration");

            migrationBuilder.DropTable(
                name: "DeviceConfig");

            migrationBuilder.DropTable(
                name: "MaintenanceBypassTokens");

            migrationBuilder.DropTable(
                name: "OrderLines");

            migrationBuilder.DropTable(
                name: "OrderQueue");

            migrationBuilder.DropTable(
                name: "OrderStageHistory");

            migrationBuilder.DropTable(
                name: "ProductOptions");

            migrationBuilder.DropTable(
                name: "Variants");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "ProductSnapshots");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Options");

            migrationBuilder.DropTable(
                name: "VariantGroups");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "CustomerRecords");

            migrationBuilder.DropTable(
                name: "OrderStages");

            migrationBuilder.DropTable(
                name: "OptionGroups");

            migrationBuilder.DropTable(
                name: "Media");

            migrationBuilder.DropTable(
                name: "ProductGroups");

            migrationBuilder.DropTable(
                name: "MediaGroups");
        }
    }
}
