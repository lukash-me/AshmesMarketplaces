export interface LogisticListItem {
  id: string;
  idProduct: string;
  idWarehouse: string | null;
  stockAmount: number | null;
  stockAmountStatistic: number;
  stockInTransit: number | null;
  costStorage: number | null;
  costLogistic: number | null;
  type: number;
  date: string;
}

export type LogisticDetail = LogisticListItem;

export interface WarehouseListItem {
  id: string;
  idMp: string;
  name: string;
  code: string;
  region: string;
  city: string | null;
  isActive: boolean;
  type: number;
  dateCreate: string;
  dateUpdate: string;
}

export interface WarehouseDetail extends WarehouseListItem {
  address: string | null;
  latitude: string | null;
  longitude: string | null;
}

export interface LogisticListParams {
  page: number;
  pageSize: number;
  search?: string;
  sort?: string;
  idProduct?: string;
  idWarehouse?: string;
  type?: number;
  dateFrom?: string;
  dateTo?: string;
}

export type LogisticQueryState = {
  page: number;
  pageSize: number;
  search: string;
  sort: string;
  idProduct: string;
  idWarehouse: string;
  type: string;
  dateFrom: string;
  dateTo: string;
};
