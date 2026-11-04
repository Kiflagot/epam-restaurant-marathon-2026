import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupOrdersService } from './group-orders.service';

describe('GroupOrdersService owner loading', () => {
  let service: GroupOrdersService;
  let http: HttpTestingController;
  const baseUrl = 'https://api.test';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: baseUrl }],
    });
    service = TestBed.inject(GroupOrdersService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads owner group metadata without guest invite endpoints', () => {
    let result: unknown;
    service.getOwnerGroupReservation$('res-1').subscribe((response) => (result = response));

    const request = http.expectOne(`${baseUrl}/reservations/res-1/group-reservation`);
    expect(request.request.method).toBe('GET');
    request.flush({ GroupReservationId: 'group-1', Status: 'Submitted' });

    expect(result).toEqual({ groupReservationId: 'group-1', status: 'Submitted' });
  });

  it('adds a dish to the owner group session', () => {
    service.addOwnerDish$('res-1', 'dish-1', 2).subscribe();
    const request = http.expectOne(`${baseUrl}/reservations/res-1/group-session/dishes`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ dishId: 'dish-1', quantity: 2 });
    request.flush({});
  });

  it('submits the owner group session', () => {
    service.submitOwner$('res-1').subscribe();
    const request = http.expectOne(`${baseUrl}/reservations/res-1/group-session/submit`);
    expect(request.request.method).toBe('POST');
    request.flush({});
  });

  it('maps the owner participant to a Cart order', () => {
    let result: unknown;
    service.getOwnerCartOrder$('res-1').subscribe((order) => (result = order));

    http.expectOne(`${baseUrl}/reservations/res-1/group-reservation`).flush({
      GroupReservationId: 'group-1',
      Status: 'Open',
    });
    http.expectOne(`${baseUrl}/reservations/res-1`).flush({
      id: 'res-1',
      locationAddress: 'Address',
      startTime: '2026-01-01T12:00:00Z',
    });
    http.expectOne(`${baseUrl}/guest/groups/group-1/sessions`).flush({
      Participants: [{
        Id: 'owner-session',
        Type: 'Owner',
        Name: 'Owner',
        Items: [{ dish: { id: 'dish-1', name: 'Dish', price: 10 }, quantity: 2 }],
        Total: 20,
        IsSubmitted: false,
      }],
      Total: 20,
    });

    expect(result).toEqual(jasmine.objectContaining({
      reservationId: 'res-1',
      status: 0,
      orderItems: [jasmine.objectContaining({ quantity: 2 })],
    }));
  });
});
