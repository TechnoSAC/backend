using Microsoft.Extensions.Localization;
using TechnoSac.FullTank.Platform.Ordering.Interfaces.Acl;
using TechnoSac.FullTank.Platform.Payment.Application.CommandServices;
using TechnoSac.FullTank.Platform.Payment.Domain.Model;
using TechnoSac.FullTank.Platform.Payment.Domain.Model.Commands;
using TechnoSac.FullTank.Platform.Resources.Errors;
using TechnoSac.FullTank.Platform.Shared.Application.Model;
using TechnoSac.FullTank.Platform.Shared.Domain.Repositories;

namespace TechnoSac.FullTank.Platform.Payment.Application.Internal.CommandServices;

public class PaymentCheckoutService(
    IPaymentCommandService paymentCommandService,
    IInvoiceCommandService invoiceCommandService,
    IOrderingContextFacade orderingContextFacade,
    IUnitOfWork unitOfWork,
    IStringLocalizer<ErrorMessages> localizer) : IPaymentCheckoutService
{
    public async Task<Result<PaymentCheckout>> Handle(CheckoutPaymentCommand command,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var paymentResult = await paymentCommandService.Handle(command.Payment, cancellationToken);
            if (paymentResult.IsFailure)
                return await Rollback(paymentResult.Error!, paymentResult.Message, cancellationToken);

            var invoice = command.Invoice with { PaymentId = paymentResult.Value!.Id };
            var invoiceResult = await invoiceCommandService.Handle(invoice, cancellationToken);
            if (invoiceResult.IsFailure)
                return await Rollback(invoiceResult.Error!, invoiceResult.Message, cancellationToken);

            if (command.Payment.OrderId is null
                || !await orderingContextFacade.MarkOrderPaid(command.Payment.OrderId.Value, cancellationToken))
                return await Rollback(PaymentError.ValidationError,
                    localizer[nameof(PaymentError.ValidationError)], cancellationToken);

            await unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result<PaymentCheckout>.Success(new PaymentCheckout(paymentResult.Value, invoiceResult.Value!));
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            return Result<PaymentCheckout>.Failure(PaymentError.InternalServerError,
                localizer[nameof(PaymentError.InternalServerError)]);
        }
    }

    private async Task<Result<PaymentCheckout>> Rollback(Enum error, string message,
        CancellationToken cancellationToken)
    {
        await unitOfWork.RollbackTransactionAsync(cancellationToken);
        var paymentError = error is PaymentError value ? value : PaymentError.ValidationError;
        return Result<PaymentCheckout>.Failure(paymentError, message);
    }
}
