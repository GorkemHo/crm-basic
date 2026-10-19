export interface Customer {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  isActive: boolean;
  companyId: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCustomer {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string | null;
  companyId: number;
}

export type UpdateCustomer = Partial<
  Pick<Customer, 'firstName' | 'lastName' | 'email' | 'phoneNumber' | 'companyId' | 'isActive'>
>;
