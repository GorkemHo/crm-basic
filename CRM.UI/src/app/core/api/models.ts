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

export type Currency = 'TRY' | 'USD' | 'EUR';
export const CURRENCIES: Currency[] = ['TRY', 'USD', 'EUR'];

export type DealStatus = 'open' | 'won' | 'lost';

export interface StageDto {
  id: number;
  name: string;
  position: number;
  /** Win probability, 0–100. */
  probability: number;
  openDealCount: number;
}

export interface DealDto {
  id: number;
  title: string;
  companyId: number;
  companyName: string | null;
  customerId: number | null;
  customerName: string | null;
  amount: number;
  currency: Currency;
  stageId: number;
  stageName: string;
  probability: number;
  status: DealStatus;
  /** yyyy-MM-dd */
  expectedCloseDate: string | null;
  closedAt: string | null;
  lostReason: string | null;
  createdAt: string;
  updatedAt: string;
}

/** Deals in one currency; amounts in different currencies are never added together. */
export interface CurrencyTotal {
  currency: Currency;
  count: number;
  amount: number;
  weightedAmount: number;
}

export interface BoardColumn {
  stageId: number;
  name: string;
  probability: number;
  deals: DealDto[];
  totals: CurrencyTotal[];
}

export interface PipelineSummary {
  open: CurrencyTotal[];
  wonThisMonth: CurrencyTotal[];
  lostThisMonth: number;
}

export type ActivityType = 'note' | 'call' | 'meeting' | 'email';
export const ACTIVITY_TYPES: ActivityType[] = ['note', 'call', 'meeting', 'email'];

export interface ActivityDto {
  id: number;
  type: ActivityType;
  subject: string;
  notes: string | null;
  occurredAt: string;
  durationMinutes: number | null;
  companyId: number;
  companyName: string | null;
  customerId: number | null;
  customerName: string | null;
  dealId: number | null;
  dealTitle: string | null;
  /** Author; null for records created by the system (demo data). */
  createdBy: number | null;
  createdByName: string | null;
  createdAt: string;
  updatedAt: string;
}

export type TaskPriority = 'low' | 'normal' | 'high';
export const TASK_PRIORITIES: TaskPriority[] = ['high', 'normal', 'low'];

export interface TaskDto {
  id: number;
  title: string;
  description: string | null;
  /** yyyy-MM-dd */
  dueDate: string | null;
  priority: TaskPriority;
  assigneeId: number;
  assigneeName: string | null;
  isDone: boolean;
  completedAt: string | null;
  companyId: number | null;
  companyName: string | null;
  customerId: number | null;
  customerName: string | null;
  dealId: number | null;
  dealTitle: string | null;
  createdBy: number | null;
  createdByName: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AssigneeDto {
  id: number;
  fullName: string;
}

export interface MyTaskSummary {
  open: number;
  overdue: number;
  dueToday: number;
}

export type NotificationType = 'task.assigned' | 'task.completed' | 'deal.won' | 'deal.lost';

export interface NotificationDto {
  id: number;
  type: NotificationType;
  /** Who caused it; null for the system. */
  actorId: number | null;
  actorName: string | null;
  /** Title of the task / deal it is about. */
  subject: string;
  /** App route to open. */
  link: string | null;
  createdAt: string;
  readAt: string | null;
}

export type AttachmentRecordType = 'company' | 'customer' | 'deal';

export interface AttachmentDto {
  id: number;
  recordType: AttachmentRecordType;
  recordId: number;
  fileName: string;
  contentType: string;
  /** Bytes */
  size: number;
  /** Uploader; null for files added by the system (demo data). */
  createdBy: number | null;
  createdByName: string | null;
  createdAt: string;
}

export interface AuditLogDto {
  id: number;
  action: 'Updated' | 'Deleted';
  /** `{ field: { old?: unknown, new?: unknown } }` */
  changes: Record<string, { old?: unknown; new?: unknown }>;
  userId: number | null;
  occurredAt: string;
}
