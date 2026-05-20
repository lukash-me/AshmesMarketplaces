export interface OrderListItem {
  id: string;
  idProduct: string;
  price: number;
  discount: number;
  amount: number;
  locationSource: string | null;
  locationDestination: string | null;
  status: number;
  dateDelivered: string | null;
  dateOpened: string;
  dateClosed: string | null;
  dateUpdate: string;
}

export type OrderDetail = OrderListItem;

export interface OrderListParams {
  page: number;
  pageSize: number;
  search?: string;
  sort?: string;
  idProduct?: string;
  status?: number;
  dateOpenedFrom?: string;
  dateOpenedTo?: string;
}

export type OrderQueryState = {
  page: number;
  pageSize: number;
  search: string;
  sort: string;
  idProduct: string;
  status: string;
  dateOpenedFrom: string;
  dateOpenedTo: string;
};
