export interface CampaignListItem {
  id: string;
  idProduct: string;
  idSetCampaign: string | null;
  name: string;
  budget: number | null;
  region: string | null;
  status: number;
  type: number;
  dateStart: string | null;
  dateEnd: string | null;
  dateCreate: string;
  dateUpdate: string;
}

export interface CampaignDetail extends CampaignListItem {
  description: string | null;
  timeToImpression: unknown | null;
}

export interface CampaignMetricListItem {
  id: string;
  idCampaign: string;
  impressionAmount: number | null;
  clicksAmount: number | null;
  costDay: number | null;
  date: string;
}

export type CampaignMetricDetail = CampaignMetricListItem;

export interface CampaignListParams {
  page: number;
  pageSize: number;
  search?: string;
  sort?: string;
  idProduct?: string;
  idSetCampaign?: string;
  status?: number;
  type?: number;
  dateStartFrom?: string;
  dateStartTo?: string;
  dateEndFrom?: string;
  dateEndTo?: string;
}

export interface CampaignMetricListParams {
  page: number;
  pageSize: number;
  sort?: string;
  search?: string;
  idCampaign?: string;
  dateFrom?: string;
  dateTo?: string;
}

export type CampaignQueryState = {
  page: number;
  pageSize: number;
  search: string;
  sort: string;
  idProduct: string;
  idSetCampaign: string;
  status: string;
  type: string;
  dateStartFrom: string;
  dateStartTo: string;
  dateEndFrom: string;
  dateEndTo: string;
};
