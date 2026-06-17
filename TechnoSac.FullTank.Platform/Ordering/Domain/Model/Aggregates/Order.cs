using TechnoSac.FullTank.Platform.Ordering.Domain.Model.Commands;
using TechnoSac.FullTank.Platform.Shared.Domain.Model.Entities;

namespace TechnoSac.FullTank.Platform.Ordering.Domain.Model.Aggregates;

/// <summary>
///     Aggregate root representing a confirmed fuel order, created when a provider accepts a request.
/// </summary>
/// <remarks>External references (RequestId, BuyerCompanyId, ProviderId, EquipmentId, DriverId, VehicleId)
///     are kept as primitive IDs.</remarks>
public class Order : IAuditableEntity
{
    protected Order()
    {
        FuelType = string.Empty;
        Unit = string.Empty;
        DeliveryAddress = string.Empty;
        Status = string.Empty;
        PaymentStatus = string.Empty;
    }

    public Order(CreateOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Apply(command.RequestId, command.BuyerCompanyId, command.ProviderId, command.EquipmentId, command.FuelType,
            command.Quantity, command.Unit, command.UnitPrice, command.TotalAmount, command.DeliveryAddress,
            string.IsNullOrWhiteSpace(command.Status) ? "ACCEPTED" : command.Status,
            string.IsNullOrWhiteSpace(command.PaymentStatus) ? "PENDING" : command.PaymentStatus,
            command.DriverId, command.VehicleId, command.EstimatedDeliveryDate, command.DispatchedAt,
            command.DeliveredAt, command.PaidAt, command.ClosedAt, command.CancelledAt, command.CancelReason);
    }

    public void Update(UpdateOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        Apply(command.RequestId, command.BuyerCompanyId, command.ProviderId, command.EquipmentId, command.FuelType,
            command.Quantity, command.Unit, command.UnitPrice, command.TotalAmount, command.DeliveryAddress,
            command.Status, command.PaymentStatus, command.DriverId, command.VehicleId, command.EstimatedDeliveryDate,
            command.DispatchedAt, command.DeliveredAt, command.PaidAt, command.ClosedAt, command.CancelledAt,
            command.CancelReason);
    }

    private void Apply(int? requestId, int? buyerCompanyId, int? providerId, int? equipmentId, string fuelType,
        int quantity, string unit, decimal unitPrice, decimal totalAmount, string deliveryAddress, string status,
        string paymentStatus, int? driverId, int? vehicleId, string? estimatedDeliveryDate, string? dispatchedAt,
        string? deliveredAt, string? paidAt, string? closedAt, string? cancelledAt, string? cancelReason)
    {
        RequestId = requestId;
        BuyerCompanyId = buyerCompanyId;
        ProviderId = providerId;
        EquipmentId = equipmentId;
        FuelType = fuelType;
        Quantity = quantity;
        Unit = unit;
        UnitPrice = unitPrice;
        TotalAmount = totalAmount;
        DeliveryAddress = deliveryAddress;
        Status = status;
        PaymentStatus = paymentStatus;
        DriverId = driverId;
        VehicleId = vehicleId;
        EstimatedDeliveryDate = estimatedDeliveryDate;
        DispatchedAt = dispatchedAt;
        DeliveredAt = deliveredAt;
        PaidAt = paidAt;
        ClosedAt = closedAt;
        CancelledAt = cancelledAt;
        CancelReason = cancelReason;
    }

    public int Id { get; private set; }
    public int? RequestId { get; private set; }
    public int? BuyerCompanyId { get; private set; }
    public int? ProviderId { get; private set; }
    public int? EquipmentId { get; private set; }
    public string FuelType { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string DeliveryAddress { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string PaymentStatus { get; private set; } = string.Empty;
    public int? DriverId { get; private set; }
    public int? VehicleId { get; private set; }
    public string? EstimatedDeliveryDate { get; private set; }
    public string? DispatchedAt { get; private set; }
    public string? DeliveredAt { get; private set; }
    public string? PaidAt { get; private set; }
    public string? ClosedAt { get; private set; }
    public string? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
