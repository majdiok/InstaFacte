import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';

export interface ProductCategory {
  id: string;
  code: string;
  name: string;
  displayOrder: number;
  isActive: boolean;
}

export interface ProductCategorySelect {
  id: string;
  name: string;
}

export interface CreateProductCategoryRequest {
  code: string;
  name: string;
  displayOrder: number;
}

export interface UpdateProductCategoryRequest {
  name: string;
  displayOrder: number;
  isActive: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ProductCategoryService {
  private readonly API_URL = `${environment.apiUrl}/product-categories`;
  private http = inject(HttpClient);

  getProductCategories(activeOnly: boolean = true): Observable<ApiResponse<ProductCategorySelect[]>> {
    return this.http
      .get<ApiResponse<ProductCategorySelect[]>>(this.API_URL, {
        params: { activeOnly: activeOnly.toString() }
      })
      .pipe(
        map(response => {
          if (!response.success || !response.data) {
            return response as ApiResponse<ProductCategorySelect[]>;
          }
          return {
            ...response,
            data: response.data
          } as ApiResponse<ProductCategorySelect[]>;
        })
      );
  }

  getCategoryOptionsForDropdown(): Observable<{ label: string; value: string }[]> {
    return this.getProductCategories(true).pipe(
      map(response => {
        if (!response.success || !response.data) {
          return [];
        }
        return response.data.map(c => ({
          label: c.name,
          value: c.id
        }));
      })
    );
  }

  getCategoryList(): Observable<ApiResponse<ProductCategory[]>> {
    return this.http.get<ApiResponse<ProductCategory[]>>(`${this.API_URL}/list`);
  }

  getCategoryById(id: string): Observable<ApiResponse<ProductCategory>> {
    return this.http.get<ApiResponse<ProductCategory>>(`${this.API_URL}/${id}`);
  }

  createCategory(body: CreateProductCategoryRequest): Observable<ApiResponse<string>> {
    return this.http.post<ApiResponse<string>>(this.API_URL, body);
  }

  updateCategory(id: string, body: UpdateProductCategoryRequest): Observable<ApiResponse<ProductCategory>> {
    return this.http.put<ApiResponse<ProductCategory>>(`${this.API_URL}/${id}`, body);
  }
}
