import { Tenant, DocumentSummary, DocumentChunk, ChatMessage, RagInspection } from '../types';

const API_BASE = 'http://localhost:5252/api';

class ApiClient {
  private tenantId: string = '11111111-1111-1111-1111-111111111111'; // Default Acme
  private roles: string[] = ['Engineering', 'General'];

  public setContext(tenantId: string, roles: string[]) {
    this.tenantId = tenantId;
    this.roles = roles;
  }

  private getHeaders(): HeadersInit {
    return {
      'Content-Type': 'application/json',
      'X-Tenant-Id': this.tenantId,
      'X-User-Roles': this.roles.join(','),
    };
  }

  async getTenants(): Promise<Tenant[]> {
    const res = await fetch(`${API_BASE}/tenants`, { headers: this.getHeaders() });
    if (!res.ok) throw new Error('Failed to fetch tenants');
    return res.json();
  }

  async getDocuments(): Promise<DocumentSummary[]> {
    const res = await fetch(`${API_BASE}/documents`, { headers: this.getHeaders() });
    if (!res.ok) throw new Error('Failed to fetch documents');
    return res.json();
  }

  async uploadDocument(file: File, aclRoles: string = 'Engineering,General'): Promise<any> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('aclRoles', aclRoles);

    const res = await fetch(`${API_BASE}/documents/upload`, {
      method: 'POST',
      headers: {
        'X-Tenant-Id': this.tenantId,
        'X-User-Roles': this.roles.join(','),
      },
      body: formData,
    });
    if (!res.ok) {
      const err = await res.json();
      throw new Error(err.error || 'Upload failed');
    }
    return res.json();
  }

  async getDocumentChunks(documentId: string): Promise<DocumentChunk[]> {
    const res = await fetch(`${API_BASE}/documents/${documentId}/chunks`, { headers: this.getHeaders() });
    if (!res.ok) throw new Error('Failed to fetch chunks');
    return res.json();
  }

  async askQuestion(
    question: string,
    conversationId?: string | null,
    topK: number = 25,
    topN: number = 5
  ): Promise<{
    messageId: string;
    conversationId: string;
    answer: string;
    citations: any[];
    isGrounded: boolean;
    confidenceScore: number;
    refusalReason: string | null;
    inspection: RagInspection;
  }> {
    const res = await fetch(`${API_BASE}/chat/ask`, {
      method: 'POST',
      headers: this.getHeaders(),
      body: JSON.stringify({
        question,
        conversationId,
        topKCandidates: topK,
        topNReranked: topN,
      }),
    });
    if (!res.ok) {
      const err = await res.json();
      throw new Error(err.error || 'Query failed');
    }
    return res.json();
  }

  async getConversations(): Promise<any[]> {
    const res = await fetch(`${API_BASE}/chat/conversations`, { headers: this.getHeaders() });
    if (!res.ok) throw new Error('Failed to fetch conversations');
    return res.json();
  }

  async getConversationMessages(id: string): Promise<ChatMessage[]> {
    const res = await fetch(`${API_BASE}/chat/conversations/${id}/messages`, { headers: this.getHeaders() });
    if (!res.ok) throw new Error('Failed to fetch messages');
    return res.json();
  }
}

export const api = new ApiClient();
