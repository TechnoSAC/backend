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
[Route("api/v1/fuel-requests")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Available Fuel Request endpoints")]
[Authorize("BUYER", "PROVIDER")]
public class RequestsController(
    IRequestCommandService commandService,
    IRequestQueryService queryService,
    IStringLocalizer<ErrorMessages> errorLocalizer,
    ProblemDetailsFactory problemDetailsFactory)
    : ControllerBase
{
    [HttpGet]
    [SwaggerOperation(Summary = "Get fuel requests (optionally by buyerCompanyId or providerId)",
        OperationId = "GetAllRequests")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(IEnumerable<RequestResource>))]
    public async Task<IActionResult> GetAll([FromQuery] int? buyerCompanyId, [FromQuery] int? providerId,
        CancellationToken cancellationToken)
    {
        var user = this.CurrentUser();
        buyerCompanyId = user.Role == "BUYER" ? user.CompanyId : null;
        providerId = user.Role == "PROVIDER" ? user.CompanyId : null;
        var items = await queryService.Handle(new GetAllRequestsQuery(buyerCompanyId, providerId), cancellationToken);
        return Ok(items.Select(RequestResourceFromEntityAssembler.ToResourceFromEntity));
    }

    [HttpGet("{id:int}")]
    [SwaggerOperation(Summary = "Get a fuel request by id", OperationId = "GetRequestById")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(RequestResource))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not found")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await queryService.Handle(new GetRequestByIdQuery(id), cancellationToken);
        if (item is not null
            && !this.OwnsBuyerCompany(item.BuyerCompanyId)
            && !this.OwnsProviderCompany(item.ProviderId))
            return Forbid();
        return OrderingActionResultAssembler.ToActionResultFromEntity(this, item, errorLocalizer, problemDetailsFactory,
            found => Ok(RequestResourceFromEntityAssembler.ToResourceFromEntity(found)));
    }

    [HttpPost]
    [Authorize("BUYER")]
    [SwaggerOperation(Summary = "Create a fuel request", OperationId = "CreateRequest")]
    [SwaggerResponse(StatusCodes.Status201Created, "Created", typeof(RequestResource))]
    public async Task<IActionResult> Create([FromBody] CreateRequestResource resource,
        CancellationToken cancellationToken)
    {
        if (!this.OwnsBuyerCompany(resource.BuyerCompanyId)) return Forbid();
        var result = await commandService.Handle(
            CreateRequestCommandFromResourceAssembler.ToCommandFromResource(resource), cancellationToken);
        return OrderingActionResultAssembler.ToActionResult(this, result, problemDetailsFactory,
            created => CreatedAtAction(nameof(GetById), new { id = created.Id },
                RequestResourceFromEntityAssembler.ToResourceFromEntity(created)));
    }

    [HttpPut("{id:int}")]
    [SwaggerOperation(Summary = "Update a fuel request", OperationId = "UpdateRequest")]
    [SwaggerResponse(StatusCodes.Status200OK, "OK", typeof(RequestResource))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Not found")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRequestResource resource,
        CancellationToken cancellationToken)
    {
        if (!this.OwnsBuyerCompany(resource.BuyerCompanyId)
            && !this.OwnsProviderCompany(resource.ProviderId))
            return Forbid();
        var result = await commandService.Handle(
            UpdateRequestCommandFromResourceAssembler.ToCommandFromResource(id, resource), cancellationToken);
        return OrderingActionResultAssembler.ToActionResult(this, result, problemDetailsFactory,
            updated => Ok(RequestResourceFromEntityAssembler.ToResourceFromEntity(updated)));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var request = await queryService.Handle(new GetRequestByIdQuery(id), cancellationToken);
        if (request is not null
            && !this.OwnsBuyerCompany(request.BuyerCompanyId)
            && !this.OwnsProviderCompany(request.ProviderId))
            return Forbid();
        var result = await commandService.Handle(new DeleteRequestCommand(id), cancellationToken);
        return OrderingActionResultAssembler.ToActionResult(this, result, problemDetailsFactory, () => NoContent());
    }
}
