import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { HttpErrorResponse } from '@angular/common/http';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { Order } from '@app/models/orders/order.model';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { catchError, concatMap, finalize, forkJoin, from, map, switchMap, throwError, toArray, of, Observable, shareReplay } from 'rxjs';
import { CreateOrderRequest } from '@app/models/cart/create-order-request.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';

@Injectable({
  providedIn: 'root',
})
export class PreOrdersService {
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly http = inject(HttpClient);
  private readonly groupOrdersService = inject(GroupOrdersService);
  private activePreOrdersLoad$: Observable<Order[]> | null = null;

  getPreOrders(): Observable<Order[]> {
    if (this.activePreOrdersLoad$) {
      return this.activePreOrdersLoad$;
    }

    this.activePreOrdersLoad$ = forkJoin({
      orders: this.http.get<Order[]>(`${this.baseUrl}/orders`),
      reservations: this.http.get<Array<{ id: string }>>(`${this.baseUrl}/reservations`),
    }).pipe(
      switchMap(({ orders, reservations }) => from(reservations).pipe(
        concatMap((reservation) => this.groupOrdersService.getOwnerCartOrder$(reservation.id).pipe(
          catchError(() => of(null)),
        )),
        toArray(),
        map((groupOrders) => [
          ...orders,
          ...groupOrders.filter((order): order is Order => order !== null && !orders.some((item) => item.reservationId === order.reservationId)),
        ]),
      )),
      finalize(() => {
        this.activePreOrdersLoad$ = null;
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );

    return this.activePreOrdersLoad$;
  }

  getByReservationId(reservationId: string) {
    return this.http.get<Order>(`${this.baseUrl}/orders/${reservationId}`);
  }

  addDishToPreOrderByReservationId(
    reservationId: string,
    dish: DishListResponse,
    quantity: number
  ) {
    return this.groupOrdersService.getOwnerGroupReservation$(reservationId).pipe(
      switchMap(() => this.groupOrdersService.addOwnerDish$(reservationId, dish.id, quantity)),
      catchError((error: HttpErrorResponse) => this.isGroupReservationNotFound(error)
        ? this.addDishToRegularPreOrder(reservationId, dish, quantity)
        : throwError(() => error)),
    );
  }

  private addDishToRegularPreOrder(
    reservationId: string,
    dish: DishListResponse,
    quantity: number,
  ) {
    return this.getPreOrders().pipe(
      map((orders) => {
        const order = orders.find((o) => o.reservationId === reservationId);
        if (!order) {
          return order;
        }

        const existingItem = order.orderItems.find((item) => item.dish.id === dish.id);
        if (existingItem) {
          existingItem.quantity += quantity;
        } else {
          order.orderItems.push({ dish, quantity, state: 0 });
        }
        return order;
      }),
      switchMap((order) => {
        if (!order) {
          const orderCreateRequest: CreateOrderRequest = {
            reservationId,
            orderItems: [{ dish, quantity, state: 0 }],
          };
          return this.createPreOrder(orderCreateRequest);
        }

        return this.editPreOrderByReservationId(reservationId, order);
      })
    );
  }

  createPreOrder(order: CreateOrderRequest) {
    const createOrderRequest = {
      reservationId: order.reservationId,
      orderItems: order.orderItems.map((item) => ({
        dishId: item.dish.id,
        quantity: item.quantity,
      })),
    };

    return this.http.post<Order>(`${this.baseUrl}/orders`, createOrderRequest);
  }

  editPreOrderByReservationId(reservationId: string, order: Order) {
    const orderItems = order.orderItems.map((item) => ({
      dishId: item.dish.id,
      quantity: item.quantity,
    }));

    if (orderItems.length === 0) {
      return this.cancelPreOrderByReservationId(reservationId);
    }

    return this.http.put(`${this.baseUrl}/orders/${reservationId}`, { orderItems });
  }

  updateDishQuantityByReservationId$(reservationId: string, dishId: string, currentQuantity: number, quantity: number) {
    const delta = quantity - currentQuantity;
    return this.groupOrdersService.getOwnerGroupReservation$(reservationId).pipe(
      switchMap(() => delta > 0
        ? this.groupOrdersService.addOwnerDish$(reservationId, dishId, delta)
        : this.groupOrdersService.removeOwnerDish$(reservationId, dishId, -delta)),
      catchError((error: HttpErrorResponse) => this.isGroupReservationNotFound(error)
        ? this.updateRegularDishQuantity$(reservationId, dishId, quantity)
        : throwError(() => error)),
    );
  }

  removeDishFromPreOrderByReservationId$(reservationId: string, dishId: string, quantity: number) {
    return this.groupOrdersService.getOwnerGroupReservation$(reservationId).pipe(
      switchMap(() => this.groupOrdersService.removeOwnerDish$(reservationId, dishId, quantity)),
      catchError((error: HttpErrorResponse) => this.isGroupReservationNotFound(error)
        ? this.removeRegularDish$(reservationId, dishId)
        : throwError(() => error)),
    );
  }

  private updateRegularDishQuantity$(reservationId: string, dishId: string, quantity: number) {
    return this.getPreOrders().pipe(
      switchMap((orders) => {
        const order = orders.find((item) => item.reservationId === reservationId);
        if (!order) return throwError(() => new Error('Pre-order not found.'));

        const updatedOrder = {
          ...order,
          orderItems: order.orderItems.map((item) => item.dish.id === dishId ? { ...item, quantity } : item),
        };
        return this.editPreOrderByReservationId(reservationId, updatedOrder);
      }),
    );
  }

  private removeRegularDish$(reservationId: string, dishId: string) {
    return this.getPreOrders().pipe(
      switchMap((orders) => {
        const order = orders.find((item) => item.reservationId === reservationId);
        if (!order) return throwError(() => new Error('Pre-order not found.'));

        return this.editPreOrderByReservationId(reservationId, {
          ...order,
          orderItems: order.orderItems.filter((item) => item.dish.id !== dishId),
        });
      }),
    );
  }

  cancelPreOrderByReservationId(reservationId: string) {
    return this.http.delete(`${this.baseUrl}/orders/${reservationId}`);
  }

  submitPreOrderByReservationId(reservationId: string) {
    return this.groupOrdersService.getOwnerGroupReservation$(reservationId).pipe(
      switchMap(() => this.groupOrdersService.submitOwner$(reservationId)),
      catchError((error: HttpErrorResponse) => this.isGroupReservationNotFound(error)
        ? this.http.post(`${this.baseUrl}/orders/${reservationId}/submit`, {})
        : throwError(() => error)),
    );
  }

  unsubmitPreOrderByReservationId(reservationId: string) {
    return this.http.post(`${this.baseUrl}/orders/${reservationId}/unsubmit`, {});
  }

  confirmDishInOrder(reservationId: string, dishId: string) {
    return this.http.patch(
      `${this.baseUrl}/reservations/${reservationId}/order/confirmations/${dishId}`,
      {}
    );
  }

  rejectDishInOrder(reservationId: string, dishId: string) {
    return this.http.patch(
      `${this.baseUrl}/reservations/${reservationId}/order/cancellations/${dishId}`,
      {}
    );
  }

  private isGroupReservationNotFound(error: HttpErrorResponse): boolean {
    return error.status === 404;
  }
}
