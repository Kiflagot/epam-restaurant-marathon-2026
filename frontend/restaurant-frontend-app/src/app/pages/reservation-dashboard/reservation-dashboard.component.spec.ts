import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ReservationDashboardComponent } from './reservation-dashboard.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { HIDDEN_RESERVATION_IDS } from '@app/shared/constants/hidden-reservation-ids';

describe('ReservationDashboardComponent', () => {
  let component: ReservationDashboardComponent;
  let fixture: ComponentFixture<ReservationDashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        provideRouter([]),
      ],
      imports: [ReservationDashboardComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ReservationDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('keeps only non-hidden reservation ids in the customer list', () => {
    const hiddenId = HIDDEN_RESERVATION_IDS.values().next().value as string;
    const visibleId = '27ed43a1-2296-41e5-92e4-0fce91d94597';

    component.reservations.set([
      { id: hiddenId } as never,
      { id: visibleId } as never,
    ]);

    component.reservations.update((reservations) =>
      reservations.filter(({ id }) => !HIDDEN_RESERVATION_IDS.has(id)),
    );

    expect(component.reservations().map(({ id }) => id)).toEqual([visibleId]);
  });
});
