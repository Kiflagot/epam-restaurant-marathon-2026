import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { CardReservationComponent } from './card-reservation.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ReservationStatusCustomer } from '@app/models/reservations/reservation.model';

describe('CardReservationComponent', () => {
  let component: CardReservationComponent;
  let fixture: ComponentFixture<CardReservationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        provideRouter([]),
      ],
      imports: [CardReservationComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(CardReservationComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('shows Group Pre-Order and navigates with the reservation id', async () => {
    const reservationId = 'reservation-1';
    fixture.componentRef.setInput('r', {
      id: reservationId,
      tableId: 'table-1',
      startTime: new Date('2026-01-01T12:00:00Z'),
      endTime: new Date('2026-01-01T13:00:00Z'),
      guestCount: 2,
      status: ReservationStatusCustomer.Reserved,
      restaurantName: 'Restaurant',
      locationAddress: 'Address',
      waiterId: 'waiter-1',
      guestInviteLinkAvailable: true,
    });
    fixture.detectChanges();

    const button = Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button'))
      .find((element) => element.textContent?.trim() === 'Group Pre-Order');
    expect(button).toBeTruthy();
    if (!button) {
      return;
    }

    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.returnValue(Promise.resolve(true));
    button.click();

    expect(navigateSpy).toHaveBeenCalledWith([
      '/reservations',
      reservationId,
      'group-order',
    ]);
  });

  it('does not show Group Pre-Order for a regular reservation', () => {
    fixture.componentRef.setInput('r', {
      id: 'reservation-1',
      tableId: 'table-1',
      startTime: new Date('2026-01-01T12:00:00Z'),
      endTime: new Date('2026-01-01T13:00:00Z'),
      guestCount: 2,
      status: ReservationStatusCustomer.Reserved,
      restaurantName: 'Restaurant',
      locationAddress: 'Address',
      waiterId: 'waiter-1',
      guestInviteLinkAvailable: false,
    });
    fixture.detectChanges();

    const groupButton = Array.from<HTMLButtonElement>(fixture.nativeElement.querySelectorAll('button'))
      .find((element) => element.textContent?.trim() === 'Group Pre-Order');
    expect(groupButton).toBeUndefined();
  });
});
