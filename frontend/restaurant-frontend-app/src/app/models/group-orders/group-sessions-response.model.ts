import { Participant } from './participant.model';

/** GET /guest/groups/{groupReservationId}/sessions */
export interface GroupSessionsResponse {
  participants: Participant[];
  total: number;
}
