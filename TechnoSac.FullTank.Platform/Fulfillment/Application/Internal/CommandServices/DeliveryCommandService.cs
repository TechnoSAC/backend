using TechnoSac.FullTank.Platform.Fulfillment.Application.CommandServices;
using TechnoSac.FullTank.Platform.Fulfillment.Domain.Model;
using TechnoSac.FullTank.Platform.Fulfillment.Domain.Model.Aggregates;
using TechnoSac.FullTank.Platform.Fulfillment.Domain.Model.Commands;
using TechnoSac.FullTank.Platform.Fulfillment.Domain.Repositories;
using TechnoSac.FullTank.Platform.Ordering.Interfaces.Acl;
using TechnoSac.FullTank.Platform.Resources.Errors;
using TechnoSac.FullTank.Platform.Shared.Application.Model;
using TechnoSac.FullTank.Platform.Shared.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace TechnoSac.FullTank.Platform.Fulfillment.Application.Internal.CommandServices;

public class DeliveryCommandService(
    IDeliveryRepository repository,
    IDriverRepository driverRepository,
    IVehicleRepository vehicleRepository,
    IOrderingContextFacade orderingContextFacade,
    IUnitOfWork unitOfWork,
    IStringLocalizer<ErrorMessages> localizer)
    : IDeliveryCommandService
{
    public async Task<Result<Delivery>> Handle(CreateDeliveryCommand command, CancellationToken cancellationToken)
    {
        if (command.OrderId is not > 0
            || command.ProviderId is not > 0
            || command.DriverId is not > 0
            || command.VehicleId is not > 0
            || command.Status != "on_the_way"
            || string.IsNullOrWhiteSpace(command.OriginLocation)
            || string.IsNullOrWhiteSpace(command.DestinationLocation))
            return ValidationFailure();

        var driver = await driverRepository.FindByIdAsync(command.DriverId.Value, cancellationToken);
        var vehicle = await vehicleRepository.FindByIdAsync(command.VehicleId.Value, cancellationToken);
        if (driver?.ProviderId != command.ProviderId
            || vehicle?.ProviderId != command.ProviderId
            || await orderingContextFacade.FetchOrderProviderId(command.OrderId.Value, cancellationToken)
            != command.ProviderId
            || await orderingContextFacade.FetchOrderStatus(command.OrderId.Value, cancellationToken) != "ACCEPTED"
            || await repository.FindActiveByOrderIdAsync(command.OrderId.Value, cancellationToken) is not null)
            return ValidationFailure();

        var delivery = new Delivery(command);
        try
        {
            await repository.AddAsync(delivery, cancellationToken);
            await unitOfWork.CompleteAsync(cancellationToken);
            return Result<Delivery>.Success(delivery);
        }
        catch (DbUpdateException)
        {
            return Result<Delivery>.Failure(FulfillmentError.DatabaseError,
                localizer[nameof(FulfillmentError.DatabaseError)]);
        }
        catch (Exception)
        {
            return Result<Delivery>.Failure(FulfillmentError.InternalServerError,
                localizer[nameof(FulfillmentError.InternalServerError)]);
        }
    }

    public async Task<Result<Delivery>> Handle(UpdateDeliveryCommand command, CancellationToken cancellationToken)
    {
        var delivery = await repository.FindByIdAsync(command.Id, cancellationToken);
        if (delivery is null)
            return Result<Delivery>.Failure(FulfillmentError.NotFound, localizer[nameof(FulfillmentError.NotFound)]);

        try
        {
            delivery.Update(command);
            repository.Update(delivery);
            await unitOfWork.CompleteAsync(cancellationToken);
            return Result<Delivery>.Success(delivery);
        }
        catch (Exception)
        {
            return Result<Delivery>.Failure(FulfillmentError.InternalServerError,
                localizer[nameof(FulfillmentError.InternalServerError)]);
        }
    }

    public async Task<Result<Delivery>> Handle(CompleteDeliveryCommand command, CancellationToken cancellationToken)
    {
        var delivery = await repository.FindByIdAsync(command.Id, cancellationToken);
        if (delivery is null)
            return Result<Delivery>.Failure(FulfillmentError.NotFound, localizer[nameof(FulfillmentError.NotFound)]);
        if (delivery.Status != "on_the_way")
            return ValidationFailure();

        try
        {
            delivery.Complete(command.DeliveredAt);
            repository.Update(delivery);
            await unitOfWork.CompleteAsync(cancellationToken);
            return Result<Delivery>.Success(delivery);
        }
        catch (Exception)
        {
            return Result<Delivery>.Failure(FulfillmentError.InternalServerError,
                localizer[nameof(FulfillmentError.InternalServerError)]);
        }
    }

    private Result<Delivery> ValidationFailure()
    {
        return Result<Delivery>.Failure(FulfillmentError.ValidationError,
            localizer[nameof(FulfillmentError.ValidationError)]);
    }
}
