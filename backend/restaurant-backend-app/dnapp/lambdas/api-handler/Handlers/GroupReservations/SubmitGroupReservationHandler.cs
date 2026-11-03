using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GroupReservation;
using APIHandler.Services.GroupReservations;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

public class SubmitGroupReservationHandler(IGroupReservationService groupReservationService) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("groupReservationId", out string? groupReservationId))
            return ResponseBuilder.CreateErrorResponse(400, "Missing 'groupReservationId' in path parameters.", context.Logger);

        string? userId = UserContextHelper.GetAuthenticatedUserId(request);
        if (userId is null)
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);

        if (!TryParseRequest(request.Body, out SubmitGroupReservationRequest? submitRequest))
            return ResponseBuilder.CreateErrorResponse(400, "Invalid request body.", context.Logger);

        try
        {
            var result = await groupReservationService.SubmitGroupReservationAsync(
                groupReservationId,
                submitRequest!.ReservationId,
                userId);

            return ResponseBuilder.CreateJsonResponse(200, result);
        }
        catch (GroupReservationNotFoundException exception)
        {
            return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger);
        }
        catch (ReservationNotFoundException exception)
        {
            return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger);
        }
        catch (ActionForbiddenException exception)
        {
            return ResponseBuilder.CreateErrorResponse(403, exception.Message, context.Logger);
        }
        catch (ValidationException exception)
        {
            return ResponseBuilder.CreateErrorResponse(400, exception.Message, context.Logger);
        }
        catch (Exception exception)
        {
            return ResponseBuilder.CreateErrorResponse(500, "Internal Server Error", context.Logger, exception);
        }
    }

    private static bool TryParseRequest(string? body, out SubmitGroupReservationRequest? request)
    {
        try
        {
            request = JsonSerializer.Deserialize<SubmitGroupReservationRequest>(body ?? string.Empty);
            return request is not null && !string.IsNullOrWhiteSpace(request.ReservationId);
        }
        catch (JsonException)
        {
            request = null;
            return false;
        }
    }
}
