using APIHandler.Constants.Dishes;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GuestSessions;
using Moq;

namespace APIHandler.Tests.Services;

public class OwnerGuestSessionMutationTests
{
    [Fact]
    public async Task AddDishToOwnerSessionAsync_OwnerOpenGroup_SavesOwnerSession()
    {
        var group = CreateGroup(GroupReservationStatus.Open);
        var reservation = CreateReservation(group.ReservationId, "owner-id");
        var ownerSession = CreateOwnerSession(group.GroupReservationId, "owner-id");
        var dish = new Dish { Id = "dish-id", Name = "Dish", State = DishState.Available };
        var groupRepository = new Mock<IGroupReservationRepository>();
        var reservationRepository = new Mock<IReservationRepository>();
        var sessionRepository = new Mock<IGuestSessionRepository>();
        var dishRepository = new Mock<IDishRepository>();
        var tableRepository = new Mock<ITableRepository>();
        Setup(groupRepository, reservationRepository, sessionRepository, reservation, group, ownerSession);
        reservation.TableId = "table-id";
        tableRepository.Setup(r => r.GetTableAsync(reservation.TableId)).ReturnsAsync(new Table { Id = reservation.TableId, LocationId = "location-id" });
        dishRepository.Setup(r => r.GetDishesByLocationIdAsync("location-id")).ReturnsAsync([dish]);
        var service = CreateService(groupRepository, reservationRepository, sessionRepository, dishRepository, tableRepository);

        await service.AddDishToOwnerSessionAsync(reservation.Id, "owner-id", dish.Id, 2);

        Assert.Equal(2, ownerSession.Dishes.Single().Quantity);
        sessionRepository.Verify(r => r.SaveAsync(ownerSession), Times.Once);
    }

    [Fact]
    public async Task AddDishToOwnerSessionAsync_NonOwner_ThrowsForbidden()
    {
        var group = CreateGroup(GroupReservationStatus.Open);
        var reservation = CreateReservation(group.ReservationId, "owner-id");
        var groupRepository = new Mock<IGroupReservationRepository>();
        var reservationRepository = new Mock<IReservationRepository>();
        reservationRepository.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        var service = CreateService(groupRepository, reservationRepository, new Mock<IGuestSessionRepository>(), new Mock<IDishRepository>(), new Mock<ITableRepository>());

        await Assert.ThrowsAsync<ActionForbiddenException>(() => service.AddDishToOwnerSessionAsync(reservation.Id, "other-id", "dish-id", 1));
    }

    [Fact]
    public async Task SubmitOwnerSessionAsync_SubmittedGroup_ThrowsWithoutSaving()
    {
        var group = CreateGroup(GroupReservationStatus.Submitted);
        var reservation = CreateReservation(group.ReservationId, "owner-id");
        var repository = new Mock<IGroupReservationRepository>();
        var reservationRepository = new Mock<IReservationRepository>();
        var sessionRepository = new Mock<IGuestSessionRepository>();
        reservationRepository.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        repository.Setup(r => r.GetByReservationIdAsync(reservation.Id)).ReturnsAsync(group);
        var service = CreateService(repository, reservationRepository, sessionRepository, new Mock<IDishRepository>(), new Mock<ITableRepository>());

        await Assert.ThrowsAsync<GroupReservationSubmittedException>(() => service.SubmitOwnerSessionAsync(reservation.Id, "owner-id"));
        sessionRepository.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    private static void Setup(Mock<IGroupReservationRepository> groups, Mock<IReservationRepository> reservations, Mock<IGuestSessionRepository> sessions, Reservation reservation, GroupReservation group, GuestSession owner)
    {
        reservations.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        groups.Setup(r => r.GetByReservationIdAsync(reservation.Id)).ReturnsAsync(group);
        groups.Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId)).ReturnsAsync(group);
        sessions.Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId)).ReturnsAsync([owner]);
    }

    private static GuestSessionService CreateService(Mock<IGroupReservationRepository> groups, Mock<IReservationRepository> reservations, Mock<IGuestSessionRepository> sessions, Mock<IDishRepository> dishes, Mock<ITableRepository> tables)
        => new(groups.Object, sessions.Object, dishes.Object, reservations.Object, new Mock<IUserRepository>().Object, tables.Object);

    private static GroupReservation CreateGroup(GroupReservationStatus status) => new() { GroupReservationId = "group-id", ReservationId = "reservation-id", ReservationToken = "token", Status = status };
    private static Reservation CreateReservation(string id, string customerId) => new() { Id = id, CustomerId = customerId, TableId = "table-id", WaiterId = "waiter-id" };
    private static GuestSession CreateOwnerSession(string groupId, string userId) => new() { Id = "owner-session", GroupReservationId = groupId, GuestName = "Owner", OwnerUserId = userId, IsOwnerSession = true, Dishes = [] };
}
