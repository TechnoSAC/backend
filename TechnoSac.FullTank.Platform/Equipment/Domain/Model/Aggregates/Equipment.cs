using TechnoSac.FullTank.Platform.Equipment.Domain.Model.Commands;
using TechnoSac.FullTank.Platform.Shared.Domain.Model.Entities;

namespace TechnoSac.FullTank.Platform.Equipment.Domain.Model.Aggregates;

/// <summary>Aggregate root representing a buyer's fuel-consuming equipment (vehicle, machine, tank).</summary>
public class Equipment : IAuditableEntity
{
    protected Equipment()
    {
        Name = string.Empty;
        Type = string.Empty;
        RequiredFuelType = string.Empty;
        Unit = string.Empty;
        Status = string.Empty;
        Location = string.Empty;
    }

    public Equipment(CreateEquipmentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Apply(command.CompanyId, command.Name, command.Type, command.RequiredFuelType, command.Capacity,
            command.CurrentLevel, command.Unit, command.Status, command.FavoriteProviderId, command.AutoRefill,
            command.RefillThreshold, command.Location, command.LastRefillDate);
    }

    public void Update(UpdateEquipmentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Apply(command.CompanyId, command.Name, command.Type, command.RequiredFuelType, command.Capacity,
            command.CurrentLevel, command.Unit, command.Status, command.FavoriteProviderId, command.AutoRefill,
            command.RefillThreshold, command.Location, command.LastRefillDate);
    }

    private void Apply(int? companyId, string name, string type, string requiredFuelType, int capacity,
        int currentLevel, string unit, string status, int? favoriteProviderId, bool autoRefill, int refillThreshold,
        string location, string? lastRefillDate)
    {
        CompanyId = companyId;
        Name = name;
        Type = type;
        RequiredFuelType = requiredFuelType;
        Capacity = capacity;
        CurrentLevel = currentLevel;
        Unit = unit;
        Status = status;
        FavoriteProviderId = favoriteProviderId;
        AutoRefill = autoRefill;
        RefillThreshold = refillThreshold;
        Location = location;
        LastRefillDate = lastRefillDate;
    }

    public int Id { get; private set; }
    public int? CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public string RequiredFuelType { get; private set; } = string.Empty;
    public int Capacity { get; private set; }
    public int CurrentLevel { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public int? FavoriteProviderId { get; private set; }
    public bool AutoRefill { get; private set; }
    public int RefillThreshold { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public string? LastRefillDate { get; private set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
