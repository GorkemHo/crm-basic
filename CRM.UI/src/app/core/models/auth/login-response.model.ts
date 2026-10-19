import { UserDto } from "../user/user.model";

// export interface LoginResponse {
//     accessToken:string;
//     refreshToken:string;
//     expiresAt:string;
//     user:IUser;
// }

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  email: string;
  expiresAt: string;
  refreshTokenExpiresAt: string;
  roles: string[];
  user: UserDto;
}