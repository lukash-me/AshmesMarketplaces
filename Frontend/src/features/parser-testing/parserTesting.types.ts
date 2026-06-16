import type {
  ParserProductDetail,
  ParserProductDeliveryDestinationSignal,
  ParserProductDeliveryLocationEstimate,
  ParserProductFilterOptions,
  ParserProductLogisticsSummaryAggregate,
  ParserProductLogisticsSummaryParams,
  ParserProductListItem,
  ParserProductListParams
} from '@/features/parser-products/parserProducts.types';

export type ParserTestingProduct = ParserProductListItem;
export type ParserTestingProductDetail = ParserProductDetail;
export type ParserTestingDeliveryDestination = ParserProductDeliveryDestinationSignal;
export type ParserTestingDeliveryLocationEstimate = ParserProductDeliveryLocationEstimate;
export type ParserTestingFilterOptions = ParserProductFilterOptions;
export type ParserTestingLogisticsSummary = ParserProductLogisticsSummaryAggregate;
export type ParserTestingLogisticsSummaryParams = ParserProductLogisticsSummaryParams;

export interface ParserTestingListParams extends ParserProductListParams {
  requireDeliveryProfile: true;
}
