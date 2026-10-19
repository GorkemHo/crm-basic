import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { PagedResponse, UserDto } from '../../core/api/models';
import { PageQuery, toHttpParams } from '../../core/api/paging';

export interface CreateUserInput {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  preferredLanguage: string;
  roleIds: number[];
}

export interface UpdateUserInput {
  firstName?: string;
  lastName?: string;
  email?: string;
  isActive?: boolean;
  preferredLanguage?: string;
}

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/users';

  /** Filters: `search`, `isActive`, `roleId`. */
  list(query: PageQuery): Observable<PagedResponse<UserDto>> {
    return this.http.get<PagedResponse<UserDto>>(this.url, { params: toHttpParams(query) });
  }

  get(id: number): Observable<UserDto> {
    return this.http.get<UserDto>(`${this.url}/${id}`);
  }

  create(input: CreateUserInput): Observable<UserDto> {
    return this.http.post<UserDto>(this.url, input);
  }

  update(id: number, changes: UpdateUserInput): Observable<UserDto> {
    return this.http.patch<UserDto>(`${this.url}/${id}`, changes);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  /** Replaces the user's roles. */
  setRoles(id: number, roleIds: number[]): Observable<UserDto> {
    return this.http.put<UserDto>(`${this.url}/${id}/roles`, { roleIds });
  }

  historyUrl(id: number): string {
    return `${this.url}/${id}/history`;
  }
}
