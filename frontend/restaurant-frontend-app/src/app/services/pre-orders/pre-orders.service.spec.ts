import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { PreOrdersService } from './pre-orders.service';

describe('PreOrdersService Cart loading', () => {
  let service: PreOrdersService;
  let http: HttpTestingController;
  let groupOrders: jasmine.SpyObj<GroupOrdersService>;

  beforeEach(() => {
    groupOrders = jasmine.createSpyObj('GroupOrdersService', ['getOwnerCartOrder$', 'getOwnerGroupReservation$']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'https://api.test' },
        { provide: GroupOrdersService, useValue: groupOrders },
      ],
    });
    service = TestBed.inject(PreOrdersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('shares one active Cart load and keeps regular orders when group lookup returns 404', () => {
    groupOrders.getOwnerCartOrder$.and.returnValue(throwError(() => new HttpErrorResponse({ status: 404 })));

    service.getPreOrders().subscribe();
    service.getPreOrders().subscribe();

    const orders = http.expectOne('https://api.test/orders');
    const reservations = http.expectOne('https://api.test/reservations');
    expect(http.match('https://api.test/orders').length).toBe(0);
    expect(http.match('https://api.test/reservations').length).toBe(0);

    orders.flush([{
      orderId: 'order-1',
      reservationAddress: 'Address',
      reservationTime: '2026-01-01T12:00:00Z',
      reservationId: 'reservation-1',
      orderItems: [],
      status: 0,
    }]);
    reservations.flush([{ id: 'reservation-1' }]);
  });

  it('falls back to the regular order flow for add after group reservation 404', () => {
    groupOrders.getOwnerGroupReservation$.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 404 })),
    );
    groupOrders.getOwnerCartOrder$.and.returnValue(of(null));
    service.addDishToPreOrderByReservationId('reservation-1', {
      id: 'dish-1', name: 'Dish', price: 10, imageUrl: '', state: 'Available',
      weight: '', dishType: '', description: '', calories: '', proteins: '', carbohydrates: '', fats: '', vitamins: '',
    }, 2).subscribe();

    const orders = http.expectOne('https://api.test/orders');
    const reservations = http.expectOne('https://api.test/reservations');
    orders.flush([]);
    reservations.flush([{ id: 'reservation-1' }]);

    const create = http.expectOne('https://api.test/orders');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ reservationId: 'reservation-1', orderItems: [{ dishId: 'dish-1', quantity: 2 }] });
    create.flush({});
  });
});
