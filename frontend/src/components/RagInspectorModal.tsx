import React, { useEffect } from 'react';
import { RagInspection } from '../types';
import { X, Activity, Layers, Zap, Clock, ShieldCheck, ArrowRight, Database, Cpu } from 'lucide-react';

interface RagInspectorModalProps {
  isOpen: boolean;
  onClose: () => void;
  inspection: RagInspection | null;
}

export const RagInspectorModal: React.FC<RagInspectorModalProps> = ({
  isOpen,
  onClose,
  inspection
}) => {
  // Close on Escape key
  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 bg-black/80 backdrop-blur-md flex items-center justify-center p-4 sm:p-6 transition-opacity animate-fadeIn"
      onClick={onClose}
    >
      <div
        className="w-full max-w-4xl bg-[#0e1526] border border-surface-border rounded-2xl shadow-2xl overflow-hidden flex flex-col max-h-[85vh] relative z-10"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="h-16 px-6 border-b border-surface-border flex items-center justify-between bg-[#141d33] shrink-0">
          <div className="flex items-center space-x-3">
            <div className="p-2.5 rounded-xl bg-amber-500/15 text-amber-400 border border-amber-500/25">
              <Activity className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-white flex items-center space-x-2">
                <span>RAG Pipeline Observability & Inspection</span>
                <span className="text-[10px] uppercase font-mono px-2 py-0.5 rounded bg-brand-500/10 text-brand-300 border border-brand-500/20">
                  Telemetry Profile
                </span>
              </h2>
              <p className="text-xs text-surface-textMuted">
                Real-time hybrid retrieval analysis, cross-encoder scores, and stage latencies
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-2 rounded-xl text-surface-textMuted hover:text-white hover:bg-surface-card transition-colors"
            title="Close dialog (Esc)"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6 space-y-6">
          {!inspection ? (
            <div className="py-20 text-center text-surface-textMuted">
              <Activity className="w-10 h-10 text-surface-border mx-auto mb-3 animate-pulse" />
              <p className="text-sm font-semibold text-white">No query telemetry recorded in this session</p>
              <p className="text-xs text-surface-textMuted mt-1 max-w-md mx-auto">
                Submit any question in the chat pane to inspect the dense/sparse hybrid search ranks, Reciprocal Rank Fusion (RRF), and cross-encoder reranking scores.
              </p>
            </div>
          ) : (
            <>
              {/* Query & Latency Breakdown Cards */}
              <div className="grid grid-cols-5 gap-3">
                <div className="col-span-5 p-3.5 bg-[#141d33] rounded-xl border border-surface-border flex items-center justify-between text-xs">
                  <div className="truncate max-w-2xl">
                    <span className="text-surface-textMuted font-mono uppercase text-[10px] block">Inspected Query</span>
                    <span className="text-white font-medium italic">"{inspection.query}"</span>
                  </div>
                  <div className="flex items-center space-x-1.5 text-emerald-400 bg-emerald-500/10 px-2.5 py-1 rounded-lg border border-emerald-500/20 font-mono text-xs">
                    <ShieldCheck className="w-4 h-4" />
                    <span>RLS Verified</span>
                  </div>
                </div>

                <div className="p-3 bg-[#1b2542] rounded-xl border border-surface-border text-center">
                  <span className="text-[10px] uppercase font-mono text-surface-textMuted block">Total Time</span>
                  <span className="text-lg font-bold font-mono text-amber-400">
                    {inspection.latencies.totalLatencyMs}ms
                  </span>
                </div>
                <div className="p-3 bg-[#1b2542] rounded-xl border border-surface-border text-center">
                  <span className="text-[10px] uppercase font-mono text-surface-textMuted block">Embedding</span>
                  <span className="text-lg font-bold font-mono text-white">
                    {inspection.latencies.embeddingLatencyMs}ms
                  </span>
                </div>
                <div className="p-3 bg-[#1b2542] rounded-xl border border-surface-border text-center">
                  <span className="text-[10px] uppercase font-mono text-surface-textMuted block">Hybrid RRF</span>
                  <span className="text-lg font-bold font-mono text-white">
                    {inspection.latencies.retrievalLatencyMs}ms
                  </span>
                </div>
                <div className="p-3 bg-[#1b2542] rounded-xl border border-surface-border text-center">
                  <span className="text-[10px] uppercase font-mono text-surface-textMuted block">Reranker</span>
                  <span className="text-lg font-bold font-mono text-white">
                    {inspection.latencies.rerankLatencyMs}ms
                  </span>
                </div>
                <div className="p-3 bg-[#1b2542] rounded-xl border border-surface-border text-center">
                  <span className="text-[10px] uppercase font-mono text-surface-textMuted block">LLM Gen</span>
                  <span className="text-lg font-bold font-mono text-brand-400">
                    {inspection.latencies.llmLatencyMs}ms
                  </span>
                </div>
              </div>

              {/* Reranked Top-5 Chunks Table */}
              <div>
                <div className="flex items-center justify-between mb-3">
                  <h3 className="text-xs font-bold uppercase tracking-wider text-white flex items-center space-x-2">
                    <Zap className="w-4 h-4 text-brand-400" />
                    <span>Top Injected Chunks (Post-Rerank vs Pre-Rerank)</span>
                  </h3>
                  <span className="text-xs text-surface-textMuted font-mono">
                    Filtered {inspection.totalCandidatesEvaluated} candidates &rarr; Top {inspection.topRerankedChunks.length} Context Chunks
                  </span>
                </div>

                <div className="overflow-x-auto rounded-xl border border-surface-border">
                  <table className="w-full text-left text-xs">
                    <thead className="bg-[#141d33] text-surface-textMuted font-mono text-[11px] border-b border-surface-border">
                      <tr>
                        <th className="py-2.5 px-3">Rerank #</th>
                        <th className="py-2.5 px-3">Document & Page</th>
                        <th className="py-2.5 px-3">Dense (Cosine)</th>
                        <th className="py-2.5 px-3">Sparse (BM25)</th>
                        <th className="py-2.5 px-3">RRF Score</th>
                        <th className="py-2.5 px-3">Rerank Score</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-surface-border bg-[#1b2542]/40">
                      {inspection.topRerankedChunks.map((item, idx) => (
                        <tr key={idx} className="hover:bg-[#1b2542] transition-colors">
                          <td className="py-2.5 px-3 font-mono font-bold text-amber-400">
                            #{item.rerankRank}
                          </td>
                          <td className="py-2.5 px-3 truncate max-w-[200px]">
                            <span className="text-white font-medium">{item.chunk.documentName}</span>
                            <span className="text-surface-textMuted ml-1.5 font-mono text-[11px]">p.{item.chunk.pageNumber}</span>
                          </td>
                          <td className="py-2.5 px-3 font-mono">
                            <span className="text-brand-300">{item.chunk.denseScore.toFixed(3)}</span>
                            <span className="text-surface-textMuted text-[10px] ml-1">(R#{item.chunk.denseRank})</span>
                          </td>
                          <td className="py-2.5 px-3 font-mono">
                            <span className="text-emerald-400">{item.chunk.sparseScore.toFixed(2)}</span>
                            <span className="text-surface-textMuted text-[10px] ml-1">(R#{item.chunk.sparseRank})</span>
                          </td>
                          <td className="py-2.5 px-3 font-mono text-indigo-300">
                            {item.chunk.rrfScore.toFixed(4)}
                          </td>
                          <td className="py-2.5 px-3 font-mono font-bold text-amber-300">
                            {item.rerankScore.toFixed(3)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* RRF Mathematical Formula Explanation Box */}
              <div className="p-4 rounded-xl bg-[#141d33] border border-surface-border text-xs text-surface-textMuted leading-relaxed">
                <span className="font-bold text-white block mb-1">Portfolio Architecture Note:</span>
                Reciprocal Rank Fusion fuses disparate dense and sparse scales without calibration errors:
                <code className="text-brand-300 bg-[#090d16] px-2 py-0.5 rounded mx-1 font-mono">
                  RRF(d) = 1/(60 + DenseRank) + 1/(60 + SparseRank)
                </code>.
                The Cross-Encoder reranker scores candidate cross-attention across full query-chunk token sequences to eliminate the lost-in-the-middle phenomenon.
              </div>
            </>
          )}
        </div>

        {/* Footer */}
        <div className="h-14 px-6 border-t border-surface-border flex items-center justify-end bg-[#141d33] shrink-0">
          <button
            onClick={onClose}
            className="px-4 py-2 rounded-xl bg-brand-600 hover:bg-brand-500 text-white font-medium text-xs transition-colors"
          >
            Close Inspector
          </button>
        </div>
      </div>
    </div>
  );
};
