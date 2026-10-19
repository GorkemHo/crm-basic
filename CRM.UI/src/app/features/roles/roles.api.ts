import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { PermissionDto, RoleDto } from '../../core/api/models';

@Injectable({ providedIn: 'root' })
export class RolesApi {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/roles';

  list(): Observable<RoleDto[]> {
    return this.http.get<RoleDto[]>(this.url);
  }

  get(id: number): Observable<RoleDto> {
    return this.http.get<RoleDto>(`${this.url}/${id}`);
  }

  create(name: string): Observable<RoleDto> {
    return this.http.post<RoleDto>(this.url, { name });
  }

  update(id: number, changes: { name?: string; isActive?: boolean }): Observable<RoleDto> {
    return this.http.patch<RoleDto>(`${this.url}/${id}`, changes);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  /** Replaces the role's permissions (not allowed for Admin). */
  setPermissions(id: number, permissions: string[]): Observable<RoleDto> {
    return this.http.put<RoleDto>(`${this.url}/${id}/permissions`, { permissions });
  }

  /** Every permission declared by the API modules. */
  permissions(): Observable<PermissionDto[]> {
    return this.http.get<PermissionDto[]>('/api/permissions');
  }
}
