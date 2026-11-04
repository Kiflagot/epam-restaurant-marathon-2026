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
    service.getOwnerGroupReservation$('res-1').subscribe();

    const request = http.expectOne(`${baseUrl}/reservations/res-1/group-reservation`);
    expect(request.request.method).toBe('GET');
    request.flush({ groupReservationId: 'group-1', status: 'Submitted' });
  });
});
