using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GovErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "coa");

            migrationBuilder.EnsureSchema(
                name: "ledger");

            migrationBuilder.EnsureSchema(
                name: "validation");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "ap");

            migrationBuilder.CreateTable(
                name: "AccountCombinations",
                schema: "coa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountCombinations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BudgetLines",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Account = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    ControlMode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Adopted = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Actuals = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Encumbered = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ChangeStamp = table.Column<long>(type: "bigint", nullable: false),
                    OpeningBalanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                schema: "coa",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Encumbrances",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PoLineRef = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Account = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Original = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Liquidated = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Released = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AuthorizedPoAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AlreadyPostedAgainstPo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ChangeStamp = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Encumbrances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationRecords",
                schema: "validation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TransactionVersion = table.Column<int>(type: "int", nullable: false),
                    ApprovalCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EvaluatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EvaluatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleSetVersions = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RuleFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Outcomes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Overall = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Capabilities = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Steps = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovalRoute = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PostingPreview = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PostingCheck = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReadyForPaymentHandoff = table.Column<bool>(type: "bit", nullable: false),
                    InputSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Actor = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubjectRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Explanations",
                schema: "validation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvaluationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionRef = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PromptVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FallbackReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Explanations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FiscalPeriods",
                schema: "ledger",
                columns: table => new
                {
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalPeriods", x => new { x.Year, x.Month });
                });

            migrationBuilder.CreateTable(
                name: "Funds",
                schema: "coa",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Basis = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ControlMode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    GrantPolicy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AllowedDepartments = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AllowedObjects = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Funds", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Grants",
                schema: "coa",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sponsor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsFederal = table.Column<bool>(type: "bit", nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: true),
                    AllowedDepartments = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AllowableObjects = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grants", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    PeriodMonth = table.Column<int>(type: "int", nullable: false),
                    PostedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ObjectCodes",
                schema: "coa",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectCodes", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "OpeningBalances",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Account = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InitialActuals = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InitialEncumbered = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningBalances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedCommands",
                schema: "ap",
                columns: table => new
                {
                    CommandId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommandType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedCommands", x => x.CommandId);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                schema: "ap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuleDefinitions",
                schema: "validation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Step = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Layer = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ScopeFund = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    ScopeGrant = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Parameters = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OverridableBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Resolution = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuleDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorInvoices",
                schema: "ap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PostingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PoRef = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContentVersion = table.Column<int>(type: "int", nullable: false),
                    ApprovalCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastEvaluationRef = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RuleFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PaymentHold = table.Column<bool>(type: "bit", nullable: false),
                    ChangeStamp = table.Column<long>(type: "bigint", nullable: false),
                    ReservationRefs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EncumbranceClaimRefs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PoBillingClaimRefs = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NormalizedNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, computedColumnSql: "UPPER(TRIM([Number]))", stored: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    ApprovalRoute = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorInvoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vendors",
                schema: "ap",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SamRegistered = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BudgetAmendments",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BudgetLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetAmendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetAmendments_BudgetLines_BudgetLineId",
                        column: x => x.BudgetLineId,
                        principalSchema: "ledger",
                        principalTable: "BudgetLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BudgetReservations",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentVersion = table.Column<int>(type: "int", nullable: false),
                    SourceRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BudgetLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BudgetReservations_BudgetLines_BudgetLineId",
                        column: x => x.BudgetLineId,
                        principalSchema: "ledger",
                        principalTable: "BudgetLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EncumbranceClaims",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentVersion = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EncumbranceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncumbranceClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncumbranceClaims_Encumbrances_EncumbranceId",
                        column: x => x.EncumbranceId,
                        principalSchema: "ledger",
                        principalTable: "Encumbrances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EncumbranceLiquidations",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EncumbranceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncumbranceLiquidations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncumbranceLiquidations_Encumbrances_EncumbranceId",
                        column: x => x.EncumbranceId,
                        principalSchema: "ledger",
                        principalTable: "Encumbrances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PoBillingClaims",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentVersion = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EncumbranceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoBillingClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PoBillingClaims_Encumbrances_EncumbranceId",
                        column: x => x.EncumbranceId,
                        principalSchema: "ledger",
                        principalTable: "Encumbrances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JournalLines",
                schema: "ledger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Account = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Family = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalLines_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "ledger",
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLines",
                schema: "ap",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    Account = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalSchema: "ap",
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceApprovals",
                schema: "ap",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EvaluationRef = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentVersion = table.Column<int>(type: "int", nullable: false),
                    ApprovalCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceApprovals_VendorInvoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "ap",
                        principalTable: "VendorInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceDistributions",
                schema: "ap",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    Account = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PoLineNo = table.Column<int>(type: "int", nullable: true),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceDistributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceDistributions_VendorInvoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "ap",
                        principalTable: "VendorInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceOverrides",
                schema: "ap",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Target = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceOverrides_VendorInvoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "ap",
                        principalTable: "VendorInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceWithdrawals",
                schema: "ap",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApprovalCycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceWithdrawals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceWithdrawals_VendorInvoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalSchema: "ap",
                        principalTable: "VendorInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountCombinations_Code",
                schema: "coa",
                table: "AccountCombinations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetAmendments_BudgetLineId",
                schema: "ledger",
                table: "BudgetAmendments",
                column: "BudgetLineId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetLines_Account_FiscalYear",
                schema: "ledger",
                table: "BudgetLines",
                columns: new[] { "Account", "FiscalYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetReservations_BudgetLineId",
                schema: "ledger",
                table: "BudgetReservations",
                column: "BudgetLineId");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetReservations_InvoiceId",
                schema: "ledger",
                table: "BudgetReservations",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_EncumbranceClaims_EncumbranceId",
                schema: "ledger",
                table: "EncumbranceClaims",
                column: "EncumbranceId");

            migrationBuilder.CreateIndex(
                name: "IX_EncumbranceClaims_InvoiceId",
                schema: "ledger",
                table: "EncumbranceClaims",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_EncumbranceLiquidations_EncumbranceId",
                schema: "ledger",
                table: "EncumbranceLiquidations",
                column: "EncumbranceId");

            migrationBuilder.CreateIndex(
                name: "IX_Encumbrances_PoLineRef",
                schema: "ledger",
                table: "Encumbrances",
                column: "PoLineRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationRecords_TransactionRef",
                schema: "validation",
                table: "EvaluationRecords",
                column: "TransactionRef");

            migrationBuilder.CreateIndex(
                name: "IX_Events_SubjectRef",
                schema: "audit",
                table: "Events",
                column: "SubjectRef");

            migrationBuilder.CreateIndex(
                name: "IX_Explanations_EvaluationId",
                schema: "validation",
                table: "Explanations",
                column: "EvaluationId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceApprovals_InvoiceId",
                schema: "ap",
                table: "InvoiceApprovals",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDistributions_InvoiceId",
                schema: "ap",
                table: "InvoiceDistributions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceOverrides_InvoiceId",
                schema: "ap",
                table: "InvoiceOverrides",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceWithdrawals_InvoiceId",
                schema: "ap",
                table: "InvoiceWithdrawals",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_SourceRef",
                schema: "ledger",
                table: "JournalEntries",
                column: "SourceRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalLines_JournalEntryId",
                schema: "ledger",
                table: "JournalLines",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_OpeningBalances_Account_FiscalYear",
                schema: "ledger",
                table: "OpeningBalances",
                columns: new[] { "Account", "FiscalYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PoBillingClaims_EncumbranceId",
                schema: "ledger",
                table: "PoBillingClaims",
                column: "EncumbranceId");

            migrationBuilder.CreateIndex(
                name: "IX_PoBillingClaims_InvoiceId",
                schema: "ledger",
                table: "PoBillingClaims",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_PurchaseOrderId",
                schema: "ap",
                table: "PurchaseOrderLines",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Number",
                schema: "ap",
                table: "PurchaseOrders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuleDefinitions_RuleId_Layer_Version_ScopeFund_ScopeGrant",
                schema: "validation",
                table: "RuleDefinitions",
                columns: new[] { "RuleId", "Layer", "Version", "ScopeFund", "ScopeGrant" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_VendorInvoices_Vendor_Number",
                schema: "ap",
                table: "VendorInvoices",
                columns: new[] { "VendorId", "NormalizedNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_Code",
                schema: "ap",
                table: "Vendors",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountCombinations",
                schema: "coa");

            migrationBuilder.DropTable(
                name: "BudgetAmendments",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "BudgetReservations",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "Departments",
                schema: "coa");

            migrationBuilder.DropTable(
                name: "EncumbranceClaims",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "EncumbranceLiquidations",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "EvaluationRecords",
                schema: "validation");

            migrationBuilder.DropTable(
                name: "Events",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "Explanations",
                schema: "validation");

            migrationBuilder.DropTable(
                name: "FiscalPeriods",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "Funds",
                schema: "coa");

            migrationBuilder.DropTable(
                name: "Grants",
                schema: "coa");

            migrationBuilder.DropTable(
                name: "InvoiceApprovals",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "InvoiceDistributions",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "InvoiceOverrides",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "InvoiceWithdrawals",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "JournalLines",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "ObjectCodes",
                schema: "coa");

            migrationBuilder.DropTable(
                name: "OpeningBalances",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "PoBillingClaims",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "ProcessedCommands",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLines",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "RuleDefinitions",
                schema: "validation");

            migrationBuilder.DropTable(
                name: "Vendors",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "BudgetLines",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "VendorInvoices",
                schema: "ap");

            migrationBuilder.DropTable(
                name: "JournalEntries",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "Encumbrances",
                schema: "ledger");

            migrationBuilder.DropTable(
                name: "PurchaseOrders",
                schema: "ap");
        }
    }
}
