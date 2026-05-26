export type OrderBadgeTone = 'neutral' | 'success' | 'warning' | 'danger' | 'info' | 'ember' | 'hot';

export function getOrderStatusLabel(status: number): string {
  return Number.isFinite(status) ? `Status ${status}` : 'Status -';
}

export function getOrderStatusTone(): OrderBadgeTone {
  return 'neutral';
}

export function formatDateTime(value: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);

  if (!Number.isFinite(date.getTime())) {
    return '-';
  }

  return new Intl.DateTimeFormat('en', {
    month: 'short',
    day: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date);
}

export function formatDateShort(value: string | null): string {
  if (!value) {
    return '-';
  }

  const date = new Date(value);

  if (!Number.isFinite(date.getTime())) {
    return '-';
  }

  return new Intl.DateTimeFormat('en', {
    month: 'short',
    day: '2-digit',
    year: 'numeric'
  }).format(date);
}

export function formatNumber(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return '-';
  }

  return new Intl.NumberFormat('en', {
    maximumFractionDigits: 2
  }).format(value);
}

export function compactId(value: string | null | undefined): string {
  if (!value) {
    return '-';
  }

  return value.length > 12 ? `${value.slice(0, 8)}...${value.slice(-4)}` : value;
}

export function fieldValue(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === '' ? '-' : String(value);
}

export function getOrderSortLabel(value: string): string {
  const direction = value.startsWith('-') ? 'desc' : 'asc';
  const field = value.replace(/^-/, '');

  if (field === 'dateOpened') {
    return `Opened ${direction}`;
  }

  if (field === 'dateUpdate') {
    return `Updated ${direction}`;
  }

  if (field === 'price') {
    return `Price ${direction}`;
  }

  if (field === 'amount') {
    return `Amount ${direction}`;
  }

  return `${field} ${direction}`;
}

export function getObservedStockDecreaseSortLabel(value: string): string {
  const labels: Record<string, string> = {
    '-decrease': 'Больше всего снизился',
    decrease: 'Меньше всего снизился',
    '-currentQuantity': 'Остаток сейчас: больше',
    currentQuantity: 'Остаток сейчас: меньше',
    '-previousQuantity': 'Остаток раньше: больше',
    previousQuantity: 'Остаток раньше: меньше',
    '-observedAtUtc': 'Последнее наблюдение: новое',
    observedAtUtc: 'Последнее наблюдение: старое'
  };

  return labels[value] ?? value;
}

export function getObservedStockDecreaseWarningText(code: string): string | null {
  const labels: Record<string, string> = {
    observed_stock_decrease_is_not_confirmed_order:
      'Это оценка по изменению остатка, а не подтверждённые заказы.',
    quantity_exactness_not_proven: 'Остатки WB показываются как наблюдаемые значения.',
    stock_caps_may_hide_orders:
      'Если WB ограничивает отображаемый остаток, часть заказов может быть не видна.',
    not_enough_logistics_runs: 'Нужно минимум два успешных сбора логистики.'
  };

  return labels[code] ?? null;
}

export function getObservedMarketEventSortLabel(value: string): string {
  const labels: Record<string, string> = {
    '': 'Приоритет событий',
    eventType: 'Событие: А-Я',
    '-eventType': 'Событие: Я-А',
    quantityChange: 'Изменение: меньше',
    '-quantityChange': 'Изменение: больше',
    '-currentQuantity': 'Остаток сейчас: больше',
    currentQuantity: 'Остаток сейчас: меньше',
    '-previousQuantity': 'Остаток раньше: больше',
    previousQuantity: 'Остаток раньше: меньше',
    '-observedAtUtc': 'Последнее наблюдение: новое',
    observedAtUtc: 'Последнее наблюдение: старое'
  };

  return labels[value] ?? value;
}

export function getObservedMarketEventLabel(value: string): string {
  const labels: Record<string, string> = {
    stock_decreased: 'Остаток снизился',
    stock_increased: 'Остаток вырос',
    new_product_observed: 'Новый товар',
    product_missing_in_current: 'Не найден в новом наблюдении'
  };

  return labels[value] ?? 'Событие';
}

export function getObservedMarketEventWarningText(code: string): string | null {
  const labels: Record<string, string> = {
    observed_events_are_not_confirmed_orders: 'Это наблюдаемые изменения, а не подтверждённые заказы.',
    quantity_exactness_not_proven: 'Остатки WB показываются как наблюдаемые значения.',
    stock_caps_may_hide_events:
      'Если WB ограничивает отображаемый остаток, часть событий может быть не видна.',
    logistics_run_coverage_may_differ: 'Состав товаров в двух наблюдениях может отличаться.',
    not_enough_logistics_runs: 'Нужно минимум два успешных сбора логистики.'
  };

  return labels[code] ?? null;
}

export function formatObservedDateTime(value: string | null | undefined): string {
  if (!value) {
    return 'Нет данных';
  }

  const date = new Date(value);

  if (!Number.isFinite(date.getTime())) {
    return 'Нет данных';
  }

  return new Intl.DateTimeFormat('ru-RU', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date);
}

export function formatObservedNumber(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return 'Нет данных';
  }

  return new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value);
}

export function formatObservedMoney(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return 'Нет данных';
  }

  return `${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value)} ₽`;
}

export function formatObservedDecrease(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return 'Нет данных';
  }

  return `−${new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value)}`;
}

export function formatObservedQuantityChange(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return 'Нет данных';
  }

  const formatted = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(Math.abs(value));

  if (value < 0) {
    return `−${formatted}`;
  }

  if (value > 0) {
    return `+${formatted}`;
  }

  return formatted;
}

export function observedFieldValue(value: string | number | null | undefined): string {
  return value === null || value === undefined || value === '' ? 'Нет данных' : String(value);
}
