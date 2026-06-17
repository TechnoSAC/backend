using System.Net.Mime;
using TechnoSac.FullTank.Platform.Ordering.Application.CommandServices;
using TechnoSac.FullTank.Platform.Ordering.Application.QueryServices;
using TechnoSac.FullTank.Platform.Ordering.Domain.Model.Commands;
using TechnoSac.FullTank.Platform.Ordering.Domain.Model.Queries;
using TechnoSac.FullTank.Platform.Ordering.Interfaces.Rest.Resources;
using TechnoSac.FullTank.Platform.Ordering.Interfaces.Rest.Transform;
using TechnoSac.FullTank.Platform.Resources.Errors;
using TechnoSac.FullTank.Platform.Shared.Interfaces.Rest.ProblemDetails;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Swashbuckle.AspNetCore.Annotations;
using TechnoSac.FullTank.Platform.Iam.Infrastructure.Pipeline.Middleware.Attributes;
using TechnoSac.FullTank.Platform.Iam.Infrastructure.Pipeline.Middleware.Extensions;

namespace TechnoSac.FullTank.Platform.Ordering.Interfaces.Rest;

[ApiController]
[Route("api/v1/orders")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Available Order endpoints")]
[Authorize("BUYER", "PROVIDER")]
public class OrdersController(
    IOrderCommandService commandService,
    IOrderQueryService queryService,
    IStringLocalizer<ErrorMessages> errorLocalizer,
    ProblemDetailsFactory problemDetailsFactory)
    : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Get orders (optionally by buyerCompanyId or providerId)", OperationId = "GetAllOrders")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(IEnumerable<OrderResource>))]
    public async Task<IActionResult> GetAll([FromQuery] int? buyerCompanyId, [FromQuery] int? providerId,
        CancellationToken cancellationToken)
    {
        var user = this.CurrentUser();
        buyerCompanyId = user.Role == "BUYER" ? user.CompanyId : null;
        providerId = user.Role == "PROVIDER" ? user.CompanyId : null;
        var items = await queryService.Handle(new GetAllOrdersQuery(buyerCompanyId, providerId), cancellationToken);
        return Ok(items.Select(OrderResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("{id:int}")]
    [SwaggerOperation(Summary = "Get an order by id", OperationId = "GetOrderById")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(OrderResource))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not found")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await queryService.Handle(new GetOrderByIdQuery(id), cancellationToken);
        if (item is not null
            && !this.OwnsBuyerCompany(item.BuyerCompanyId)
            && !this.OwnsProviderCompany(item.ProviderId))
            return Forbid();
        return OrderingActionResultAssembler.ToActionResultFromEntity(this, item, errorLocalizer, problemDetailsFactory,
            found => Ok(OrderResourceFromEntityAssembler.ToResourceFromEntity(found)));
    }

    [HttpPost]
    [Authorize("PROVIDER")]
    [SwaggerOperation(Summary = "Create an order", OperationId = "CreateOrder")]
    [SwaggerResponse(StatusCodes.Status201Created, "Created", typeof(OrderResource))]
    public async Task<IActionResult> Create([FromBody] CreateOrderResource resource, CancellationToken cancellationToken)
    {
        if (!this.OwnsProviderCompany(resource.ProviderId)) return Forbid();
        var result = await commandService.Handle(
            CreateOrderCommandFromResourceAssembler.ToCommandFromResource(resource), cancellationToken);
        return OrderingActionResultAssembler.ToActionResult(this, result, problemDetailsFactory,
            created => CreatedAtAction(nameof(GetById), new { id = created.Id },
                OrderResourceFromEntityAssembler.ToResourceFromEntity(created)));
    }

    [HttpPut("{id:int}")]
    [SwaggerOperation(Summary = "Update an order", OperationId = "UpdateOrder")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(OrderResource))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not found")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateOrderResource resource,
        CancellationToken cancellationToken)
    {
        if (!this.OwnsBuyerCompany(resource.BuyerCompanyId)
            && !this.OwnsProviderCompany(resource.ProviderId))
            return Forbid();
        var result = await commandService.Handle(
            UpdateOrderCommandFromResourceAssembler.ToCommandFromResource(id, resource), cancellationToken);
        return OrderingActionResultAssembler.ToActionResult(this, result, problemDetailsFactory,
            updated => Ok(OrderResourceFromEntityAssembler.ToResourceFromEntity(updated)));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var order = await queryService.Handle(new GetOrderByIdQuery(id), cancellationToken);
        if (order is not null && !this.OwnsProviderCompany(order.ProviderId)) return Forbid();
        var result = await commandService.Handle(new DeleteOrderCommand(id), cancellationToken);
        return OrderingActionResultAssembler.ToActionResult(this, result, problemDetailsFactory, () => NoContent());
    }
}
