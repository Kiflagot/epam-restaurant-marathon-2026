using APIHandler.Constants;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Models.GuestReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GuestSessions;
using APIHandler.Services.Locations;
using APIHandler.Services.Orders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Services.GroupReservations;

public class GroupReservationService(
    IGroupReservationRepository groupReservationRepository,
    ITableRepository tableRepository,
    IReservationRepository reservationRepository,
    IGuestSessionRepository guestSessionRepository,
    IUserService userService,
    ILocationService locationService,
    IOrderService orderService,
    IGuestSessionService guestSessionService) : IGroupReservationService
{
    private const string DefaultRestaurantName = "Green & Tasty";

    public async Task<GetGroupReservationResponse> GetGroupReservationByTokenAsync(string token)
    {
        GroupReservation groupReservation = await groupReservationRepository.GetGroupReservationByTokenAsync(token)
            ?? throw new GroupReservationNotFoundException();

        if (groupReservation.Status != GroupReservationStatus.Open)
            throw new GroupReservationExpiredException();

        Task<Reservation?> reservationTask = reservationRepository.GetReservationByIdAsync(groupReservation.ReservationId);

        Reservation reservation = await reservationTask ?? throw new ValidationException("Associated reservation not found.");
        Table table = await tableRepository.GetTableAsync(reservation.TableId)
            ?? throw new InvalidOperationException("Associated table not found.");

        Location? location = await locationService.GetLocationByIdAsync(table.LocationId);
        string restaurantName = DefaultRestaurantName;
        string address = location?.Address ?? "Unknown Address";

        string inviterName = "Unknown";
        if (!string.IsNullOrWhiteSpace(reservation.CustomerId))
        {
            inviterName = await userService.GetUserName(reservation.CustomerId) ?? "Unknown";
        }
        return new GetGroupReservationResponse(
            groupReservation.GroupReservationId,
            table.LocationId,
            restaurantName,
            address,
            reservation.StartTime,
            reservation.EndTime,
            reservation.GuestCount,
            table.TableNumber,
            inviterName);
    }

    public async Task<GroupReservation> GetGroupReservationAsync(string groupReservationId)
    {
        GroupReservation? groupReservation = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId);
        return groupReservation ?? throw new GroupReservationNotFoundException();
    }

    public async Task<GroupReservation> CreateGroupReservationAsync(string reservationId)
    {
        var groupReservation = new GroupReservation
        {
            GroupReservationId = Guid.NewGuid().ToString(),
            ReservationId = reservationId,
            ReservationToken = Guid.NewGuid().ToString("N"),
            Status = GroupReservationStatus.Open,
        };

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
        return groupReservation;
    }

    public async Task CreateGroupReservationIfDoesNotExistAsync(string userId, string reservationId)
    {
        GroupReservation? group = await groupReservationRepository.GetByReservationIdAsync(reservationId);
        if (group is not null)
            return;

        GroupReservation createdGroupReservation = await CreateGroupReservationAsync(reservationId);
        await guestSessionService.CreateGroupOwnerSessionAsync(userId, createdGroupReservation.GroupReservationId);
    }

    public async Task UpdateGroupReservationStatusAsync(string groupReservationId, GroupReservationStatus status)
    {
        var groupReservation = await GetGroupReservationAsync(groupReservationId);

        groupReservation.Status = status;

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
    }

    public async Task DeleteGroupReservationAsync(string groupReservationId)
    {
        var groupReservation = await GetGroupReservationAsync(groupReservationId);

        await groupReservationRepository.DeleteGroupReservationAsync(groupReservation);
    }

    public async Task CloseGroupReservationAsync(string reservationId)
    {
        GroupReservation? groupReservation = await groupReservationRepository.GetByReservationIdAsync(reservationId);
        if (groupReservation is null)
            return;

        groupReservation.Status = GroupReservationStatus.Closed;

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
    }

    public async Task<GetGroupInviteTokenResponse> GetGroupReservationInviteTokenAsync(string reservationId)
    {
        var reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException($"Reservation {reservationId} was not found.");

        if (reservation.GuestCount < 2)
            throw new ValidationException("Retrieving invitation token for the reservation with only one guest is not permitted.");

        if (reservation.Status != ReservationStatus.Reserved)
            throw new GroupReservationExpiredException("The group reservation is expired.");

        GroupReservation groupReservation = (await groupReservationRepository.GetByReservationIdAsync(reservationId))!;

        if (groupReservation.Status == GroupReservationStatus.Submitted)
            throw new GroupReservationSubmittedException();

        if (groupReservation.Status == GroupReservationStatus.Closed)
            throw new GroupReservationExpiredException();

        return new GetGroupInviteTokenResponse(groupReservation.ReservationToken);
    }

    public async Task<IReadOnlyList<SubmittedDishResponse>> SubmitGroupReservationAsync(
        string groupReservationId,
        string reservationId,
        string userId)
    {
        GroupReservation groupReservation = await GetGroupReservationAsync(groupReservationId);

        if (groupReservation.Status == GroupReservationStatus.Submitted)
            throw new GroupReservationSubmittedException();

        if (groupReservation.Status == GroupReservationStatus.Closed)
            throw new GroupReservationExpiredException();

        if (!string.Equals(groupReservation.ReservationId, reservationId, StringComparison.Ordinal))
            throw new ValidationException("The reservation does not belong to the group reservation.");

        Reservation reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException("Reservation not found.");

        if (!string.Equals(reservation.CustomerId, userId, StringComparison.OrdinalIgnoreCase))
            throw new ActionForbiddenException("Only the reservation owner can submit the group reservation.");

        var submittedDishes = (await guestSessionRepository.GetAllByGroupReservationIdAsync(groupReservationId))
            .Where(session => session.IsSubmitted)
            .SelectMany(session => session.Dishes ?? [])
            .GroupBy(dish => dish.DishId)
            .Select(group => new SubmittedDishResponse(group.Key, group.Sum(dish => dish.Quantity)))
            .Where(dish => dish.Quantity > 0)
            .ToArray();

        if (submittedDishes.Length == 0)
            throw new ValidationException("No submitted guest session contains dishes.");

        Order? existingOrder = await orderService.GetOrderByReservationIdAsync(reservationId);
        Order order = existingOrder ?? await CreateGroupOrderAsync(reservation, submittedDishes);
        order.Dishes = submittedDishes
            .Select(dish => new OrderItem
            {
                DishId = dish.DishId,
                Quantity = dish.Quantity,
                State = OrderItemState.Pending
            })
            .ToList();

        await orderService.SaveGroupOrderAsync(order);

        groupReservation.Status = GroupReservationStatus.Submitted;
        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);

        return submittedDishes;
    }

    private async Task<Order> CreateGroupOrderAsync(Reservation reservation, IReadOnlyList<SubmittedDishResponse> dishes)
    {
        var location = await locationService.GetLocationByTableIdAsync(reservation.TableId);
        return new Order
        {
            OrderId = Guid.NewGuid().ToString(),
            CustomerId = reservation.CustomerId ?? string.Empty,
            ReservationId = reservation.Id,
            ReservationAddress = location.Address,
            ReservationTime = $"{reservation.StartTime:MMM dd, yyyy, h:mm tt} - {reservation.EndTime:h:mm tt}",
            Dishes = dishes.Select(dish => new OrderItem
            {
                DishId = dish.DishId,
                Quantity = dish.Quantity,
                State = OrderItemState.Pending
            }).ToList(),
            Status = OrderStatus.Created
        };
    }
}

