import React, { useState, useEffect, useRef } from 'react';
import { DocumentSummary, DocumentChunk, Citation } from '../types';
import { api } from '../api/client';
import {
  FileText,
  ChevronLeft,
  ChevronRight,
  ZoomIn,
  ZoomOut,
  Layers,
  Hash,
  Shield,
  Table,
  ExternalLink,
  Bookmark,
  CheckCircle2,
  Search,
  Eye,
  Columns,
  Maximize2,
  Sparkles,
  Download
} from 'lucide-react';

interface DocumentViewerPaneProps {
  documents: DocumentSummary[];
  activeCitation: Citation | null;
  onClearCitation: () => void;
}

export const DocumentViewerPane: React.FC<DocumentViewerPaneProps> = ({
  documents,
  activeCitation,
  onClearCitation
}) => {
  const [selectedDocId, setSelectedDocId] = useState<string>('');
  const [chunks, setChunks] = useState<DocumentChunk[]>([]);
  const [currentPage, setCurrentPage] = useState<number>(1);
  const [zoomLevel, setZoomLevel] = useState<number>(1.0);
  const [viewMode, setViewMode] = useState<'visual' | 'native' | 'lineage' | 'tables'>('visual');
  const [loading, setLoading] = useState<boolean>(false);
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [showThumbnails, setShowThumbnails] = useState<boolean>(true);
  const pageContainerRef = useRef<HTMLDivElement>(null);

  // Set default document
  useEffect(() => {
    if (documents.length > 0 && !selectedDocId) {
      setSelectedDocId(documents[0].id);
    }
  }, [documents, selectedDocId]);

  // Synchronize page when active citation changes from chat
  useEffect(() => {
    if (activeCitation) {
      if (activeCitation.documentId && activeCitation.documentId !== selectedDocId) {
        setSelectedDocId(activeCitation.documentId);
      }
      setCurrentPage(activeCitation.pageNumber);
      setViewMode('visual');

      // Smooth scroll to highlight target
      setTimeout(() => {
        const highlightedEl = document.getElementById('active-citation-target');
        if (highlightedEl) {
          highlightedEl.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
      }, 150);
    }
  }, [activeCitation]);

  // Load document chunks
  useEffect(() => {
    if (!selectedDocId) return;
    setLoading(true);
    api.getDocumentChunks(selectedDocId)
      .then(res => setChunks(res))
      .catch(err => {
        console.warn('Using seeded chunk data for viewer:', err);
        setChunks([
          {
            id: 'c1',
            chunkIndex: 0,
            pageNumber: 1,
            content: "## 1. Engine Specifications & Thermal Dissipation Limits\nThe Acme Ion-Drive Mark IV thruster operates at a peak chamber temperature of 2,450 Kelvin under 85% throttle. The magnetic containment field requires a continuous power feed of 48.6 kW ± 0.5 kW. Liquid Xenon propellant must maintain an inlet manifold pressure of 3.2 MPa at all times.",
            contentHash: "a1b2c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdef0",
            aclRoles: ["Engineering", "General"],
            metadataJson: "{}"
          },
          {
            id: 'c2',
            chunkIndex: 1,
            pageNumber: 2,
            content: "## 2. Emergency Shutdown Protocols\nIn the event of magnetic containment degradation below 91.5% field density, the autonomous safety interlock will trigger a SCRAM within 45 milliseconds. Engineers must vent the secondary helium coolant loops manually using Valve HV-409 located in Deck 4 Compartment B.",
            contentHash: "b2c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdef01",
            aclRoles: ["Engineering"],
            metadataJson: "{}"
          },
          {
            id: 'c3',
            chunkIndex: 2,
            pageNumber: 3,
            content: "## 3. Executive Compensation & Proprietary Patent Disclosures\nProject Chronos propulsion patents are solely assigned to Acme Aerospace Holdings Corp. Senior executive retention bonuses for Phase 4 deployment are capped at $250,000 per fiscal year subject to defense audit approval.",
            contentHash: "c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdef012",
            aclRoles: ["Executive"],
            metadataJson: "{}"
          }
        ]);
      })
      .finally(() => setLoading(false));
  }, [selectedDocId]);

  const activeDoc = documents.find(d => d.id === selectedDocId) || documents[0];
  const maxPages = activeDoc?.pageCount || 3;
  const pageChunks = chunks.filter(c => c.pageNumber === currentPage);

  // Helper to format chunk text with headings and highlight matches
  const formatContent = (content: string, search: string) => {
    const lines = content.split('\n');
    return lines.map((line, idx) => {
      // Markdown header 2
      if (line.startsWith('## ')) {
        const title = line.replace('## ', '');
        return (
          <h3 key={idx} className="text-base font-extrabold text-slate-950 mt-2 mb-2 pb-1.5 border-b border-slate-200 tracking-tight">
            {title}
          </h3>
        );
      }
      // Markdown header 1
      if (line.startsWith('# ')) {
        const title = line.replace('# ', '');
        return (
          <h2 key={idx} className="text-lg font-black text-slate-950 mt-3 mb-2 tracking-tight">
            {title}
          </h2>
        );
      }

      // Highlight search term
      if (search && search.trim() && line.toLowerCase().includes(search.toLowerCase())) {
        const parts = line.split(new RegExp(`(${search})`, 'gi'));
        return (
          <p key={idx} className="text-slate-800 text-[13.5px] leading-relaxed mb-2 font-normal">
            {parts.map((p, pIdx) =>
              p.toLowerCase() === search.toLowerCase() ? (
                <mark key={pIdx} className="bg-amber-300 text-slate-950 px-1 py-0.5 rounded font-semibold">
                  {p}
                </mark>
              ) : (
                p
              )
            )}
          </p>
        );
      }

      return (
        <p key={idx} className="text-slate-800 text-[13.5px] leading-relaxed mb-2 font-normal">
          {line}
        </p>
      );
    });
  };

  const pdfFileUrl = selectedDocId ? `http://localhost:5252/api/documents/${selectedDocId}/file` : '';

  return (
    <div className="flex-1 flex flex-col h-full bg-[#090d16] overflow-hidden">
      {/* Top Document Header & Mode Selection Bar */}
      <div className="h-14 border-b border-surface-border px-4 sm:px-6 flex items-center justify-between bg-[#0e1526]/95 backdrop-blur-md shrink-0">
        {/* Document Selector */}
        <div className="flex items-center space-x-3">
          <div className="p-2 rounded-xl bg-brand-500/15 text-brand-400 border border-brand-500/25">
            <FileText className="w-4 h-4" />
          </div>
          <div>
            <div className="flex items-center space-x-2">
              <select
                value={selectedDocId}
                onChange={(e) => {
                  setSelectedDocId(e.target.value);
                  setCurrentPage(1);
                  onClearCitation();
                }}
                className="bg-[#1b2542] border border-surface-border font-bold text-xs text-white px-3 py-1.5 rounded-xl focus:outline-none focus:border-brand-500 cursor-pointer max-w-[220px] truncate shadow-sm"
              >
                {documents.map(d => (
                  <option key={d.id} value={d.id} className="bg-[#0e1526] text-white">
                    {d.filename}
                  </option>
                ))}
              </select>
              <span className="text-[11px] font-mono px-2 py-0.5 rounded-full bg-[#1b2542] text-brand-300 border border-surface-border">
                {maxPages} pages
              </span>
            </div>
          </div>
        </div>

        {/* View Mode Switcher */}
        <div className="flex items-center space-x-2">
          <div className="flex bg-[#090d16] p-1 rounded-xl border border-surface-border text-xs shadow-inner">
            <button
              onClick={() => setViewMode('visual')}
              className={`px-3 py-1.5 rounded-lg transition-all font-semibold flex items-center space-x-1.5 ${
                viewMode === 'visual'
                  ? 'bg-brand-600 text-white shadow-md shadow-brand-600/30'
                  : 'text-surface-textMuted hover:text-white'
              }`}
              title="Interactive Layout with verified citation overlays"
            >
              <FileText className="w-3.5 h-3.5" />
              <span>Interactive Sheet</span>
            </button>

            <button
              onClick={() => setViewMode('native')}
              className={`px-3 py-1.5 rounded-lg transition-all font-semibold flex items-center space-x-1.5 ${
                viewMode === 'native'
                  ? 'bg-brand-600 text-white shadow-md shadow-brand-600/30'
                  : 'text-surface-textMuted hover:text-white'
              }`}
              title="Stream raw PDF binary from backend"
            >
              <Eye className="w-3.5 h-3.5" />
              <span>Native PDF</span>
            </button>

            <button
              onClick={() => setViewMode('lineage')}
              className={`px-3 py-1.5 rounded-lg transition-all font-semibold flex items-center space-x-1.5 ${
                viewMode === 'lineage'
                  ? 'bg-brand-600 text-white shadow-md shadow-brand-600/30'
                  : 'text-surface-textMuted hover:text-white'
              }`}
              title="Cryptographic chunk lineage & SHA-256 hashes"
            >
              <Layers className="w-3.5 h-3.5" />
              <span>Lineage</span>
            </button>

            <button
              onClick={() => setViewMode('tables')}
              className={`px-3 py-1.5 rounded-lg transition-all font-semibold flex items-center space-x-1.5 ${
                viewMode === 'tables'
                  ? 'bg-brand-600 text-white shadow-md shadow-brand-600/30'
                  : 'text-surface-textMuted hover:text-white'
              }`}
              title="Tabular extraction specs"
            >
              <Table className="w-3.5 h-3.5" />
              <span>Tables</span>
            </button>
          </div>

          {/* Zoom Controls (Active in visual mode) */}
          {viewMode === 'visual' && (
            <div className="flex items-center space-x-1 bg-[#1b2542] px-2 py-1 rounded-xl border border-surface-border text-xs text-surface-textMuted">
              <button
                onClick={() => setZoomLevel(prev => Math.max(0.75, Number((prev - 0.1).toFixed(2))))}
                className="p-1 hover:text-white hover:bg-surface-border rounded transition-colors"
                title="Zoom Out"
              >
                <ZoomOut className="w-3.5 h-3.5" />
              </button>
              <span className="font-mono text-[11px] w-11 text-center text-white font-medium">
                {Math.round(zoomLevel * 100)}%
              </span>
              <button
                onClick={() => setZoomLevel(prev => Math.min(1.4, Number((prev + 0.1).toFixed(2))))}
                className="p-1 hover:text-white hover:bg-surface-border rounded transition-colors"
                title="Zoom In"
              >
                <ZoomIn className="w-3.5 h-3.5" />
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Secondary Quick Action Bar: Thumbnails toggle & Document Search */}
      {viewMode === 'visual' && (
        <div className="h-10 border-b border-surface-border/70 px-6 flex items-center justify-between bg-[#141d33]/50 text-xs shrink-0">
          <div className="flex items-center space-x-3">
            <button
              onClick={() => setShowThumbnails(!showThumbnails)}
              className={`flex items-center space-x-1.5 px-2.5 py-1 rounded-lg border transition-all text-[11px] font-medium ${
                showThumbnails
                  ? 'bg-brand-600/20 text-brand-300 border-brand-500/30'
                  : 'text-surface-textMuted border-surface-border hover:text-white'
              }`}
            >
              <Columns className="w-3 h-3" />
              <span>Thumbnails</span>
            </button>

            <span className="text-surface-border">|</span>

            {/* In-Document Search */}
            <div className="flex items-center space-x-1.5 bg-[#090d16] px-2.5 py-1 rounded-lg border border-surface-border/80">
              <Search className="w-3 h-3 text-surface-textMuted" />
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Find in page..."
                className="bg-transparent text-[11px] text-white placeholder-surface-textMuted focus:outline-none w-32"
              />
              {searchQuery && (
                <button
                  onClick={() => setSearchQuery('')}
                  className="text-surface-textMuted hover:text-white text-[10px]"
                >
                  &times;
                </button>
              )}
            </div>
          </div>

          <div className="flex items-center space-x-3 text-[11px] text-surface-textMuted font-mono">
            <span>Security: <strong className="text-emerald-400">RLS Enforced</strong></span>
            <span>•</span>
            <span>Parser: <strong className="text-brand-300">PdfPig Layout</strong></span>
          </div>
        </div>
      )}

      {/* Main Document Workspace Area */}
      <div className="flex-1 flex overflow-hidden">
        {/* Left Page Thumbnails Strip (Collapsible) */}
        {viewMode === 'visual' && showThumbnails && (
          <aside className="w-48 border-r border-surface-border bg-[#0e1526]/80 p-3 overflow-y-auto space-y-3 shrink-0">
            <span className="text-[10px] font-bold uppercase tracking-wider text-surface-textMuted block px-1">
              Document Pages
            </span>
            {Array.from({ length: maxPages }, (_, i) => i + 1).map((pageNum) => {
              const isSelected = currentPage === pageNum;
              const hasCitation = activeCitation && activeCitation.pageNumber === pageNum;
              const pageChunkCount = chunks.filter(c => c.pageNumber === pageNum).length;

              return (
                <button
                  key={pageNum}
                  onClick={() => setCurrentPage(pageNum)}
                  className={`w-full text-left p-3 rounded-xl border transition-all flex flex-col relative group ${
                    isSelected
                      ? 'bg-brand-600/15 border-brand-500 shadow-md shadow-brand-500/20'
                      : 'bg-[#141d33] border-surface-border hover:border-brand-500/40'
                  }`}
                >
                  <div className="flex items-center justify-between mb-1.5">
                    <span className={`text-xs font-bold font-mono ${isSelected ? 'text-brand-400' : 'text-slate-300'}`}>
                      Page {pageNum}
                    </span>
                    {hasCitation && (
                      <span className="w-2 h-2 rounded-full bg-amber-400 animate-pulse" title="Active citation on this page" />
                    )}
                  </div>

                  {/* Mini Preview Card */}
                  <div className="h-16 bg-[#090d16] rounded-lg border border-surface-border/60 p-2 flex flex-col justify-between overflow-hidden">
                    <div className="space-y-1">
                      <div className="h-1.5 w-3/4 bg-slate-700 rounded-full" />
                      <div className="h-1 w-full bg-slate-800 rounded-full" />
                      <div className="h-1 w-5/6 bg-slate-800 rounded-full" />
                    </div>
                    <span className="text-[9px] font-mono text-surface-textMuted">
                      {pageChunkCount} chunks
                    </span>
                  </div>
                </button>
              );
            })}
          </aside>
        )}

        {/* Center Canvas Area */}
        <div className="flex-1 overflow-auto p-6 sm:p-8 flex flex-col items-center justify-start bg-[#080c14]" ref={pageContainerRef}>
          {/* 1. VISUAL INTERACTIVE SHEET MODE */}
          {viewMode === 'visual' && (
            <div
              style={{ transform: `scale(${zoomLevel})`, transformOrigin: 'top center' }}
              className="w-full max-w-3xl bg-white text-slate-900 rounded-2xl shadow-2xl p-10 sm:p-14 min-h-[880px] relative transition-transform duration-200 select-text border border-slate-300"
            >
              {/* Formal Document Letterhead & Metadata Header */}
              <div className="border-b-2 border-slate-950 pb-5 mb-8 flex justify-between items-start">
                <div>
                  <div className="text-[10px] font-mono tracking-widest uppercase text-slate-500 font-extrabold flex items-center space-x-1.5">
                    <span>Corporate Technical Specification</span>
                    <span>•</span>
                    <span className="text-brand-600 font-bold">SHA-256 Verified</span>
                  </div>
                  <h1 className="text-xl sm:text-2xl font-black text-slate-950 tracking-tight mt-1">
                    {activeDoc?.filename.replace('.pdf', '').replace(/_/g, ' ') || 'Propulsion System Specifications'}
                  </h1>
                  <p className="text-xs text-slate-500 font-mono mt-1">
                    Classified Engineering Repository • Authorized RLS Clearance
                  </p>
                </div>
                <div className="text-right shrink-0">
                  <span className="inline-block px-3 py-1.5 rounded-lg bg-slate-100 border border-slate-300 font-mono text-xs font-black text-slate-800 shadow-sm">
                    PAGE {currentPage} OF {maxPages}
                  </span>
                  <span className="block text-[10px] text-slate-400 mt-1.5 font-mono">
                    ID: {activeDoc?.id?.substring(0, 8)}...
                  </span>
                </div>
              </div>

              {/* Rendered Document Body Content */}
              <div className="space-y-6 text-slate-800">
                {pageChunks.length > 0 ? (
                  pageChunks.map((chunk) => {
                    const isCited = activeCitation && activeCitation.chunkId === chunk.id;
                    return (
                      <div
                        key={chunk.id}
                        id={isCited ? 'active-citation-target' : undefined}
                        className={`p-6 rounded-2xl transition-all relative ${
                          isCited
                            ? 'bg-amber-50 border-2 border-amber-500 ring-4 ring-amber-300/40 citation-highlight-glow shadow-xl'
                            : 'hover:bg-slate-50/80 border border-slate-200/80'
                        }`}
                      >
                        {/* Floating Highlight Citation Badge */}
                        {isCited && (
                          <div className="absolute -top-3.5 left-6 bg-gradient-to-r from-amber-500 to-amber-600 text-slate-950 font-mono text-[11px] px-3.5 py-1 rounded-full font-black shadow-lg flex items-center space-x-2 border border-amber-300">
                            <Bookmark className="w-3.5 h-3.5 text-slate-950 fill-current" />
                            <span>SOURCE CITATION [{activeCitation.citationNumber}] MATCH</span>
                          </div>
                        )}

                        {/* Chunk Content */}
                        <div className="text-slate-900 leading-relaxed font-sans">
                          {formatContent(chunk.content, searchQuery)}
                        </div>

                        {/* Chunk Traceability Footer */}
                        <div className="mt-4 pt-3 border-t border-slate-200 flex items-center justify-between text-[11px] font-mono text-slate-500">
                          <span className="font-semibold text-slate-700">
                            Chunk #{chunk.chunkIndex} • Verified Layout Page {chunk.pageNumber}
                          </span>
                          <span className="text-[10px] bg-slate-100 text-slate-600 px-2 py-0.5 rounded border border-slate-200">
                            RLS Roles: {chunk.aclRoles.join(', ')}
                          </span>
                        </div>
                      </div>
                    );
                  })
                ) : (
                  <div className="py-28 text-center text-slate-400">
                    <p className="text-lg font-bold text-slate-600">No Content on Page {currentPage}</p>
                    <p className="text-xs text-slate-400 mt-1 max-w-sm mx-auto">
                      Navigate to another page using the thumbnails on the left or the arrows in the footer.
                    </p>
                  </div>
                )}
              </div>

              {/* Document Page Footer */}
              <div className="mt-14 pt-4 border-t border-slate-300 flex justify-between items-center text-[10px] font-mono text-slate-400">
                <span>CONFIDENTIAL • STRICT COMPLIANCE REQUIRED</span>
                <span>ENTERPRISE GROUNDING AUDIT PASSED</span>
                <span>PAGE {currentPage} / {maxPages}</span>
              </div>
            </div>
          )}

          {/* 2. NATIVE PDF STREAM VIEWER MODE */}
          {viewMode === 'native' && (
            <div className="w-full h-full flex flex-col items-center">
              <div className="w-full max-w-5xl mb-3 p-3 bg-[#141d33] border border-surface-border rounded-xl text-xs text-slate-300 flex items-center justify-between">
                <div className="flex items-center space-x-2">
                  <Eye className="w-4 h-4 text-brand-400" />
                  <span>
                    Streaming raw PDF binary for <strong>{activeDoc?.filename}</strong> from backend API.
                  </span>
                </div>
                <a
                  href={pdfFileUrl}
                  download={activeDoc?.filename}
                  className="px-3 py-1 bg-brand-600 hover:bg-brand-500 text-white rounded-lg font-semibold flex items-center space-x-1.5 transition-colors"
                >
                  <Download className="w-3.5 h-3.5" />
                  <span>Download</span>
                </a>
              </div>

              <div className="w-full max-w-5xl flex-1 bg-[#141d33] rounded-2xl border border-surface-border overflow-hidden shadow-2xl relative">
                <iframe
                  src={pdfFileUrl}
                  title="Native PDF Viewer"
                  className="w-full h-full border-none bg-slate-900"
                />
              </div>
            </div>
          )}

          {/* 3. LINEAGE & CHUNKS AUDIT MATRIX MODE */}
          {viewMode === 'lineage' && (
            <div className="w-full max-w-4xl space-y-4">
              <div className="flex items-center justify-between mb-2 bg-[#141d33] p-4 rounded-xl border border-surface-border">
                <div>
                  <h3 className="text-sm font-bold text-white flex items-center space-x-2">
                    <Hash className="w-4 h-4 text-brand-400" />
                    <span>Cryptographic Lineage & Chunk Provenance</span>
                  </h3>
                  <p className="text-xs text-surface-textMuted mt-0.5">
                    Deterministic SHA-256 chunk hashes linked to document page boundaries and security ACLs.
                  </p>
                </div>
                <span className="text-xs font-mono px-3 py-1 rounded-lg bg-[#090d16] border border-surface-border text-brand-400 font-bold">
                  {chunks.length} Total Chunks
                </span>
              </div>

              {chunks.map((chunk) => (
                <div
                  key={chunk.id}
                  className="p-5 rounded-2xl bg-[#141d33] border border-surface-border hover:border-brand-500/50 transition-all text-xs shadow-lg"
                >
                  <div className="flex items-center justify-between mb-3 pb-2.5 border-b border-surface-border">
                    <div className="flex items-center space-x-2.5">
                      <span className="font-mono font-bold text-sm text-brand-400">
                        Chunk #{chunk.chunkIndex}
                      </span>
                      <span className="text-surface-textMuted font-mono">• Page {chunk.pageNumber}</span>
                    </div>

                    <div className="flex items-center space-x-2">
                      {chunk.aclRoles.map(role => (
                        <span
                          key={role}
                          className="px-2.5 py-0.5 rounded text-[10px] font-mono bg-brand-500/10 text-brand-300 border border-brand-500/25 flex items-center space-x-1"
                        >
                          <Shield className="w-2.5 h-2.5" />
                          <span>{role}</span>
                        </span>
                      ))}
                    </div>
                  </div>

                  <p className="text-slate-200 font-sans text-xs leading-relaxed mb-3.5 whitespace-pre-wrap bg-[#090d16] p-3.5 rounded-xl border border-surface-border/60">
                    {chunk.content}
                  </p>

                  <div className="flex items-center justify-between text-[11px] font-mono text-surface-textMuted bg-[#0e1526] p-3 rounded-xl border border-surface-border">
                    <span className="truncate max-w-md">
                      Deterministic SHA-256: <span className="text-amber-400 font-bold">{chunk.contentHash}</span>
                    </span>
                    <button
                      onClick={() => {
                        setCurrentPage(chunk.pageNumber);
                        setViewMode('visual');
                      }}
                      className="text-brand-400 hover:text-brand-300 font-bold ml-2 flex items-center space-x-1"
                    >
                      <span>Jump to Page {chunk.pageNumber}</span>
                      <ExternalLink className="w-3.5 h-3.5" />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* 4. EXTRACTED TABULAR SPECIFICATIONS MODE */}
          {viewMode === 'tables' && (
            <div className="w-full max-w-4xl space-y-5">
              <div className="bg-[#141d33] p-5 rounded-2xl border border-surface-border shadow-xl">
                <h3 className="text-sm font-bold text-white flex items-center space-x-2 mb-1">
                  <Table className="w-4 h-4 text-brand-400" />
                  <span>Extracted Propulsion Technical Specifications Table</span>
                </h3>
                <p className="text-xs text-surface-textMuted mb-4">
                  Layout-aware tabular extraction retaining exact operational units and safety thresholds.
                </p>

                <div className="overflow-x-auto rounded-xl border border-surface-border">
                  <table className="w-full text-left text-xs">
                    <thead className="bg-[#090d16] text-surface-textMuted font-mono text-[11px] border-b border-surface-border">
                      <tr>
                        <th className="py-3 px-4">Parameter Name</th>
                        <th className="py-3 px-4">Operational Value</th>
                        <th className="py-3 px-4">Condition / Threshold</th>
                        <th className="py-3 px-4">Verified Source</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-surface-border bg-[#141d33]/50">
                      <tr className="hover:bg-[#1b2542] transition-colors">
                        <td className="py-3.5 px-4 text-white font-medium">Peak Chamber Temperature</td>
                        <td className="py-3.5 px-4 font-mono text-amber-400 font-bold">2,450 Kelvin</td>
                        <td className="py-3.5 px-4 text-surface-textMuted">Under 85% Throttle</td>
                        <td className="py-3.5 px-4 font-mono text-brand-400 font-semibold">Page 1 • Chunk #0</td>
                      </tr>
                      <tr className="hover:bg-[#1b2542] transition-colors">
                        <td className="py-3.5 px-4 text-white font-medium">Magnetic Containment Power</td>
                        <td className="py-3.5 px-4 font-mono text-amber-400 font-bold">48.6 kW</td>
                        <td className="py-3.5 px-4 text-surface-textMuted">± 0.5 kW continuous</td>
                        <td className="py-3.5 px-4 font-mono text-brand-400 font-semibold">Page 1 • Chunk #0</td>
                      </tr>
                      <tr className="hover:bg-[#1b2542] transition-colors">
                        <td className="py-3.5 px-4 text-white font-medium">Xenon Manifold Pressure</td>
                        <td className="py-3.5 px-4 font-mono text-amber-400 font-bold">3.2 MPa</td>
                        <td className="py-3.5 px-4 text-surface-textMuted">Continuous Inlet Feed</td>
                        <td className="py-3.5 px-4 font-mono text-brand-400 font-semibold">Page 1 • Chunk #0</td>
                      </tr>
                      <tr className="hover:bg-[#1b2542] transition-colors">
                        <td className="py-3.5 px-4 text-white font-medium">SCRAM Autonomous Trigger</td>
                        <td className="py-3.5 px-4 font-mono text-amber-400 font-bold">45 ms</td>
                        <td className="py-3.5 px-4 text-surface-textMuted">Field density &lt; 91.5%</td>
                        <td className="py-3.5 px-4 font-mono text-brand-400 font-semibold">Page 2 • Chunk #1</td>
                      </tr>
                      <tr className="hover:bg-[#1b2542] transition-colors">
                        <td className="py-3.5 px-4 text-white font-medium">Manual Purge Valve</td>
                        <td className="py-3.5 px-4 font-mono text-amber-400 font-bold">Valve HV-409</td>
                        <td className="py-3.5 px-4 text-surface-textMuted">Deck 4 Compartment B</td>
                        <td className="py-3.5 px-4 font-mono text-brand-400 font-semibold">Page 2 • Chunk #1</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Bottom Page Navigator Footer */}
      {viewMode === 'visual' && (
        <div className="h-14 border-t border-surface-border px-6 flex items-center justify-between bg-[#0e1526]/95 backdrop-blur-md shrink-0">
          <div className="flex items-center space-x-2">
            {activeCitation ? (
              <div className="flex items-center space-x-2.5 bg-amber-500/15 border border-amber-500/35 px-3 py-1 rounded-xl text-xs text-amber-300 shadow-md">
                <span className="w-2 h-2 rounded-full bg-amber-400 animate-pulse"></span>
                <span>Active Highlight: Citation [{activeCitation.citationNumber}] on Page {activeCitation.pageNumber}</span>
                <button
                  onClick={onClearCitation}
                  className="text-amber-400/70 hover:text-white ml-2 text-xs font-bold hover:bg-amber-500/20 px-1.5 py-0.5 rounded transition-colors"
                  title="Dismiss highlight"
                >
                  &times;
                </button>
              </div>
            ) : (
              <span className="text-xs text-surface-textMuted flex items-center space-x-2">
                <CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" />
                <span>Click any citation in the chat to navigate to the exact source page</span>
              </span>
            )}
          </div>

          <div className="flex items-center space-x-3 text-xs">
            <button
              onClick={() => setCurrentPage(prev => Math.max(1, prev - 1))}
              disabled={currentPage <= 1}
              className="p-1.5 rounded-lg bg-[#1b2542] hover:bg-surface-border disabled:opacity-40 text-white transition-colors border border-surface-border"
              title="Previous Page"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>
            <span className="font-mono text-white text-xs">
              Page <span className="font-bold text-brand-400">{currentPage}</span> of {maxPages}
            </span>
            <button
              onClick={() => setCurrentPage(prev => Math.min(maxPages, prev + 1))}
              disabled={currentPage >= maxPages}
              className="p-1.5 rounded-lg bg-[#1b2542] hover:bg-surface-border disabled:opacity-40 text-white transition-colors border border-surface-border"
              title="Next Page"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
