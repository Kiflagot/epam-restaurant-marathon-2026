import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, map, switchMap } from 'rxjs';
import { GuestInviteService } from '@app/services/reservation/guest-invite.service';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { SubmittedDish, SubmitGroupPreOrderRequest } from '@app/models/group-orders/submit-group-pre-order.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

interface GuestSessionIdResponse {
  guestSessionId: string;
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
  CurrentParticipants: number;
  MaxCapacity: number;
  Status: GroupStatusResponse['status'];
}

@Injectable({ providedIn: 'root' })
export class GroupOrdersService {
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly http = inject(HttpClient);
  private readonly guestInviteService = inject(GuestInviteService);

  getGroupInvite$(reservationId: string): Observable<GuestInvite> {
    return this.guestInviteService.getInviteToken$(reservationId).pipe(
      switchMap(({ token }) => this.guestInviteService.getInvite$(token)),
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
        currentParticipants: response.CurrentParticipants,
        maxCapacity: response.MaxCapacity,
        status: response.Status,
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
