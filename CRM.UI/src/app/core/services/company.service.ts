import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Company, CreateCompany, UpdateCompany } from '../models/company/company.model';

@Injectable({
  providedIn: 'root',
})
export class CompanyService {
  private http = inject(HttpClient);

  private readonly apiUrl = `${environment.apiUrl}/companies`;

  getAll(): Observable<Company[]> {
    // Temporary until the PrimeNG rewrite: the API is paged, the old grids expect a plain array.
    return this.http.get<{ items: Company[] }>(this.apiUrl, { params: { pageSize: 100 } }).pipe(map((r) => r.items));
  }

  getById(id: number): Observable<Company> {
    return this.http.get<Company>(`${this.apiUrl}/${id}`);
  }

  create(company: CreateCompany): Observable<Company> {
    return this.http.post<Company>(this.apiUrl, company);
  }

  patch(id: number, company: UpdateCompany): Observable<Company> {
    return this.http.patch<Company>(`${this.apiUrl}/${id}`, company);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
