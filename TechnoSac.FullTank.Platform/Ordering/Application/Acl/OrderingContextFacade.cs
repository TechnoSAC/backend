using TechnoSac.FullTank.Platform.Ordering.Application.QueryServices;
using TechnoSac.FullTank.Platform.Ordering.Application.CommandServices;
using TechnoSac.FullTank.Platform.Ordering.Domain.Model.Commands;
using TechnoSac.FullTank.Platform.Ordering.Domain.Model.Queries;
using TechnoSac.FullTank.Platform.Ordering.Interfaces.Acl;

namespace TechnoSac.FullTank.Platform.Ordering.Application.Acl;

public class OrderingContextFacade(
    IOrderQueryService orderQueryService,
    IOrderCommandService orderCommandService) : IOrderingContextFacade
{
    public async Task<bool> ExistsOrder(int orderId, CancellationToken cancellationToken)
    {
        return await Find(orderId, cancellationToken) is not null;
    }

    public async Task<int?> FetchOrderBuyerCompanyId(int orderId, CancellationToken cancellationToken)
    {
        return (await Find(orderId, cancellationToken))?.BuyerCompanyId;
    }

    public async Task<int?> FetchOrderProviderId(int orderId, CancellationToken cancellationToken)
    {
        return (await Find(orderId, cancellationToken))?.ProviderId;
    }

    public async Task<decimal?> FetchOrderTotalAmount(int orderId, CancellationToken cancellationToken)
    {
        return (await Find(orderId, cancellationToken))?.TotalAmount;
    }

    public async Task<string> FetchOrderStatus(int orderId, CancellationToken cancellationToken)
    {
        return (await Find(orderId, cancellationToken))?.Status ?? string.Empty;
    }

    public async Task<bool> MarkOrderPaid(int orderId, CancellationToken cancellationToken)
    {
        var order = await Find(orderId, cancellationToken);
        if (order is null) return false;

        var paidAt = DateTimeOffset.UtcNow.ToString("O");
        var command = new UpdateOrderCommand(order.Id, order.RequestId, order.BuyerCompanyId, order.ProviderId,
            order.EquipmentId, order.FuelType, order.Quantity, order.Unit, order.UnitPrice, order.TotalAmount,
            order.DeliveryAddress, "PAID", "PAID", order.DriverId, order.VehicleId, order.EstimatedDeliveryDate,
            order.DispatchedAt, order.DeliveredAt, paidAt, order.ClosedAt, order.CancelledAt, order.CancelReason);
        var result = await orderCommandService.Handle(command, cancellationToken);
        return result.IsSuccess;
    }

    private async Task<Domain.Model.Aggregates.Order?> Find(int orderId, CancellationToken cancellationToken)
    {
        return await orderQueryService.Handle(new GetOrderByIdQuery(orderId), cancellationToken);
    }
}
