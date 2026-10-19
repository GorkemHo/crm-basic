/** Permission names declared by the API modules (CrmPermissions, IdentityPermissions). */
export const Permissions = {
  companiesRead: 'crm.companies.read',
  companiesWrite: 'crm.companies.write',
  companiesDelete: 'crm.companies.delete',
  customersRead: 'crm.customers.read',
  customersWrite: 'crm.customers.write',
  customersDelete: 'crm.customers.delete',
  usersRead: 'identity.users.read',
  usersManage: 'identity.users.manage',
  rolesRead: 'identity.roles.read',
  rolesManage: 'identity.roles.manage',
} as const;

export type Permission = (typeof Permissions)[keyof typeof Permissions];
