import React, { useState, useEffect } from 'react';
import { Tenant, DocumentSummary, ChatMessage, Citation, RagInspection } from './types';
import { api } from './api/client';
import { Navbar } from './components/Navbar';
import { ChatPane } from './components/ChatPane';
import { DocumentViewerPane } from './components/DocumentViewerPane';
import { RagInspectorModal } from './components/RagInspectorModal';
import { UploadModal } from './components/UploadModal';

export const App: React.FC = () => {
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [currentTenantId, setCurrentTenantId] = useState<string>('11111111-1111-1111-1111-111111111111');
  const [currentRole, setCurrentRole] = useState<string>('Engineering');
  const [documents, setDocuments] = useState<DocumentSummary[]>([]);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [activeCitation, setActiveCitation] = useState<Citation | null>(null);
  const [inspectorOpen, setInspectorOpen] = useState<boolean>(false);
  const [uploadModalOpen, setUploadModalOpen] = useState<boolean>(false);
  const [latestInspection, setLatestInspection] = useState<RagInspection | null>(null);
  const [backendOnline, setBackendOnline] = useState<boolean>(true);

  // Initialize tenants and documents
  useEffect(() => {
    loadTenants();
    loadDocuments();
  }, [currentTenantId, currentRole]);

  const loadTenants = async () => {
    try {
      const data = await api.getTenants();
      setTenants(data);
      if (data.length > 0 && !data.some(t => t.id === currentTenantId)) {
        setCurrentTenantId(data[0].id);
      }
    } catch (err) {
      console.warn('Backend API not yet running, using simulated tenant state:', err);
      setTenants([
        { id: '11111111-1111-1111-1111-111111111111', name: 'Acme Aerospace', description: 'Defense & Propulsion Systems', documentCount: 1 },
        { id: '22222222-2222-2222-2222-222222222222', name: 'Globex Health', description: 'Pharmaceutical Research', documentCount: 1 }
      ]);
    }
  };

  const loadDocuments = async () => {
    try {
      const docs = await api.getDocuments();
      setDocuments(docs);
    } catch (err) {
      console.warn('Backend API not yet running, using simulated document state:', err);
      setDocuments([
        {
          id: '33333333-3333-3333-3333-333333333333',
          filename: 'Acme_Propulsion_System_Specifications_v4.2.pdf',
          fileSizeBytes: 204800,
          pageCount: 3,
          status: 'Indexed',
          chunkCount: 3,
          createdAt: new Date().toISOString()
        }
      ]);
    }
  };

  const handleTenantChange = (newTenantId: string) => {
    setCurrentTenantId(newTenantId);
    api.setContext(newTenantId, [currentRole, 'General']);
    setMessages([]);
    setConversationId(null);
    setActiveCitation(null);
  };

  const handleRoleChange = (newRole: string) => {
    setCurrentRole(newRole);
    api.setContext(currentTenantId, [newRole, 'General']);
  };

  const handleSendMessage = async (query: string, topK: number, topN: number) => {
    const userMsg: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content: query,
      citations: [],
      createdAt: new Date().toISOString()
    };

    setMessages(prev => [...prev, userMsg]);
    setIsLoading(true);

    try {
      const res = await api.askQuestion(query, conversationId, topK, topN);
      setConversationId(res.conversationId);

      const assistantMsg: ChatMessage = {
        id: res.messageId,
        role: 'assistant',
        content: res.answer,
        citations: res.citations,
        isGrounded: res.isGrounded,
        confidenceScore: res.confidenceScore,
        refusalReason: res.refusalReason,
        latencyMs: res.inspection?.latencies?.totalLatencyMs,
        inspection: res.inspection,
        createdAt: new Date().toISOString()
      };

      setMessages(prev => [...prev, assistantMsg]);
      setLatestInspection(res.inspection);

      if (res.citations && res.citations.length > 0) {
        setActiveCitation(res.citations[0]);
      }
    } catch (err: any) {
      const errorMsg: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'assistant',
        content: `Error querying knowledge base: ${err.message}. Please verify the backend API is online.`,
        citations: [],
        isGrounded: false,
        refusalReason: 'API_ERROR',
        createdAt: new Date().toISOString()
      };
      setMessages(prev => [...prev, errorMsg]);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex flex-col h-screen w-screen overflow-hidden bg-surface-darkest font-sans">
      {/* Top Navigation */}
      <Navbar
        tenants={tenants}
        currentTenantId={currentTenantId}
        onTenantChange={handleTenantChange}
        currentRole={currentRole}
        onRoleChange={handleRoleChange}
        onOpenUpload={() => setUploadModalOpen(true)}
        onToggleInspector={() => setInspectorOpen(prev => !prev)}
        inspectorOpen={inspectorOpen}
        backendOnline={backendOnline}
      />

      {/* Main Dual-Pane Responsive Split Workspace */}
      <main className="flex-1 flex overflow-hidden">
        {/* Left: Chat & Citations Pane (50% width on desktop) */}
        <div className="w-1/2 flex flex-col h-full border-r border-surface-border">
          <ChatPane
            messages={messages}
            isLoading={isLoading}
            onSendMessage={handleSendMessage}
            onSelectCitation={(citation) => setActiveCitation(citation)}
            activeCitation={activeCitation}
            onClearChat={() => {
              setMessages([]);
              setConversationId(null);
              setActiveCitation(null);
              setLatestInspection(null);
            }}
          />
        </div>

        {/* Right: Document & Live PDF Highlight Viewer Pane (50% width on desktop) */}
        <div className="w-1/2 flex flex-col h-full">
          <DocumentViewerPane
            documents={documents}
            activeCitation={activeCitation}
            onClearCitation={() => setActiveCitation(null)}
          />
        </div>
      </main>

      {/* RAG Inspector Observability Modal */}
      <RagInspectorModal
        isOpen={inspectorOpen}
        onClose={() => setInspectorOpen(false)}
        inspection={latestInspection}
      />

      {/* PDF Document Ingestion Modal */}
      <UploadModal
        isOpen={uploadModalOpen}
        onClose={() => setUploadModalOpen(false)}
        onUploadSuccess={loadDocuments}
      />
    </div>
  );
};

export default App;
