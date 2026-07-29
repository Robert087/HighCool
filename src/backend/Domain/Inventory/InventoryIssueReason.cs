namespace ERP.Domain.Inventory;

public enum InventoryIssueReason
{
    InternalConsumption = 1,
    Damage = 2,
    Scrap = 3,
    Sample = 4,
    Maintenance = 5,
    BranchUse = 6,
    Other = 7
}
