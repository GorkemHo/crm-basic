/** Permission names declared by the API modules (CrmPermissions, SalesPermissions, ActivitiesPermissions, TasksPermissions, IdentityPermissions). */
export const Permissions = {
  companiesRead: 'crm.companies.read',
  companiesWrite: 'crm.companies.write',
  companiesDelete: 'crm.companies.delete',
  customersRead: 'crm.customers.read',
  customersWrite: 'crm.customers.write',
  customersDelete: 'crm.customers.delete',
  dealsRead: 'sales.deals.read',
  dealsWrite: 'sales.deals.write',
  dealsDelete: 'sales.deals.delete',
  stagesManage: 'sales.stages.manage',
  activitiesRead: 'activities.activities.read',
  activitiesWrite: 'activities.activities.write',
  activitiesManage: 'activities.activities.manage',
  tasksRead: 'tasks.tasks.read',
  tasksWrite: 'tasks.tasks.write',
  tasksManage: 'tasks.tasks.manage',
  usersRead: 'identity.users.read',
  usersManage: 'identity.users.manage',
  rolesRead: 'identity.roles.read',
  rolesManage: 'identity.roles.manage',
} as const;

export type Permission = (typeof Permissions)[keyof typeof Permissions];
