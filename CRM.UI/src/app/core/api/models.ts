// DTOs returned by the API (src/Modules/*/Features). Dates are ISO strings in UTC.

export interface PagedResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface UserDto {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  isActive: boolean;
  preferredLanguage: string;
  roles: string[];
  createdAt: string;
  updatedAt: string;
}

export interface CurrentUserDto extends UserDto {
  permissions: string[];
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: CurrentUserDto;
}

export interface RoleDto {
  id: number;
  name: string;
  isActive: boolean;
  isSystem: boolean;
  userCount: number;
  permissions: string[];
  createdAt: string;
}

export interface PermissionDto {
  name: string;
  module: string;
  group: string;
  description: string;
}

export interface CompanyDto {
  id: number;
  companyName: string;
  taxNumber: string | null;
  isActive: boolean;
  customerCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface CustomerDto {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  isActive: boolean;
  companyId: number;
  companyName: string;
  createdAt: string;
  updatedAt: string;
}

export interface AuditLogDto {
  id: number;
  action: 'Updated' | 'Deleted';
  /** `{ field: { old?: unknown, new?: unknown } }` */
  changes: Record<string, { old?: unknown; new?: unknown }>;
  userId: number | null;
  occurredAt: string;
}
