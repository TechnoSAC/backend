using System.Net.Mime;
using TechnoSac.FullTank.Platform.Payment.Application.CommandServices;
using TechnoSac.FullTank.Platform.Payment.Application.QueryServices;
using TechnoSac.FullTank.Platform.Payment.Domain.Model.Queries;
using TechnoSac.FullTank.Platform.Payment.Interfaces.Rest.Resources;
using TechnoSac.FullTank.Platform.Payment.Interfaces.Rest.Transform;
using TechnoSac.FullTank.Platform.Resources.Errors;
using TechnoSac.FullTank.Platform.Shared.Interfaces.Rest.ProblemDetails;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Swashbuckle.AspNetCore.Annotations;
using TechnoSac.FullTank.Platform.Iam.Infrastructure.Pipeline.Middleware.Extensions;

namespace TechnoSac.FullTank.Platform.Payment.Interfaces.Rest;

[ApiController]
[Authorize("BUYER")]
[Route("api/v1/invoices")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Available Invoice endpoints")]
public class InvoicesController(
    IInvoiceCommandService commandService,
    IInvoiceQueryService queryService,
    IPaymentQueryService paymentQueryService,
    IStringLocalizer<ErrorMessages> errorLocalizer,
    ProblemDetailsFactory problemDetailsFactory)
    : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Get all invoices", OperationId = "GetAllInvoices")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(IEnumerable<InvoiceResource>))]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var items = await queryService.Handle(new GetAllInvoicesQuery(), cancellationToken);
        var payments = await paymentQueryService.Handle(
            new GetAllPaymentsQuery(this.CurrentUser().CompanyId), cancellationToken);
        var paymentIds = payments.Select(payment => payment.Id).ToHashSet();
        return Ok(items.Where(invoice => invoice.PaymentId.HasValue && paymentIds.Contains(invoice.PaymentId.Value))
            .Select(InvoiceResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("{id:int}")]
    [SwaggerOperation(Summary = "Get an invoice by id", OperationId = "GetInvoiceById")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(InvoiceResource))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not found")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await queryService.Handle(new GetInvoiceByIdQuery(id), cancellationToken);
        if (item?.PaymentId is int paymentId
            && !await OwnsPayment(paymentId, cancellationToken))
            return Forbid();
        return PaymentActionResultAssembler.ToActionResultFromEntity(this, item, errorLocalizer,
            problemDetailsFactory, found => Ok(InvoiceResourceFromEntityAssembler.ToResourceFromEntity(found)));
    }

    [HttpGet("payment/{paymentId:int}")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [SwaggerOperation(Summary = "Get an invoice by payment id", OperationId = "GetInvoiceByPayment")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(InvoiceResource))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not found")]
    public async Task<IActionResult> GetByPayment(int paymentId, CancellationToken cancellationToken)
    {
        if (!await OwnsPayment(paymentId, cancellationToken)) return Forbid();
        var item = await queryService.Handle(new GetInvoiceByPaymentIdQuery(paymentId), cancellationToken);
        return PaymentActionResultAssembler.ToActionResultFromEntity(this, item, errorLocalizer,
            problemDetailsFactory, found => Ok(InvoiceResourceFromEntityAssembler.ToResourceFromEntity(found)));
    }


    [HttpGet("/api/v1/payments/{paymentId:int}/invoice")]
    [SwaggerOperation(Summary = "Get the invoice for a payment", OperationId = "GetInvoiceForPayment")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(InvoiceResource))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not found")]
    public Task<IActionResult> GetForPayment(int paymentId, CancellationToken cancellationToken)
    {
        return GetByPayment(paymentId, cancellationToken);
    }
    [HttpGet("order/{orderId:int}")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [SwaggerOperation(Summary = "Get an invoice by order id", OperationId = "GetInvoiceByOrder")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(InvoiceResource))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not found")]
    public async Task<IActionResult> GetByOrder(int orderId, CancellationToken cancellationToken)
    {
        var payments = await paymentQueryService.Handle(new GetPaymentsByOrderIdQuery(orderId), cancellationToken);
        if (!payments.Any(payment => this.OwnsBuyerCompany(payment.CompanyId))) return Forbid();
        var item = await queryService.Handle(new GetInvoiceByOrderIdQuery(orderId), cancellationToken);
        return PaymentActionResultAssembler.ToActionResultFromEntity(this, item, errorLocalizer,
            problemDetailsFactory, found => Ok(InvoiceResourceFromEntityAssembler.ToResourceFromEntity(found)));
    }

    [HttpGet("/api/v1/orders/{orderId:int}/invoice")]
    [SwaggerOperation(Summary = "Get the invoice for an order", OperationId = "GetInvoiceForOrder")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(InvoiceResource))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not found")]
    public Task<IActionResult> GetForOrder(int orderId, CancellationToken cancellationToken)
    {
        return GetByOrder(orderId, cancellationToken);
    }
    [HttpPost]
    [Authorize("ADMIN")]
    [SwaggerOperation(Summary = "Create an invoice", OperationId = "CreateInvoice")]
    [SwaggerResponse(StatusCodes.Status201Created, "Created", typeof(InvoiceResource))]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceResource resource,
        CancellationToken cancellationToken)
    {
        var result = await commandService.Handle(
            CreateInvoiceCommandFromResourceAssembler.ToCommandFromResource(resource), cancellationToken);
        return PaymentActionResultAssembler.ToActionResult(this, result, problemDetailsFactory,
            created => CreatedAtAction(nameof(GetById), new { id = created.Id },
                InvoiceResourceFromEntityAssembler.ToResourceFromEntity(created)));
    }

    [HttpPut("{id:int}")]
    [Authorize("ADMIN")]
    [SwaggerOperation(Summary = "Update an invoice", OperationId = "UpdateInvoice")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(InvoiceResource))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not found")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateInvoiceResource resource,
        CancellationToken cancellationToken)
    {
        var result = await commandService.Handle(
            UpdateInvoiceCommandFromResourceAssembler.ToCommandFromResource(id, resource), cancellationToken);
        return PaymentActionResultAssembler.ToActionResult(this, result, problemDetailsFactory,
            updated => Ok(InvoiceResourceFromEntityAssembler.ToResourceFromEntity(updated)));
    }

    private async Task<bool> OwnsPayment(int paymentId, CancellationToken cancellationToken)
    {
        var payment = await paymentQueryService.Handle(new GetPaymentByIdQuery(paymentId), cancellationToken);
        return payment is not null && this.OwnsBuyerCompany(payment.CompanyId);
    }
}
