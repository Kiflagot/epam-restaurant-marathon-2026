using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.GuestSessions;
using APIHandler.Services.Locations;
using APIHandler.Services.Orders;
using Moq;

namespace APIHandler.Tests.Services;

public class OwnerGroupReservationTests
{
    [Fact]
    public async Task GetOwnerGroupReservationAsync_Owner_ReturnsIdAndStatus()
    {
        var groupRepository = new Mock<IGroupReservationRepository>();
        var reservationRepository = new Mock<IReservationRepository>();
        var reservation = new Reservation
        {
            Id = "reservation-id",
            CustomerId = "owner-id",
            TableId = "table-id",
            WaiterId = "waiter-id"
        };
        var group = new GroupReservation
        {
            GroupReservationId = "group-id",
            ReservationId = reservation.Id,
            ReservationToken = "token",
            Status = GroupReservationStatus.Submitted
        };

        reservationRepository.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        groupRepository.Setup(r => r.GetByReservationIdAsync(reservation.Id)).ReturnsAsync(group);
        var service = CreateService(groupRepository, reservationRepository);

        var result = await service.GetOwnerGroupReservationAsync(reservation.Id, "owner-id");

        Assert.Equal("group-id", result.GroupReservationId);
        Assert.Equal("Submitted", result.Status);
    }

    [Fact]
    public async Task GetOwnerGroupReservationAsync_NonOwner_ThrowsForbidden()
    {
        var groupRepository = new Mock<IGroupReservationRepository>();
        var reservationRepository = new Mock<IReservationRepository>();
        var reservation = new Reservation
        {
            Id = "reservation-id",
            CustomerId = "owner-id",
            TableId = "table-id",
            WaiterId = "waiter-id"
        };
        reservationRepository.Setup(r => r.GetReservationByIdAsync(reservation.Id)).ReturnsAsync(reservation);
        var service = CreateService(groupRepository, reservationRepository);

        await Assert.ThrowsAsync<ActionForbiddenException>(() => service.GetOwnerGroupReservationAsync(reservation.Id, "other-user"));
        groupRepository.Verify(r => r.GetByReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    private static GroupReservationService CreateService(
        Mock<IGroupReservationRepository> groupRepository,
        Mock<IReservationRepository> reservationRepository)
        => new(
            groupRepository.Object,
            new Mock<ITableRepository>().Object,
            reservationRepository.Object,
            new Mock<IGuestSessionRepository>().Object,
            new Mock<IUserService>().Object,
            new Mock<ILocationService>().Object,
            new Mock<IOrderService>().Object,
            new Mock<IGuestSessionService>().Object);
}
