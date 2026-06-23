namespace ERP.Application.Common.Security;

/// <summary>
/// Canonical permission codes (module.action). Mirrors the RBAC matrix in docs/10.
/// Verticals will extend their own nested classes as features land.
/// </summary>
public static class Permissions
{
    public const string DashboardView = "dashboard.view";
    public const string ReportGenerate = "report.generate";
    public const string AccountingView = "accounting.view";

    public static class Platform
    {
        public const string ReadAll = "platform.read.all";
    }

    public static class Business
    {
        public const string Manage = "business.manage";
        public const string MembersManage = "business.members.manage";
    }

    public static class Users
    {
        public const string Manage = "user.manage";
    }

    public static class Employee
    {
        public const string Manage = "employee.manage";
        public const string AttendanceMark = "employee.attendance.mark";
    }

    public static class Expense
    {
        public const string Manage = "expense.manage";
    }

    public static class Customer
    {
        public const string Manage = "customer.manage";
        public const string CollectionRecord = "customer.collection.record";
    }

    public static class Transport
    {
        public const string VehicleManage = "transport.vehicle.manage";
        public const string DriverManage = "transport.driver.manage";
        public const string LoadCreate = "transport.load.create";
        public const string LoadView = "transport.load.view";
        public const string CreditManage = "transport.credit.manage";
    }

    public static class Cctv
    {
        public const string ItemManage = "cctv.item.manage";
        public const string PoCreate = "cctv.po.create";
        public const string PoApprove = "cctv.po.approve";
        public const string SaleCreate = "cctv.sale.create";
        public const string ServiceManage = "cctv.service.manage";
    }

    public static class Farm
    {
        public const string BatchManage = "farm.batch.manage";
        public const string FeedRecord = "farm.feed.record";
        public const string MedicalRecord = "farm.medical.record";
        public const string WalletManage = "farm.wallet.manage";
    }

    public static class Coconut
    {
        public const string BatchManage = "coconut.batch.manage";
        public const string ChargeRecord = "coconut.charge.record";
    }
}
