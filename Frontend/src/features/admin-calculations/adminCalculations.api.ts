import { http } from '@/shared/api/http';

import type { AdminCalculation, AdminCalculationManualRun } from './adminCalculations.types';

export async function getAdminCalculations(): Promise<AdminCalculation[]> {
  const response = await http.get<AdminCalculation[]>('/admin/calculations');
  return response.data;
}

export async function runAdminCalculation(scheduleKey: string): Promise<AdminCalculationManualRun> {
  const response = await http.post<AdminCalculationManualRun>(`/admin/calculations/${scheduleKey}/run`);
  return response.data;
}
