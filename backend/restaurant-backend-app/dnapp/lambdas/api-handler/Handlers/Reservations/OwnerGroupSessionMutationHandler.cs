using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.Reservations;

public abstract class OwnerGroupSessionMutationHandlerBase(IGuestSessionService service) : IApiRequestHandler
{
    protected IGuestSessionService Service { get; } = service;

    public abstract Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context);

    protected static bool TryGetReservationAndUser(APIGatewayProxyRequest request, ILambdaContext context, out string reservationId, out string userId, out APIGatewayProxyResponse? error)
    {
        reservationId = string.Empty;
        userId = string.Empty;
        error = null;
        if (!request.TryGetPathValue("reservationId", out string? pathReservationId) || string.IsNullOrWhiteSpace(pathReservationId))
        {
            error = ResponseBuilder.CreateErrorResponse(400, "Missing 'reservationId' in path parameters.", context.Logger);
            return false;
        }

        string? authenticatedUserId = UserContextHelper.GetAuthenticatedUserId(request);
        if (authenticatedUserId is null)
        {
            error = ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);
            return false;
        }

        reservationId = pathReservationId;
        userId = authenticatedUserId;
        return true;
    }

    protected static bool TryGetDishRequest(APIGatewayProxyRequest request, ILambdaContext context, out UpdateGuestSessionDishRequest? body, out APIGatewayProxyResponse? error)
    {
        body = null;
        error = null;
        try
        {
            body = JsonSerializer.Deserialize<UpdateGuestSessionDishRequest>(request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
        }

        if (body is null || string.IsNullOrWhiteSpace(body.DishId) || body.Quantity <= 0)
        {
            error = ResponseBuilder.CreateErrorResponse(400, "DishId is required and Quantity must be greater than zero.", context.Logger);
            return false;
        }

        return true;
    }

    protected static APIGatewayProxyResponse CreateError(Exception exception, ILambdaContext context)
    {
        return exception switch
        {
            ActionForbiddenException => ResponseBuilder.CreateErrorResponse(403, exception.Message, context.Logger, exception),
            ReservationNotFoundException or GroupReservationNotFoundException or GuestSessionNotFoundException => ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger, exception),
            ValidationException validation => ResponseBuilder.CreateErrorCodeResponse(validation),
            _ => ResponseBuilder.CreateErrorResponse(500, "Internal Server Error", context.Logger, exception),
        };
    }
}

public sealed class AddOwnerGroupSessionDishHandler(IGuestSessionService service) : OwnerGroupSessionMutationHandlerBase(service)
{
    public override async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!TryGetReservationAndUser(request, context, out string reservationId, out string userId, out APIGatewayProxyResponse? error) || !TryGetDishRequest(request, context, out UpdateGuestSessionDishRequest? body, out error))
            return error!;

        try
        {
            await Service.AddDishToOwnerSessionAsync(reservationId, userId, body!.DishId, body.Quantity);
            return ResponseBuilder.CreateSuccessResponse("Dish added successfully.");
        }
        catch (Exception exception)
        {
            return CreateError(exception, context);
        }
    }
}

public sealed class RemoveOwnerGroupSessionDishHandler(IGuestSessionService service) : OwnerGroupSessionMutationHandlerBase(service)
{
    public override async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!TryGetReservationAndUser(request, context, out string reservationId, out string userId, out APIGatewayProxyResponse? error) || !TryGetDishRequest(request, context, out UpdateGuestSessionDishRequest? body, out error))
            return error!;

        try
        {
            await Service.RemoveDishFromOwnerSessionAsync(reservationId, userId, body!.DishId, body.Quantity);
            return ResponseBuilder.CreateSuccessResponse("Dish removed successfully.");
        }
        catch (Exception exception)
        {
            return CreateError(exception, context);
        }
    }
}

public sealed class SubmitOwnerGroupSessionHandler(IGuestSessionService service) : OwnerGroupSessionMutationHandlerBase(service)
{
    public override async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!TryGetReservationAndUser(request, context, out string reservationId, out string userId, out APIGatewayProxyResponse? error))
            return error!;

        try
        {
            await Service.SubmitOwnerSessionAsync(reservationId, userId);
            return ResponseBuilder.CreateJsonResponse(200, new { message = "Cart submitted successfully." });
        }
        catch (Exception exception)
        {
            return CreateError(exception, context);
        }
    }
}
