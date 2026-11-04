using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.GuestSessions;
using APIHandler.Services.Locations;
using APIHandler.Services.Orders;
using AutoFixture.Xunit2;
using Moq;

namespace APIHandler.Tests.Services;

public class GroupReservationServiceTests
{
    private readonly Mock<IGroupReservationRepository> _groupReservationRepositoryMock = new();
    private readonly Mock<ITableRepository> _tableRepositoryMock = new();
    private readonly Mock<IReservationRepository> _reservationRepositoryMock = new();
    private readonly Mock<IGuestSessionRepository> _guestSessionRepositoryMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<ILocationService> _locationServiceMock = new();
    private readonly Mock<IOrderService> _orderServiceMock = new();
    private readonly Mock<IGuestSessionService> _guestSessionServiceMock = new();

    private readonly GroupReservationService _service;

    public GroupReservationServiceTests()
    {
        _service = new GroupReservationService(
            _groupReservationRepositoryMock.Object,
            _tableRepositoryMock.Object,
            _reservationRepositoryMock.Object,
            _guestSessionRepositoryMock.Object,
            _userServiceMock.Object,
            _locationServiceMock.Object,
            _orderServiceMock.Object,
            _guestSessionServiceMock.Object);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_ValidInput_ReturnsGroupReservationInfo(
        GroupReservation groupReservation,
        Table table,
        Reservation reservation,
        Location location,
        string expectedUserName)
    {
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;
        reservation.TableId = table.Id;
        table.LocationId = location.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(0);

        _tableRepositoryMock
            .Setup(r => r.GetTableAsync(table.Id))
            .ReturnsAsync(table);

        _locationServiceMock
            .Setup(s => s.GetLocationByIdAsync(table.LocationId))
            .ReturnsAsync(location);

        _userServiceMock
            .Setup(s => s.GetUserName(reservation.CustomerId!))
            .ReturnsAsync(expectedUserName);

        var result = await _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken);

        Assert.Equal(groupReservation.GroupReservationId, result.GroupReservationId);
        Assert.Equal(table.LocationId, result.LocationId);
        Assert.Equal(reservation.GuestCount, result.MaxGuestCount);
        Assert.Equal(reservation.StartTime, result.StartTime);
        Assert.Equal(reservation.EndTime, result.EndTime);
        Assert.Equal(table.TableNumber, result.TableNumber);
        Assert.Equal("Green & Tasty", result.RestaurantName);
        Assert.Equal(location.Address, result.LocationAddress);
        Assert.Equal(expectedUserName, result.InviterName);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_InvalidToken_ThrowsNotFound(string token)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(token))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.GetGroupReservationByTokenAsync(token));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed)]
    [InlineAutoData(GroupReservationStatus.Submitted)]
    public async Task GetGroupReservationByTokenAsync_GroupReservationIsNotOpen_ThrowsException(
        GroupReservationStatus status,
        GroupReservation groupReservation)
    {
        groupReservation.Status = status;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_WhenGuestCountIsMax_ReturnsGroupReservationInfo(
        GroupReservation groupReservation,
        Reservation reservation,
        Table table)
    {
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = reservation.Id;
        reservation.TableId = table.Id;
        reservation.CustomerId = null;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _tableRepositoryMock
            .Setup(r => r.GetTableAsync(table.Id))
            .ReturnsAsync(table);

        var result = await _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken);

        Assert.Equal(reservation.GuestCount, result.MaxGuestCount);
        _guestSessionRepositoryMock.Verify(
            r => r.CountByGroupReservationId(groupReservation.GroupReservationId),
            Times.Never);
    }

    [Theory]
    [AutoData]
    public async Task CreateGroupReservationIfDoesNotExistAsync_CreatesOwnerSession(
        string userId,
        string reservationId)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync((GroupReservation?)null);

        await _service.CreateGroupReservationIfDoesNotExistAsync(userId, reservationId);

        _guestSessionServiceMock.Verify(
            s => s.CreateGroupOwnerSessionAsync(userId, It.IsAny<string>()),
            Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_ValidInput_UpdatesGroupReservationStatus(GroupReservation groupReservation)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(groupReservation.ReservationId))
            .ReturnsAsync(groupReservation);

        GroupReservation? savedGroupReservation = null;
        _groupReservationRepositoryMock
            .Setup(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()))
            .Callback<GroupReservation>(r => savedGroupReservation = r);

        await _service.CloseGroupReservationAsync(groupReservation.ReservationId);

        Assert.NotNull(savedGroupReservation);
        Assert.Equal(GroupReservationStatus.Closed, savedGroupReservation.Status);
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_GroupReservationNotFound_DoesNotThrowException(string reservationId)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync((GroupReservation?)null);

        await _service.CloseGroupReservationAsync(reservationId);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_ValidInput_ReturnsToken(Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        GetGroupInviteTokenResponse result = await _service.GetGroupReservationInviteTokenAsync(reservation.Id);

        Assert.Equal(groupReservation.ReservationToken, result.Token);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_InvalidReservationId_ThrowsNotFound(string reservationId)
    {
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.GetGroupReservationInviteTokenAsync(reservationId));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_OneGuest_ThrowsValidationException(Reservation reservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(ReservationStatus.Completed)]
    [InlineAutoData(ReservationStatus.Cancelled)]
    [InlineAutoData(ReservationStatus.InProgress)]
    public async Task GetGroupReservationInviteTokenAsync_StatusIsNotReserved_ThrowsValidationException(ReservationStatus reservationStatus, Reservation reservation)
    {
        reservation.Status = reservationStatus;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    public async Task GetGroupReservationInviteTokenAsync_GroupStatusIsNotOpen_ThrowsException(GroupReservationStatus status, Type type, Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = status;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync(type, () => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_NonOwner_ThrowsForbidden()
    {
        var group = CreateGroup(GroupReservationStatus.Open);
        var reservation = CreateReservation(group.ReservationId, "owner-id");
        SetupGroupAndReservation(group, reservation);

        await Assert.ThrowsAsync<ActionForbiddenException>(() =>
            _service.SubmitGroupReservationAsync(group.GroupReservationId, reservation.Id, "other-user-id"));

        _orderServiceMock.Verify(s => s.SaveGroupOrderAsync(It.IsAny<Order>()), Times.Never);
        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_ConfirmedOwnerAndGuestCarts_AggregatesDishes()
    {
        var group = CreateGroup(GroupReservationStatus.Open);
        var reservation = CreateReservation(group.ReservationId, "owner-id");
        SetupGroupAndReservation(group, reservation);
        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([
                new GuestSession
                {
                    Id = "owner-session",
                    GroupReservationId = group.GroupReservationId,
                    GuestName = "Owner",
                    IsOwnerSession = true,
                    IsSubmitted = true,
                    Dishes = [new OrderItem { DishId = "dish-1", Quantity = 2 }]
                },
                new GuestSession
                {
                    Id = "guest-session",
                    GroupReservationId = group.GroupReservationId,
                    GuestName = "Guest",
                    IsSubmitted = true,
                    Dishes = [
                        new OrderItem { DishId = "dish-1", Quantity = 3 },
                        new OrderItem { DishId = "dish-2", Quantity = 1 }
                    ]
                },
                new GuestSession
                {
                    Id = "unsubmitted-session",
                    GroupReservationId = group.GroupReservationId,
                    GuestName = "Guest 2",
                    IsSubmitted = false,
                    Dishes = [new OrderItem { DishId = "dish-1", Quantity = 100 }]
                }
            ]);

        var existingOrder = CreateOrder(reservation.Id);
        _orderServiceMock
            .Setup(s => s.GetOrderByReservationIdAsync(reservation.Id))
            .ReturnsAsync(existingOrder);

        Order? savedOrder = null;
        _orderServiceMock
            .Setup(s => s.SaveGroupOrderAsync(It.IsAny<Order>()))
            .Callback<Order>(order => savedOrder = order);

        var result = await _service.SubmitGroupReservationAsync(group.GroupReservationId, reservation.Id, "owner-id");

        Assert.Equal(2, result.Count);
        Assert.Equal(5, result.Single(dish => dish.DishId == "dish-1").Quantity);
        Assert.Equal(1, result.Single(dish => dish.DishId == "dish-2").Quantity);
        Assert.NotNull(savedOrder);
        Assert.Equal(2, savedOrder.Dishes.Count);
        Assert.Equal(GroupReservationStatus.Submitted, group.Status);
        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(group), Times.Once);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_NoConfirmedDishes_ThrowsValidation()
    {
        var group = CreateGroup(GroupReservationStatus.Open);
        var reservation = CreateReservation(group.ReservationId, "owner-id");
        SetupGroupAndReservation(group, reservation);
        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([
                new GuestSession
                {
                    Id = "guest-session",
                    GroupReservationId = group.GroupReservationId,
                    GuestName = "Guest",
                    IsSubmitted = false,
                    Dishes = [new OrderItem { DishId = "dish-1", Quantity = 1 }]
                }
            ]);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.SubmitGroupReservationAsync(group.GroupReservationId, reservation.Id, "owner-id"));

        _orderServiceMock.Verify(s => s.SaveGroupOrderAsync(It.IsAny<Order>()), Times.Never);
        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_AlreadySubmitted_ThrowsWithoutSaving()
    {
        var group = CreateGroup(GroupReservationStatus.Submitted);
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);

        await Assert.ThrowsAsync<GroupReservationSubmittedException>(() =>
            _service.SubmitGroupReservationAsync(group.GroupReservationId, group.ReservationId, "owner-id"));

        _reservationRepositoryMock.Verify(r => r.GetReservationByIdAsync(It.IsAny<string>()), Times.Never);
        _orderServiceMock.Verify(s => s.SaveGroupOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_OrderSaveFails_DoesNotSaveGroupStatus()
    {
        var group = CreateGroup(GroupReservationStatus.Open);
        var reservation = CreateReservation(group.ReservationId, "owner-id");
        SetupGroupAndReservation(group, reservation);
        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(group.GroupReservationId))
            .ReturnsAsync([
                new GuestSession
                {
                    Id = "owner-session",
                    GroupReservationId = group.GroupReservationId,
                    GuestName = "Owner",
                    IsOwnerSession = true,
                    IsSubmitted = true,
                    Dishes = [new OrderItem { DishId = "dish-1", Quantity = 1 }]
                }
            ]);
        _orderServiceMock
            .Setup(s => s.GetOrderByReservationIdAsync(reservation.Id))
            .ReturnsAsync(CreateOrder(reservation.Id));
        _orderServiceMock
            .Setup(s => s.SaveGroupOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new InvalidOperationException("save failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SubmitGroupReservationAsync(group.GroupReservationId, reservation.Id, "owner-id"));

        Assert.Equal(GroupReservationStatus.Open, group.Status);
        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    private void SetupGroupAndReservation(GroupReservation group, Reservation reservation)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);
    }

    private static GroupReservation CreateGroup(GroupReservationStatus status)
        => new()
        {
            GroupReservationId = "group-id",
            ReservationId = "reservation-id",
            ReservationToken = "token",
            Status = status
        };

    private static Reservation CreateReservation(string reservationId, string customerId)
        => new()
        {
            Id = reservationId,
            CustomerId = customerId,
            TableId = "table-id",
            WaiterId = "waiter-id"
        };

    private static Order CreateOrder(string reservationId)
        => new()
        {
            OrderId = "order-id",
            CustomerId = "owner-id",
            ReservationId = reservationId,
            ReservationAddress = "address",
            ReservationTime = "time",
            Dishes = [],
            Status = OrderStatus.Created
        };
}
