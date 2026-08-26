import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@environments/environment';
import { ApiResponse } from './auth.service';

export type ExchangeThreadStatus = number | string;
export type ExchangeMessageVisibility = number | string;
export type ExchangeRequestCategory = number | string;
export type ExchangeRequestPriority = number | string;
export type ExchangeRequestStatus = number | string;
export type ExchangeTaskStatus = number | string;

export interface ExchangeThreadListItem {
  id: string;
  firmClientAssignmentId: string;
  firmTenantId: string;
  companyTenantId: string;
  counterpartName: string;
  status: ExchangeThreadStatus;
  lastActivityAt?: string;
  unreadCount: number;
  subject?: string;
}

export interface ExchangeParticipant {
  userId: string;
  displayName: string;
  role: string;
  side: string;
  isOnline: boolean;
}

export interface ExchangeThreadDetail {
  id: string;
  firmClientAssignmentId: string;
  firmTenantId: string;
  companyTenantId: string;
  companyName: string;
  firmName: string;
  status: ExchangeThreadStatus;
  subject?: string;
  createdAt: string;
  lastActivityAt?: string;
  closedAt?: string;
  closedByUserId?: string;
  participants: ExchangeParticipant[];
}

export interface ExchangeDocument {
  id: string;
  threadId: string;
  messageId?: string;
  requestId?: string;
  taskId?: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedByUserId: string;
  uploadedAt: string;
}

export interface ExchangeMessageRead {
  userId: string;
  displayName?: string;
  readAt: string;
}

export interface ExchangeMessage {
  id: string;
  threadId: string;
  authorUserId: string;
  authorTenantId: string;
  authorDisplayName: string;
  visibility: ExchangeMessageVisibility;
  body: string;
  sentAt: string;
  readReceipts: ExchangeMessageRead[];
  attachments: ExchangeDocument[];
}

export interface ExchangeRequest {
  id: string;
  threadId: string;
  number: number;
  title: string;
  description: string;
  category: ExchangeRequestCategory;
  priority: ExchangeRequestPriority;
  status: ExchangeRequestStatus;
  createdByUserId: string;
  createdByTenantId: string;
  assigneeUserId?: string;
  createdAt: string;
  resolvedAt?: string;
  closedAt?: string;
}

export interface ExchangeRequestComment {
  id: string;
  threadId: string;
  requestId: string;
  authorUserId: string;
  authorTenantId: string;
  authorDisplayName: string;
  body: string;
  createdAt: string;
}

export interface ExchangeTask {
  id: string;
  threadId: string;
  title: string;
  description?: string;
  dueDate?: string;
  assigneeUserId?: string;
  assigneeTenantId?: string;
  status: ExchangeTaskStatus;
  createdByUserId: string;
  createdAt: string;
  completedAt?: string;
}

export interface ExchangeAuditEvent {
  id: string;
  threadId: string;
  occurredAt: string;
  actorUserId: string;
  actorDisplayName: string;
  eventType: number;
  payloadJson?: string;
}

export interface ExchangeUnreadSummary {
  totalUnreadMessages: number;
  openRequests: number;
  threads: { threadId: string; unreadCount: number }[];
}

export interface PagedExchangeMessages {
  items: ExchangeMessage[];
  hasMore: boolean;
  oldestSentAt?: string;
}

export interface ExchangeBootstrap {
  threads?: ExchangeThreadListItem[] | null;
  companyAssignment?: FirmClientAssignmentLite | null;
  firmClients?: FirmClientDossierLite[] | null;
  activeThread?: ExchangeThreadDetail | null;
  messages?: PagedExchangeMessages | null;
  requests?: ExchangeRequest[] | null;
  tasks?: ExchangeTask[] | null;
  documents?: ExchangeDocument[] | null;
  history?: ExchangeAuditEvent[] | null;
  openRequestsCount: number;
  unreadCount: number;
  emptyHint?: string | null;
}

/** Minimal assignment shape returned by bootstrap (matches FirmClientAssignmentDto JSON). */
export interface FirmClientAssignmentLite {
  id: string;
  companyTenantId: string;
  companyName: string;
  firmTenantId: string;
  firmDisplayName: string;
  status: number | string;
  statusDisplay: string;
  requestedAt: string;
}

export interface FirmClientDossierLite {
  assignmentId: string;
  companyTenantId: string;
  companyName: string;
  activeSince: string;
}

@Injectable({ providedIn: 'root' })
export class ExchangeService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/exchanges`;

  listThreads(): Observable<ApiResponse<ExchangeThreadListItem[]>> {
    return this.http.get<ApiResponse<ExchangeThreadListItem[]>>(this.baseUrl);
  }

  getBootstrap(threadId?: string | null, tab?: string | null): Observable<ApiResponse<ExchangeBootstrap>> {
    let params = new HttpParams();
    if (threadId) params = params.set('threadId', threadId);
    if (tab && tab !== 'conversation') params = params.set('tab', tab);
    return this.http.get<ApiResponse<ExchangeBootstrap>>(`${this.baseUrl}/bootstrap`, { params });
  }

  getUnreadSummary(context?: HttpContext): Observable<ApiResponse<ExchangeUnreadSummary>> {
    return this.http.get<ApiResponse<ExchangeUnreadSummary>>(`${this.baseUrl}/unread-summary`, {
      context
    });
  }

  getThread(threadId: string): Observable<ApiResponse<ExchangeThreadDetail>> {
    return this.http.get<ApiResponse<ExchangeThreadDetail>>(`${this.baseUrl}/${threadId}`);
  }

  ensureThread(firmClientAssignmentId?: string): Observable<ApiResponse<ExchangeThreadDetail>> {
    return this.http.post<ApiResponse<ExchangeThreadDetail>>(`${this.baseUrl}/ensure`, {
      firmClientAssignmentId: firmClientAssignmentId ?? null
    });
  }

  closeThread(threadId: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/${threadId}/close`, {});
  }

  reopenThread(threadId: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/${threadId}/reopen`, {});
  }

  getMessages(
    threadId: string,
    options?: { after?: string; before?: string; limit?: number }
  ): Observable<ApiResponse<PagedExchangeMessages>> {
    let params = new HttpParams();
    if (options?.after) params = params.set('after', options.after);
    if (options?.before) params = params.set('before', options.before);
    if (options?.limit != null) params = params.set('limit', String(options.limit));
    return this.http.get<ApiResponse<PagedExchangeMessages>>(`${this.baseUrl}/${threadId}/messages`, {
      params
    });
  }

  sendMessage(
    threadId: string,
    body: string,
    visibility: ExchangeMessageVisibility = 'ClientVisible'
  ): Observable<ApiResponse<ExchangeMessage>> {
    return this.http.post<ApiResponse<ExchangeMessage>>(`${this.baseUrl}/${threadId}/messages`, {
      body,
      visibility
    });
  }

  markRead(threadId: string, messageId: string): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(
      `${this.baseUrl}/${threadId}/messages/${messageId}/read`,
      {}
    );
  }

  markReadBatch(threadId: string, messageIds: string[]): Observable<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`${this.baseUrl}/${threadId}/messages/read-batch`, {
      messageIds
    });
  }

  listRequests(threadId: string): Observable<ApiResponse<ExchangeRequest[]>> {
    return this.http.get<ApiResponse<ExchangeRequest[]>>(`${this.baseUrl}/${threadId}/requests`);
  }

  createRequest(
    threadId: string,
    payload: {
      title: string;
      description?: string;
      category: ExchangeRequestCategory;
      priority?: ExchangeRequestPriority;
    }
  ): Observable<ApiResponse<ExchangeRequest>> {
    return this.http.post<ApiResponse<ExchangeRequest>>(`${this.baseUrl}/${threadId}/requests`, payload);
  }

  changeRequestStatus(
    threadId: string,
    requestId: string,
    status: ExchangeRequestStatus
  ): Observable<ApiResponse<ExchangeRequest>> {
    return this.http.post<ApiResponse<ExchangeRequest>>(
      `${this.baseUrl}/${threadId}/requests/${requestId}/status`,
      { status }
    );
  }

  assignRequest(
    threadId: string,
    requestId: string,
    assigneeUserId: string
  ): Observable<ApiResponse<ExchangeRequest>> {
    return this.http.post<ApiResponse<ExchangeRequest>>(
      `${this.baseUrl}/${threadId}/requests/${requestId}/assign`,
      { assigneeUserId }
    );
  }

  listRequestComments(
    threadId: string,
    requestId: string
  ): Observable<ApiResponse<ExchangeRequestComment[]>> {
    return this.http.get<ApiResponse<ExchangeRequestComment[]>>(
      `${this.baseUrl}/${threadId}/requests/${requestId}/comments`
    );
  }

  addRequestComment(
    threadId: string,
    requestId: string,
    body: string
  ): Observable<ApiResponse<ExchangeRequestComment>> {
    return this.http.post<ApiResponse<ExchangeRequestComment>>(
      `${this.baseUrl}/${threadId}/requests/${requestId}/comments`,
      { body }
    );
  }

  listTasks(threadId: string): Observable<ApiResponse<ExchangeTask[]>> {
    return this.http.get<ApiResponse<ExchangeTask[]>>(`${this.baseUrl}/${threadId}/tasks`);
  }

  createTask(
    threadId: string,
    payload: { title: string; description?: string; dueDate?: string; assigneeUserId?: string }
  ): Observable<ApiResponse<ExchangeTask>> {
    return this.http.post<ApiResponse<ExchangeTask>>(`${this.baseUrl}/${threadId}/tasks`, payload);
  }

  changeTaskStatus(
    threadId: string,
    taskId: string,
    status: ExchangeTaskStatus
  ): Observable<ApiResponse<ExchangeTask>> {
    return this.http.post<ApiResponse<ExchangeTask>>(
      `${this.baseUrl}/${threadId}/tasks/${taskId}/status`,
      { status }
    );
  }

  listDocuments(threadId: string): Observable<ApiResponse<ExchangeDocument[]>> {
    return this.http.get<ApiResponse<ExchangeDocument[]>>(`${this.baseUrl}/${threadId}/documents`);
  }

  uploadDocument(
    threadId: string,
    file: File,
    messageId?: string,
    requestId?: string
  ): Observable<ApiResponse<ExchangeDocument>> {
    const form = new FormData();
    form.append('file', file, file.name);
    let params = new HttpParams();
    if (messageId) params = params.set('messageId', messageId);
    if (requestId) params = params.set('requestId', requestId);
    return this.http.post<ApiResponse<ExchangeDocument>>(
      `${this.baseUrl}/${threadId}/documents`,
      form,
      { params }
    );
  }

  downloadDocument(threadId: string, documentId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/${threadId}/documents/${documentId}/download`, {
      responseType: 'blob'
    });
  }

  deleteDocument(threadId: string, documentId: string): Observable<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`${this.baseUrl}/${threadId}/documents/${documentId}`);
  }

  getHistory(threadId: string, page = 1, pageSize = 50): Observable<ApiResponse<ExchangeAuditEvent[]>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<ApiResponse<ExchangeAuditEvent[]>>(`${this.baseUrl}/${threadId}/history`, {
      params
    });
  }
}
