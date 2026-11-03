import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin, map, switchMap } from 'rxjs';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupReservationStatus } from '@app/models/group-orders/group-status.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';

@Component({
  selector: 'app-group-order-page',
  imports: [CurrencyPipe, DatePipe, ParticipantCardComponent],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly groupOrdersService = inject(GroupOrdersService);
  readonly invite = signal<GuestInvite | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly ownerSessionId = signal('');
  readonly groupStatus = signal<GroupReservationStatus>('Open');
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly submitDialogOpen = signal(false);
  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly consolidatedOrder = computed(() => consolidatePreOrder(this.participants()));
  readonly unsubmittedCount = computed(() => this.participants().filter((person) => !person.isSubmitted).length);
  readonly hasUnsubmittedParticipants = computed(() => this.unsubmittedCount() > 0);
  readonly statusLabel = computed(() => this.submitted() || this.groupStatus() === 'Submitted' ? 'Sent to Restaurant' : this.groupStatus() === 'Open' ? 'Collecting pre-orders' : 'Closed');
  readonly isEmpty = computed(() => !this.loading() && !this.errorMessage() && !this.consolidatedOrder().items.length);
  readonly submitDisabled = computed(() => this.submitting() || this.submitted() || !this.consolidatedOrder().items.length || this.groupStatus() !== 'Open');
  private readonly reservationId = this.route.snapshot.paramMap.get('reservationId') ?? '';

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    if (!this.reservationId) { this.setLoadError(new HttpErrorResponse({ status: 404 })); return; }
    this.groupOrdersService.getGroupInvite$(this.reservationId).pipe(
      switchMap((invite) => forkJoin({ sessions: this.groupOrdersService.getSessions$(invite.GroupReservationId), ownerSessionId: this.groupOrdersService.getOwnerSessionId$(this.reservationId), status: this.groupOrdersService.getStatus$(invite.GroupReservationId) }).pipe(map((result) => ({ invite, ...result })))),
    ).subscribe({
      next: ({ invite, sessions, ownerSessionId, status }) => { this.invite.set(invite); this.participants.set(sessions.participants); this.ownerSessionId.set(ownerSessionId); this.groupStatus.set(status.status); this.submitted.set(status.status === 'Submitted'); this.loading.set(false); },
      error: (error: unknown) => this.setLoadError(error),
    });
  }

  openSubmitDialog(): void { if (!this.submitDisabled()) this.submitDialogOpen.set(true); }
  closeSubmitDialog(): void { if (!this.submitting()) this.submitDialogOpen.set(false); }

  private setLoadError(error: unknown): void { this.loading.set(false); this.errorMessage.set(error instanceof HttpErrorResponse && error.status === 404 ? 'Group reservation not found' : 'Unable to load group reservation'); }
}
