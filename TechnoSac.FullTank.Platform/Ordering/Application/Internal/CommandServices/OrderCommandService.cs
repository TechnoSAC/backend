using TechnoSac.FullTank.Platform.Ordering.Application.CommandServices;
using TechnoSac.FullTank.Platform.Ordering.Domain.Model;
using TechnoSac.FullTank.Platform.Ordering.Domain.Model.Aggregates;
using TechnoSac.FullTank.Platform.Ordering.Domain.Model.Commands;
using TechnoSac.FullTank.Platform.Ordering.Domain.Repositories;
using TechnoSac.FullTank.Platform.Iam.Interfaces.Acl;
using TechnoSac.FullTank.Platform.Resources.Errors;
using TechnoSac.FullTank.Platform.Shared.Application.Model;
using TechnoSac.FullTank.Platform.Shared.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace TechnoSac.FullTank.Platform.Ordering.Application.Internal.CommandServices;

public class OrderCommandService(
    IOrderRepository repository,
    IRequestRepository requestRepository,
    IIamContextFacade iamContextFacade,
    IUnitOfWork unitOfWork,
    IStringLocalizer<ErrorMessages> localizer)
    : IOrderCommandService
{
    public async Task<Result<Order>> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        if (!await IsValidCreate(command, cancellationToken))
            return ValidationFailure();

        var order = new Order(command);
        try
        {
            await repository.AddAsync(order, cancellationToken);
            await unitOfWork.CompleteAsync(cancellationToken);
            return Result<Order>.Success(order);
        }
        catch (DbUpdateException)
        {
            return Result<Order>.Failure(OrderingError.DatabaseError, localizer[nameof(OrderingError.DatabaseError)]);
        }
        catch (Exception)
        {
            return Result<Order>.Failure(OrderingError.InternalServerError,
                localizer[nameof(OrderingError.InternalServerError)]);
        }
    }

    public async Task<Result<Order>> Handle(UpdateOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await repository.FindByIdAsync(command.Id, cancellationToken);
        if (order is null)
            return Result<Order>.Failure(OrderingError.NotFound, localizer[nameof(OrderingError.NotFound)]);
        if (!IsValidData(command.BuyerCompanyId, command.ProviderId, command.RequestId, command.FuelType,
                command.Quantity, command.Unit, command.UnitPrice, command.TotalAmount, command.DeliveryAddress,
                command.Status, command.PaymentStatus)
            || !CanTransition(order.Status, command.Status)
            || !PaymentStatusMatches(command.Status, command.PaymentStatus)
            || !await ReferencesExist(command.BuyerCompanyId, command.ProviderId, cancellationToken))
            return ValidationFailure();

        try
        {
            order.Update(command);
            repository.Update(order);
            await unitOfWork.CompleteAsync(cancellationToken);
            return Result<Order>.Success(order);
        }
        catch (Exception)
        {
            return Result<Order>.Failure(OrderingError.InternalServerError,
                localizer[nameof(OrderingError.InternalServerError)]);
        }
    }

    public async Task<Result> Handle(DeleteOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await repository.FindByIdAsync(command.Id, cancellationToken);
        if (order is null)
            return Result.Failure(OrderingError.NotFound, localizer[nameof(OrderingError.NotFound)]);
        if (order.Status is not ("ACCEPTED" or "CANCELLED"))
            return Result.Failure(OrderingError.OperationCancelled,
                localizer[nameof(OrderingError.OperationCancelled)]);

        repository.Remove(order);
        await unitOfWork.CompleteAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<bool> IsValidCreate(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var status = string.IsNullOrWhiteSpace(command.Status) ? "ACCEPTED" : command.Status;
        var paymentStatus = string.IsNullOrWhiteSpace(command.PaymentStatus) ? "PENDING" : command.PaymentStatus;
        if (!IsValidData(command.BuyerCompanyId, command.ProviderId, command.RequestId, command.FuelType,
                command.Quantity, command.Unit, command.UnitPrice, command.TotalAmount, command.DeliveryAddress,
                status, paymentStatus)
            || status != "ACCEPTED"
            || paymentStatus != "PENDING"
            || command.RequestId is not > 0
            || !await ReferencesExist(command.BuyerCompanyId, command.ProviderId, cancellationToken))
            return false;

        var request = await requestRepository.FindByIdAsync(command.RequestId.Value, cancellationToken);
        return request is not null
               && request.Status == "APPROVED"
               && request.BuyerCompanyId == command.BuyerCompanyId
               && request.ProviderId == command.ProviderId
               && await repository.FindByRequestIdAsync(command.RequestId.Value, cancellationToken) is null;
    }

    private static bool IsValidData(int? buyerCompanyId, int? providerId, int? requestId, string fuelType,
        int quantity, string unit, decimal unitPrice, decimal totalAmount, string deliveryAddress, string status,
        string paymentStatus)
    {
        return buyerCompanyId is > 0
               && providerId is > 0
               && requestId is > 0
               && !string.IsNullOrWhiteSpace(fuelType)
               && quantity > 0
               && !string.IsNullOrWhiteSpace(unit)
               && unitPrice >= 0
               && totalAmount >= 0
               && !string.IsNullOrWhiteSpace(deliveryAddress)
               && status is "ACCEPTED" or "DISPATCHED" or "PENDING_PAYMENT" or "PAID" or "CLOSED" or "CANCELLED"
               && paymentStatus is "PENDING" or "PAID";
    }

    private async Task<bool> ReferencesExist(int? buyerCompanyId, int? providerId,
        CancellationToken cancellationToken)
    {
        return buyerCompanyId.HasValue
               && providerId.HasValue
               && await iamContextFacade.ExistsBuyerCompany(buyerCompanyId.Value, cancellationToken)
               && await iamContextFacade.ExistsProviderCompany(providerId.Value, cancellationToken);
    }

    private static bool CanTransition(string current, string next)
    {
        if (current == next) return true;
        return current switch
        {
            "ACCEPTED" => next is "DISPATCHED" or "CANCELLED",
            "DISPATCHED" => next == "PENDING_PAYMENT",
            "PENDING_PAYMENT" => next == "PAID",
            "PAID" => next == "CLOSED",
            _ => false
        };
    }

    private static bool PaymentStatusMatches(string status, string paymentStatus)
    {
        return status is "PAID" or "CLOSED" ? paymentStatus == "PAID" : paymentStatus == "PENDING";
    }

    private Result<Order> ValidationFailure()
    {
        return Result<Order>.Failure(OrderingError.ValidationError,
            localizer[nameof(OrderingError.ValidationError)]);
    }
}
