export interface Tenant {
  id: string;
  name: string;
  description: string;
  documentCount: number;
}

export interface DocumentSummary {
  id: string;
  filename: string;
  fileSizeBytes: number;
  pageCount: number;
  status: string;
  chunkCount: number;
  createdAt: string;
  checksum?: string;
}

export interface BoundingBox {
  pageNumber: number;
  left: number;
  top: number;
  width: number;
  height: number;
}

export interface Citation {
  citationNumber: number;
  chunkId: string;
  documentId: string;
  documentName: string;
  pageNumber: number;
  exactQuote: string;
  boundingBox?: BoundingBox | null;
}

export interface RetrievedCandidate {
  chunkId: string;
  documentId: string;
  documentName: string;
  pageNumber: number;
  chunkIndex: number;
  content: string;
  contentHash: string;
  denseScore: number;
  denseRank: number;
  sparseScore: number;
  sparseRank: number;
  rrfScore: number;
  boundingBox?: BoundingBox | null;
  aclRoles: string[];
}

export interface RerankedCandidate {
  chunk: RetrievedCandidate;
  rerankScore: number;
  rerankRank: number;
}

export interface LatencyProfile {
  totalLatencyMs: number;
  embeddingLatencyMs: number;
  retrievalLatencyMs: number;
  rerankLatencyMs: number;
  llmLatencyMs: number;
}

export interface RagInspection {
  query: string;
  latencies: LatencyProfile;
  topRetrievedCandidates: RetrievedCandidate[];
  topRerankedChunks: RerankedCandidate[];
  totalCandidatesEvaluated: number;
}

export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  citations: Citation[];
  isGrounded?: boolean;
  confidenceScore?: number;
  refusalReason?: string | null;
  latencyMs?: number;
  inspection?: RagInspection;
  createdAt: string;
}

export interface DocumentChunk {
  id: string;
  chunkIndex: number;
  pageNumber: number;
  content: string;
  contentHash: string;
  aclRoles: string[];
  metadataJson: string;
}
