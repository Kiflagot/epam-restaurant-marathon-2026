import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, forkJoin, map, of, switchMap } from 'rxjs';
import { Order } from '@app/models/orders/order.model';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { SubmittedDish, SubmitGroupPreOrderRequest } from '@app/models/group-orders/submit-group-pre-order.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

interface GuestSessionIdResponse {
  guestSessionId: string;
}

export interface OwnerGroupReservationResponse {
  groupReservationId: string;
  status: GroupStatusResponse['status'];
}

interface BackendOwnerGroupReservationResponse {
  GroupReservationId: string;
  Status: GroupStatusResponse['status'];
}

export interface OwnerReservationResponse {
  id: string;
  startTime: string;
  endTime: string;
  guestCount: number;
  locationAddress: string;
}

interface BackendGroupSessionsResponse {
  Participants: BackendParticipant[];
  Total: number;
}

interface BackendParticipant {
  Id: string | null;
  Type: Participant['type'];
  Name: string;
  Items: Participant['items'];
  Total: number;
  IsSubmitted: boolean;
}

interface BackendGroupStatusResponse {
  currentParticipants: number;
  maxCapacity: number;
  status: GroupStatusResponse['status'];
}

@Injectable({ providedIn: 'root' })
export class GroupOrdersService {
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly http = inject(HttpClient);

  getOwnerCartOrder$(reservationId: string): Observable<Order | null> {
    return this.getOwnerGroupReservation$(reservationId).pipe(
      switchMap((groupReservation) => forkJoin({
        reservation: this.getOwnerReservation$(reservationId),
        sessions: this.getSessions$(groupReservation.groupReservationId),
      }).pipe(
        map(({ reservation, sessions }) => {
          const owner = sessions.participants.find((participant) => participant.type === 'Owner');
          if (!owner || !owner.items.length) return null;

          return {
            orderId: `group-${groupReservation.groupReservationId}`,
            reservationAddress: reservation.locationAddress,
            reservationTime: reservation.startTime,
            reservationId,
            orderItems: owner.items.map((item) => ({
              dish: this.toCartDish(item.dish),
              quantity: item.quantity,
              state: 0,
            })),
            status: owner.isSubmitted ? 3 : 0,
          } satisfies Order;
        }),
      )),
    );
  }

  private toCartDish(dish: Participant['items'][number]['dish']): DishListResponse {
    return {
      id: dish.id,
      name: dish.name,
      price: dish.price,
      imageUrl: dish.imageUrl ?? '',
      state: 'Available',
      weight: dish.weight ?? '',
      dishType: '',
      description: '',
      calories: '',
      proteins: '',
      carbohydrates: '',
      fats: '',
      vitamins: '',
    };
  }

  addOwnerDish$(reservationId: string, dishId: string, quantity: number) {
    return this.http.post(
      `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/group-session/dishes`,
      { dishId, quantity },
    );
  }

  removeOwnerDish$(reservationId: string, dishId: string, quantity: number) {
    return this.http.request(
      'DELETE',
      `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/group-session/dishes`,
      { body: { dishId, quantity } },
    );
  }

  submitOwner$(reservationId: string) {
    return this.http.post(
      `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/group-session/submit`,
      {},
    );
  }

  getOwnerGroupReservation$(reservationId: string): Observable<OwnerGroupReservationResponse> {
    return this.http.get<BackendOwnerGroupReservationResponse>(
      `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/group-reservation`,
    ).pipe(
      map((response) => ({
        groupReservationId: response.GroupReservationId,
        status: response.Status,
      })),
    );
  }

  getOwnerReservation$(reservationId: string): Observable<OwnerReservationResponse> {
    return this.http.get<OwnerReservationResponse>(
      `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}`,
    );
  }

  getSessions$(groupReservationId: string): Observable<GroupSessionsResponse> {
    return this.http.get<BackendGroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/sessions`,
    ).pipe(
      map((response) => ({
        participants: response.Participants.map((participant) => ({
          id: participant.Id ?? '',
          type: participant.Type,
          name: participant.Name,
          items: participant.Items,
          total: participant.Total,
          isSubmitted: participant.IsSubmitted,
        })),
        total: response.Total,
      })),
    );
  }

  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.http
      .get<GuestSessionIdResponse>(
        `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/guest-session-id`,
      )
      .pipe(switchMap(({ guestSessionId }) => [guestSessionId]));
  }

  getStatus$(groupReservationId: string): Observable<GroupStatusResponse> {
    return this.http.get<BackendGroupStatusResponse>(
      `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/status`,
    ).pipe(
      map((response) => ({
        currentParticipants: response.currentParticipants,
        maxCapacity: response.maxCapacity,
        status: response.status,
      })),
    );
  }

  submit$(groupReservationId: string, reservationId: string): Observable<SubmittedDish[]> {
    const body: SubmitGroupPreOrderRequest = { reservationId };
    return this.http.post<SubmittedDish[]>(
      `${this.baseUrl}/group-reservations/${encodeURIComponent(groupReservationId)}/submit`,
      body,
    );
  }
}
