export interface Company {
  id: number;
  companyName: string;
  taxNumber: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCompany {
  companyName: string;
  taxNumber?: string | null;
}

export type UpdateCompany = Partial<Pick<Company, 'companyName' | 'taxNumber' | 'isActive'>>;
