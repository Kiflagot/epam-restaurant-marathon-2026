import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin, map, of, switchMap } from 'rxjs';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { GroupReservationStatus } from '@app/models/group-orders/group-status.model';
import { Participant } from '@app/models/group-orders/participant.model';
import { GroupOrdersService, OwnerGroupReservationResponse, OwnerReservationResponse } from '@app/services/group-orders/group-orders.service';
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
  readonly invite = signal<(OwnerGroupReservationResponse & OwnerReservationResponse) | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly ownerSessionId = signal('');
  readonly groupStatus = signal<GroupReservationStatus>('Open');
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly submitDialogOpen = signal(false);
  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly submitErrorMessage = signal<string | null>(null);
  readonly submitSuccessMessage = signal<string | null>(null);
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
    this.groupOrdersService.getOwnerGroupReservation$(this.reservationId).pipe(
      switchMap((groupReservation) => forkJoin({ reservation: this.groupOrdersService.getOwnerReservation$(this.reservationId), sessions: this.groupOrdersService.getSessions$(groupReservation.groupReservationId), ownerSessionId: this.groupOrdersService.getOwnerSessionId$(this.reservationId), status: this.groupOrdersService.getStatus$(groupReservation.groupReservationId) }).pipe(map((result) => ({ groupReservation, ...result })))),
    ).subscribe({
      next: ({ groupReservation, reservation, sessions, ownerSessionId, status }) => { this.invite.set({ ...groupReservation, ...reservation }); this.participants.set(sessions.participants); this.ownerSessionId.set(ownerSessionId); this.groupStatus.set(status.status); this.submitted.set(status.status === 'Submitted'); this.loading.set(false); },
      error: (error: unknown) => this.setLoadError(error),
    });
  }

  openSubmitDialog(): void {
    if (this.submitDisabled()) return;
    this.submitErrorMessage.set(null);
    this.submitDialogOpen.set(true);
  }

  submit(): void {
    const currentInvite = this.invite();
    if (this.submitDisabled() || !currentInvite) return;

    this.submitting.set(true);
    this.submitErrorMessage.set(null);
    this.groupOrdersService.getStatus$(currentInvite.groupReservationId).pipe(
      switchMap((status) => {
        this.groupStatus.set(status.status);

        if (status.status === 'Submitted') {
          this.submitted.set(true);
          return of({ status: 'Submitted' as const, message: 'The group pre-order has already been submitted.' });
        }

        if (status.status === 'Closed') {
          return of({ status: 'Closed' as const, message: 'The group pre-order is closed.' });
        }

        return this.groupOrdersService.submit$(currentInvite.groupReservationId, this.reservationId).pipe(
          map(() => ({ status: 'Submitted' as const, message: 'Group pre-order was sent to the restaurant.' })),
        );
      }),
    ).subscribe({
      next: (result) => {
        this.submitting.set(false);
        this.submitDialogOpen.set(false);
        this.groupStatus.set(result.status);
        this.submitted.set(result.status === 'Submitted');
        this.submitSuccessMessage.set(result.message);
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.submitErrorMessage.set(this.getSubmitErrorMessage(error));
      },
    });
  }

  closeSubmitDialog(): void { if (!this.submitting()) this.submitDialogOpen.set(false); }

  private setLoadError(error: unknown): void { this.loading.set(false); this.errorMessage.set(error instanceof HttpErrorResponse && error.status === 404 ? 'Group reservation not found' : 'Unable to load group reservation'); }

  private getSubmitErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 400) return error.error?.error ?? 'Unable to submit group pre-order.';
    if (error instanceof HttpErrorResponse && error.status === 403) return 'Only the reservation owner can submit this pre-order.';
    if (error instanceof HttpErrorResponse && error.status === 401) return 'Please log in to submit the group pre-order.';
    return 'Unable to submit group pre-order. Please try again.';
  }
}
