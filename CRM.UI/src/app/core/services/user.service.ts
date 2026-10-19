import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateUser, UpdateUser, UserDto } from '../models/user/user.model';

@Injectable({
  providedIn: 'root',
})
export class UserService {
  private http = inject(HttpClient);

  private readonly apiUrl = `${environment.apiUrl}/users`;

  getAll(): Observable<UserDto[]> {
    return this.http.get<UserDto[]>(this.apiUrl);
  }

  getById(id: number): Observable<UserDto> {
    return this.http.get<UserDto>(`${this.apiUrl}/${id}`);
  }

  create(user: CreateUser): Observable<UserDto> {
    return this.http.post<UserDto>(this.apiUrl, user);
  }

  patch(id: number, user: UpdateUser): Observable<UserDto> {
    return this.http.patch<UserDto>(`${this.apiUrl}/${id}`, user);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
