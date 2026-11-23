using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using TaskList.Api.Constants;
using TaskList.Api.Models;
using TaskList.Application.Constants;
using TaskList.Application.Enums;
using TaskList.Application.Models;
using TaskList.Application.Services;

namespace TaskList.Api.Controllers;

[ApiController]
[Route(TaskListRoutes.Collection)]
public sealed class TaskListsController(TaskListService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TaskListResponse>> CreateAsync(
        [FromHeader(Name = RequestHeaders.CallerId), Required] Guid? callerId,
        [FromBody] CreateTaskListRequest request,
        CancellationToken cancellationToken)
    {
        var list = await service.CreateAsync(callerId.GetValueOrDefault(), request.Name, cancellationToken);

        var response = new TaskListResponse(list.Id, list.Name, list.OwnerId, list.CreatedAtUtc);
        var location = Url.RouteUrl(TaskListRoutes.ListByIdName, new { listId = list.Id })
            ?? throw new InvalidOperationException(ExceptionMessageConstants.MissingTaskListLocation);
        return Created(location, response);
    }

    [HttpGet]
    public async Task<ActionResult<TaskListPage>> ListAsync(
        [FromHeader(Name = RequestHeaders.CallerId), Required] Guid? callerId,
        CancellationToken cancellationToken,
        [FromQuery] int page = PaginationConstants.FirstPage,
        [FromQuery] int pageSize = PaginationConstants.DefaultPageSize)
    {
        var result = await service.ListAsync(callerId.GetValueOrDefault(), page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet(TaskListRoutes.List, Name = TaskListRoutes.ListByIdName)]
    public async Task<ActionResult<TaskListResponse>> GetAsync(
        [FromRoute] Guid listId,
        [FromHeader(Name = RequestHeaders.CallerId), Required] Guid? callerId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(listId, callerId.GetValueOrDefault(), cancellationToken);

        if (result.List is { } list)
        {
            return Ok(new TaskListResponse(list.Id, list.Name, list.OwnerId, list.CreatedAtUtc));
        }

        return result.Exists ? StatusCode(StatusCodes.Status403Forbidden) : NotFound();
    }

    [HttpPatch(TaskListRoutes.List)]
    public async Task<IActionResult> RenameAsync(
        [FromRoute] Guid listId,
        [FromHeader(Name = RequestHeaders.CallerId), Required] Guid? callerId,
        [FromBody] RenameTaskListRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.RenameAsync(
            listId, callerId.GetValueOrDefault(), request.Name, cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete(TaskListRoutes.List)]
    public async Task<IActionResult> DeleteAsync(
        [FromRoute] Guid listId,
        [FromHeader(Name = RequestHeaders.CallerId), Required] Guid? callerId,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(listId, callerId.GetValueOrDefault(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut(TaskListRoutes.LinkedUser)]
    public async Task<IActionResult> AddLinkedUserAsync(
        [FromRoute] Guid listId,
        [FromRoute] Guid userId,
        [FromHeader(Name = RequestHeaders.CallerId), Required] Guid? callerId,
        CancellationToken cancellationToken)
    {
        var result = await service.AddLinkedUserAsync(
            listId, callerId.GetValueOrDefault(), userId, cancellationToken);
        return result switch
        {
            AddLinkedUserOutcome.Added => StatusCode(StatusCodes.Status201Created),
            AddLinkedUserOutcome.AlreadyLinked => NoContent(),
            AddLinkedUserOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
            AddLinkedUserOutcome.NotFound => NotFound(),
            AddLinkedUserOutcome.OwnerConflict => Conflict(),
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, null)
        };
    }

    [HttpGet(TaskListRoutes.LinkedUsers)]
    public async Task<ActionResult<Guid[]>> GetLinkedUsersAsync(
        [FromRoute] Guid listId,
        [FromHeader(Name = RequestHeaders.CallerId), Required] Guid? callerId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetLinkedUsersAsync(listId, callerId.GetValueOrDefault(), cancellationToken);
        if (result.LinkedUserIds is { } linkedUserIds)
        {
            return Ok(linkedUserIds);
        }

        return result.Exists ? StatusCode(StatusCodes.Status403Forbidden) : NotFound();
    }

    [HttpDelete(TaskListRoutes.LinkedUser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveLinkedUserAsync(
        [FromRoute] Guid listId,
        [FromRoute] Guid userId,
        [FromHeader(Name = RequestHeaders.CallerId), Required] Guid? callerId,
        CancellationToken cancellationToken)
    {
        var result = await service.RemoveLinkedUserAsync(
            listId, callerId.GetValueOrDefault(), userId, cancellationToken);
        return result == ListMutationOutcome.OwnerConflict ? Conflict() : ToActionResult(result);
    }

    private IActionResult ToActionResult(ListMutationOutcome result) => result switch
    {
        ListMutationOutcome.Succeeded => NoContent(),
        ListMutationOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden),
        ListMutationOutcome.NotFound => NotFound(),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, null)
    };
}
