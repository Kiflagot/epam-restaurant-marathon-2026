import { Participant } from '@app/models/group-orders/participant.model';

export interface ConsolidatedOrderItem {
  dish: Participant['items'][number]['dish'];
  quantity: number;
}

export interface ConsolidatedOrder {
  items: ConsolidatedOrderItem[];
  total: number;
}

export function consolidatePreOrder(participants: Participant[]): ConsolidatedOrder {
  const byDish = new Map<string, ConsolidatedOrderItem>();

  for (const participant of participants.filter((person) => person.isSubmitted)) {
    for (const item of participant.items) {
      const existing = byDish.get(item.dish.id);
      byDish.set(item.dish.id, {
        dish: item.dish,
        quantity: (existing?.quantity ?? 0) + item.quantity,
      });
    }
  }

  const items = [...byDish.values()];
  const total = items.reduce((sum, item) => sum + item.dish.price * item.quantity, 0);
  return { items, total };
}
