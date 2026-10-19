export interface UserDto {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  roleName: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateUser {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

export type UpdateUser = Partial<Pick<UserDto, 'firstName' | 'lastName' | 'email' | 'isActive'>>;
