using GovErp.Domain.ChartOfAccounts.Entities;
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Validation.Entities;
using CoaControl = GovErp.Domain.ChartOfAccounts.Entities.BudgetControlMode;
using LedgerControl = GovErp.Domain.Ledger.Entities.BudgetControlMode;

namespace GovErp.Infrastructure.Seed;

/// <summary>Seed Springfield (spec §2.2–2.5). Каждый вызов Create() — новые объекты: тесты не делят изменяемое состояние.</summary>
public sealed class SpringfieldData
{
    public static readonly UserId ClerkId = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    public static readonly UserId SystemUserId = new(Guid.Parse("10000000-0000-0000-0000-0000000000ff"));
    public static readonly Guid AcmeId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid ShadyId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid DormantId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly DateOnly Jun15 = new(2026, 6, 15);
    public static readonly DateOnly Jul15 = new(2026, 7, 15);
    private static readonly DateOnly Fy2026Start = new(2025, 7, 1);
    private static readonly DateTimeOffset LoadedAt = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    public List<Fund> Funds { get; } = [];
    public List<Department> Departments { get; } = [];
    public List<ObjectCodeDefinition> Objects { get; } = [];
    public List<Grant> Grants { get; } = [];
    public List<AccountCombination> Combinations { get; } = [];
    public List<OpeningBalance> OpeningBalances { get; } = [];
    public List<BudgetLine> BudgetLines { get; } = [];
    public List<Encumbrance> Encumbrances { get; } = [];
    public List<PurchaseOrder> PurchaseOrders { get; } = [];
    public List<Vendor> Vendors { get; } = [];
    public List<FiscalPeriod> Periods { get; } = [];
    public List<RuleDefinition> Rules { get; } = [];

    private static DepartmentCode D(string c) => new(c);
    private static ObjectCode O(string c) => new(c);
    private static AccountCode A(string c) => AccountCode.Parse(c);

    public static SpringfieldData Create()
    {
        var d = new SpringfieldData();
        ObjectCode[] spend = [O("53100"), O("54000"), O("55000")];

        d.Funds.AddRange(
        [
            new Fund(new FundCode("101"), "General Fund", FundType.Governmental, AccountingBasis.ModifiedAccrual, CoaControl.Soft, GrantPolicy.Forbidden, [], []),
            new Fund(new FundCode("202"), "Street Fund", FundType.Governmental, AccountingBasis.ModifiedAccrual, CoaControl.Hard, GrantPolicy.Forbidden, [D("4000")], spend),
            new Fund(new FundCode("501"), "Water Enterprise Fund", FundType.Enterprise, AccountingBasis.FullAccrual, CoaControl.Soft, GrantPolicy.Forbidden, [D("5000")], []),
            new Fund(new FundCode("701"), "Grants Fund", FundType.Governmental, AccountingBasis.ModifiedAccrual, CoaControl.Hard, GrantPolicy.Required, [], []),
        ]);
        d.Departments.AddRange(
        [
            new Department(D("0000"), "Balance sheet"), new Department(D("3000"), "Police"), new Department(D("4000"), "Public Works"),
            new Department(D("5000"), "Water Utility"), new Department(D("6000"), "Fire"),
        ]);
        d.Objects.AddRange(
        [
            new ObjectCodeDefinition(O("53100"), "Professional Services", ObjectCategory.Expenditure),
            new ObjectCodeDefinition(O("54000"), "Supplies", ObjectCategory.Expenditure),
            new ObjectCodeDefinition(O("55000"), "Capital Outlay", ObjectCategory.Expenditure),
            new ObjectCodeDefinition(O("2100"), "Accounts Payable", ObjectCategory.Liability),
            new ObjectCodeDefinition(O("1010"), "Cash", ObjectCategory.Asset),
            new ObjectCodeDefinition(O("2900"), "Reserve for Encumbrances", ObjectCategory.Budgetary),
            new ObjectCodeDefinition(O("5900"), "Encumbrances", ObjectCategory.Budgetary),
        ]);
        d.Grants.AddRange(
        [
            new Grant(new GrantCode("G-COPS-26"), "COPS program (exercise assumption)", "Federal sponsor (assumed)", isFederal: true,
                new DatePeriod(Fy2026Start, new DateOnly(2027, 6, 30)), [D("3000"), D("6000")], [O("53100"), O("54000")]),
            new Grant(new GrantCode("G-FEMA-24"), "Closed grant (demo)", "Federal sponsor (assumed)", isFederal: true,
                new DatePeriod(new DateOnly(2023, 7, 1), new DateOnly(2025, 6, 30)), [], [], GrantStatus.Closed),
        ]);

        string[] active =
        [
            "701-6000-53100-G-COPS-26", "701-3000-53100-G-COPS-26", "701-3000-54000-G-COPS-26",
            "101-6000-53100", "202-4000-53100", "501-5000-53100",
            "101-0000-2100", "202-0000-2100", "501-0000-2100", "701-0000-2100",
            "701-0000-2900-G-COPS-26", "701-3000-5900-G-COPS-26", "701-6000-5900-G-COPS-26",
        ];
        foreach (var code in active)
        {
            var c = AccountCombination.Request(A(code), Fy2026Start, SystemUserId, LoadedAt, CombinationSource.Generated);
            c.Approve(SystemUserId, LoadedAt);
            d.Combinations.Add(c);
        }

        var fema = AccountCombination.Request(A("701-6000-53100-G-FEMA-24"), new DateOnly(2023, 7, 1), SystemUserId, LoadedAt, CombinationSource.Generated);
        fema.Approve(SystemUserId, LoadedAt);
        fema.Deactivate(new DateOnly(2025, 6, 30));
        d.Combinations.Add(fema);

        var fy = new FiscalYear(2026);
        void Budget(string account, decimal adopted, decimal actuals, decimal encumbered, LedgerControl mode)
        {
            var opening = new OpeningBalance(A(account), fy, new DateOnly(2026, 6, 1), Money.Of(actuals), Money.Of(encumbered), "FY2026 opening load (demo)");
            var line = new BudgetLine(A(account), fy, mode, Money.Of(adopted));
            line.ApplyOpeningBalance(opening);
            d.OpeningBalances.Add(opening);
            d.BudgetLines.Add(line);
        }

        Budget("701-6000-53100-G-COPS-26", 375_000m, 132_000m, 96_000m, LedgerControl.Hard);
        Budget("701-3000-53100-G-COPS-26", 500_000m, 100_000m, 160_000m, LedgerControl.Hard);
        Budget("101-6000-53100", 50_000m, 40_000m, 0m, LedgerControl.Soft);
        Budget("202-4000-53100", 25_000m, 5_000m, 0m, LedgerControl.Hard);
        Budget("501-5000-53100", 60_000m, 30_000m, 0m, LedgerControl.Soft);

        d.Vendors.AddRange(
        [
            new Vendor(AcmeId, "ACME", "Acme Consulting", VendorStatus.Active, samRegistered: true),
            new Vendor(ShadyId, "SHADY", "Shady LLC", VendorStatus.Debarred, samRegistered: false),
            new Vendor(DormantId, "DORMANT", "Dormant Supply", VendorStatus.Inactive, samRegistered: true),
        ]);

        // Утверждённая сумма PO-строки копируется в Encumbrance один раз (GE-17).
        void Po(string number, string account, decimal amount)
        {
            d.PurchaseOrders.Add(new PurchaseOrder(number, AcmeId, [new PurchaseOrderLine(1, A(account), Money.Of(amount))]));
            d.Encumbrances.Add(new Encumbrance($"{number}/1", A(account), Money.Of(amount), Money.Of(amount), Money.Zero));
        }

        Po("PO-2026-0449", "701-6000-53100-G-COPS-26", 36_000m);
        Po("PO-2026-0450", "701-6000-53100-G-COPS-26", 60_000m);
        Po("PO-2026-0451", "701-3000-53100-G-COPS-26", 160_000m);

        for (var m = 0; m < 12; m++)
        {
            var date = Fy2026Start.AddMonths(m);
            var period = new FiscalPeriod(date.Year, date.Month);
            if (date < new DateOnly(2026, 6, 1))
            {
                period.Close();   // открыт только июнь 2026 — месяц демо-даты
            }

            d.Periods.Add(period);
        }

        d.Rules.AddRange(RuleSeed.All(Fy2026Start));
        return d;
    }

    private static VendorInvoice Draft(string number, decimal total, string? poRef, DateOnly date, params (string Account, decimal Amount, int? PoLine)[] lines)
    {
        var invoice = new VendorInvoice(number, AcmeId, date, date, date, date.AddDays(30), Money.Of(total), poRef, ClerkId, LoadedAt);
        foreach (var (account, amount, poLine) in lines)
        {
            invoice.AddDistribution(A(account), Money.Of(amount), poLine);
        }

        return invoice;
    }

    public VendorInvoice NonPoExerciseInvoice(string number = "V-7781", DateOnly? date = null) =>
        Draft(number, 160_000m, null, date ?? Jun15, ("701-6000-53100-G-COPS-26", 160_000m, null));

    public VendorInvoice PoBackedInvoice(decimal amount = 160_000m, string number = "V-0451-1") =>
        Draft(number, amount, "PO-2026-0451", Jun15, ("701-3000-53100-G-COPS-26", amount, 1));

    public VendorInvoice MultiFundInvoice(string number = "V-MF-1") =>
        Draft(number, 30_000m, null, Jun15, ("101-6000-53100", 12_000m, null), ("202-4000-53100", 8_000m, null), ("501-5000-53100", 10_000m, null));
}
