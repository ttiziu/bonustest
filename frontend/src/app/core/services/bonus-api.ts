import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { Bonus, BonusFilters, Employee, RegisterBonus } from '../models/bonus';
import { PagedResponse } from '../models/paged-response';

@Injectable({ providedIn: 'root' })
export class BonusApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/Bonus`;

  list(filters: BonusFilters) {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filters)) {
      if (value !== '' && value !== null) params = params.set(key, value);
    }
    return this.http.get<PagedResponse<Bonus>>(this.url, { params });
  }

  employee(departmentCode: string, employeeNumber: number) {
    return this.http.get<Employee>(`${this.url}/employee/${encodeURIComponent(departmentCode)}/${employeeNumber}`);
  }

  register(request: RegisterBonus) {
    return this.http.post<{ bonusId: number }>(this.url, request);
  }

  cancel(id: number) {
    return this.http.put<void>(`${this.url}/${id}/cancel`, {});
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
