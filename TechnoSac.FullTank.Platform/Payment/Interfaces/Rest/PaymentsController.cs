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
[Route("api/v1/payments")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Available Payment endpoints")]
public class PaymentsController(
    IPaymentCommandService commandService,
    IPaymentQueryService queryService,
    IStringLocalizer<ErrorMessages> errorLocalizer,
    ProblemDetailsFactory problemDetailsFactory)
    : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Get payments (optionally by companyId)", OperationId = "GetAllPayments")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(IEnumerable<PaymentResource>))]
    public async Task<IActionResult> GetAll([FromQuery] int? companyId, CancellationToken cancellationToken)
    {
        companyId = this.CurrentUser().CompanyId;
        var items = await queryService.Handle(new GetAllPaymentsQuery(companyId), cancellationToken);
        return Ok(items.Select(PaymentResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("{id:int}")]
    [SwaggerOperation(Summary = "Get a payment by id", OperationId = "GetPaymentById")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(PaymentResource))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not found")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await queryService.Handle(new GetPaymentByIdQuery(id), cancellationToken);
        if (item is not null && !this.OwnsBuyerCompany(item.CompanyId)) return Forbid();
        return PaymentActionResultAssembler.ToActionResultFromEntity(this, item, errorLocalizer,
            problemDetailsFactory, found => Ok(PaymentResourceFromEntityAssembler.ToResourceFromEntity(found)));
    }

    [HttpGet("company/{companyId:int}")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [SwaggerOperation(Summary = "Get payments by buyer company id", OperationId = "GetPaymentsByCompany")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(IEnumerable<PaymentResource>))]
    public async Task<IActionResult> GetByCompany(int companyId, CancellationToken cancellationToken)
    {
        if (!this.OwnsBuyerCompany(companyId)) return Forbid();
        var items = await queryService.Handle(new GetAllPaymentsQuery(companyId), cancellationToken);
        return Ok(items.Select(PaymentResourceFromEntityAssembler.ToResourceFromEntity));
    }


    [HttpGet("/api/v1/buyer-companies/{companyId:int}/payments")]
    [SwaggerOperation(Summary = "Get payments for a buyer company", OperationId = "GetPaymentsForBuyerCompany")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(IEnumerable<PaymentResource>))]
    public Task<IActionResult> GetForBuyerCompany(int companyId, CancellationToken cancellationToken)
    {
        return GetByCompany(companyId, cancellationToken);
    }
    [HttpGet("order/{orderId:int}")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [SwaggerOperation(Summary = "Get payments by order id", OperationId = "GetPaymentsByOrder")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(IEnumerable<PaymentResource>))]
    public async Task<IActionResult> GetByOrder(int orderId, CancellationToken cancellationToken)
    {
        var items = await queryService.Handle(new GetPaymentsByOrderIdQuery(orderId), cancellationToken);
        if (items.Any(item => !this.OwnsBuyerCompany(item.CompanyId))) return Forbid();
        return Ok(items.Select(PaymentResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("/api/v1/orders/{orderId:int}/payments")]
    [SwaggerOperation(Summary = "Get payments for an order", OperationId = "GetPaymentsForOrder")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(IEnumerable<PaymentResource>))]
    public Task<IActionResult> GetForOrder(int orderId, CancellationToken cancellationToken)
    {
        return GetByOrder(orderId, cancellationToken);
    }
    [HttpPost]
    [Authorize("ADMIN")]
    [SwaggerOperation(Summary = "Create a payment", OperationId = "CreatePayment")]
    [SwaggerResponse(StatusCodes.Status201Created, "Created", typeof(PaymentResource))]
    public async Task<IActionResult> Create([FromBody] CreatePaymentResource resource,
        CancellationToken cancellationToken)
    {
        var result = await commandService.Handle(
            CreatePaymentCommandFromResourceAssembler.ToCommandFromResource(resource), cancellationToken);
        return PaymentActionResultAssembler.ToActionResult(this, result, problemDetailsFactory,
            created => CreatedAtAction(nameof(GetById), new { id = created.Id },
                PaymentResourceFromEntityAssembler.ToResourceFromEntity(created)));
    }

    [HttpPut("{id:int}")]
    [Authorize("ADMIN")]
    [SwaggerOperation(Summary = "Update a payment", OperationId = "UpdatePayment")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(PaymentResource))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not found")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePaymentResource resource,
        CancellationToken cancellationToken)
    {
        var result = await commandService.Handle(
            UpdatePaymentCommandFromResourceAssembler.ToCommandFromResource(id, resource), cancellationToken);
        return PaymentActionResultAssembler.ToActionResult(this, result, problemDetailsFactory,
            updated => Ok(PaymentResourceFromEntityAssembler.ToResourceFromEntity(updated)));
    }
}
