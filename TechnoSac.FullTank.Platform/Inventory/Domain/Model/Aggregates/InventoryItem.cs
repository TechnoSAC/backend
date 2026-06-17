using TechnoSac.FullTank.Platform.Inventory.Domain.Model.Commands;
using TechnoSac.FullTank.Platform.Shared.Domain.Model.Entities;

namespace TechnoSac.FullTank.Platform.Inventory.Domain.Model.Aggregates;

/// <summary>Aggregate root representing a provider inventory item (fuel storage / stock).</summary>
public class InventoryItem : IAuditableEntity
{
    protected InventoryItem()
    {
        Name = string.Empty;
        Type = string.Empty;
        Description = string.Empty;
        Unit = string.Empty;
        Status = string.Empty;
    }

    public InventoryItem(CreateInventoryItemCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Apply(command.ProviderId, command.Name, command.Type, command.Description, command.PricePerLiter,
            command.Stock, command.Reserved, command.Capacity, command.LowStockThreshold, command.Unit, command.Status);
    }

    public void Update(UpdateInventoryItemCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Apply(command.ProviderId, command.Name, command.Type, command.Description, command.PricePerLiter,
            command.Stock, command.Reserved, command.Capacity, command.LowStockThreshold, command.Unit, command.Status);
    }

    private void Apply(int? providerId, string name, string type, string description, decimal pricePerLiter,
        int stock, int reserved, int capacity, int lowStockThreshold, string unit, string status)
    {
        ProviderId = providerId;
        Name = name;
        Type = type;
        Description = description;
        PricePerLiter = pricePerLiter;
        Stock = stock;
        Reserved = reserved;
        Capacity = capacity;
        LowStockThreshold = lowStockThreshold;
        Unit = unit;
        Status = status;
    }

    public int Id { get; private set; }
    public int? ProviderId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal PricePerLiter { get; private set; }
    public int Stock { get; private set; }
    public int Reserved { get; private set; }
    public int Capacity { get; private set; }
    public int LowStockThreshold { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
