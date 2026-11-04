/** Reserved for the backend group-submit contract when it is implemented. */
export interface SubmitGroupPreOrderRequest {
  reservationId: string;
}

export interface SubmittedDish {
  dishId: string;
  quantity: number;
}
