import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, map, switchMap } from 'rxjs';
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

  getOwnerGroupReservation$(reservationId: string): Observable<OwnerGroupReservationResponse> {
    return this.http.get<OwnerGroupReservationResponse>(
      `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/group-reservation`,
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
