import { http } from '../../../shared/api/http';

export interface PricingRuleSet {
  id: string;
  code: string;
  name: string;
  status: string;
}

export interface PricingVersion {
  id: string;
  ruleSetId: string;
  versionNumber: number;
  status: string;
  definition: PricingDefinition;
  publishedAt: string | null;
}

export interface PricingDefinition {
  rounding: string;
  rules: PricingRule[];
}

export interface PricingRule {
  priority: number;
  name: string;
  enabled: boolean;
  billingWeightStrategy: string;
  volumeToWeightFactor?: number | null;
  conditions: { field: string; operator: string; value: string; value2?: string | null }[];
  freight: {
    name: string;
    type: string;
    amount?: number | null;
    unitPrice?: number | null;
    minCharge?: number | null;
    tiers?: { upTo?: number | null; unitPrice: number }[] | null;
  };
  surcharges: { name: string; basis: string; amount: number }[];
  discount?: { name: string; mode: string; rate?: number | null; amount?: number | null } | null;
}

export interface TrialResult {
  ruleSetId: string;
  ruleSetCode: string;
  versionNumber: number;
  quote: {
    matchedRuleName: string;
    billingQuantity: number | null;
    billingBasis: string | null;
    items: { kind: string; name: string; amount: number; detail: string }[];
    discount: { name: string; amount: number; detail: string } | null;
    subtotal: number;
    total: number;
  };
}

export interface TrialInput {
  weightKg: number;
  volumeM3: number;
  pieceCount: number;
  customerId?: string | null;
  routeId?: string | null;
  originBranchId?: string | null;
  destBranchId?: string | null;
}

export async function listRuleSets(): Promise<PricingRuleSet[]> {
  const response = await http.get<PricingRuleSet[]>('/pricing/rule-sets');
  return response.data;
}

export async function createRuleSet(code: string, name: string): Promise<PricingRuleSet> {
  const response = await http.post<PricingRuleSet>('/pricing/rule-sets', { code, name });
  return response.data;
}

export async function disableRuleSet(id: string): Promise<void> {
  await http.post(`/pricing/rule-sets/${id}/disable`);
}

export async function enableRuleSet(id: string): Promise<void> {
  await http.post(`/pricing/rule-sets/${id}/enable`);
}

export async function listVersions(ruleSetId: string): Promise<PricingVersion[]> {
  const response = await http.get<PricingVersion[]>(`/pricing/rule-sets/${ruleSetId}/versions`);
  return response.data;
}

export async function createDraftVersion(
  ruleSetId: string,
  definition: PricingDefinition,
): Promise<PricingVersion> {
  const response = await http.post<PricingVersion>(`/pricing/rule-sets/${ruleSetId}/versions`, {
    definition,
  });
  return response.data;
}

export async function updateDraftVersion(
  versionId: string,
  definition: PricingDefinition,
): Promise<void> {
  await http.put(`/pricing/rule-sets/versions/${versionId}`, { definition });
}

export async function validateVersion(versionId: string): Promise<string[]> {
  const response = await http.get<{ errors: string[] }>(
    `/pricing/rule-sets/versions/${versionId}/validate`,
  );
  return response.data.errors;
}

export async function publishVersion(versionId: string): Promise<void> {
  await http.post(`/pricing/rule-sets/versions/${versionId}/publish`);
}

export async function trial(ruleSetId: string, input: TrialInput): Promise<TrialResult> {
  const response = await http.post<TrialResult>('/pricing/trial', { ruleSetId, input });
  return response.data;
}
