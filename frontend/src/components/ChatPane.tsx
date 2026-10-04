import React, { useState, useRef, useEffect } from 'react';
import { ChatMessage, Citation } from '../types';
import {
  Send,
  Sparkles,
  AlertTriangle,
  CheckCircle2,
  Sliders,
  ExternalLink,
  Zap,
  Clock,
  BookOpen,
  Trash2,
  ShieldCheck,
  Cpu
} from 'lucide-react';

interface ChatPaneProps {
  messages: ChatMessage[];
  isLoading: boolean;
  onSendMessage: (query: string, topK: number, topN: number) => void;
  onSelectCitation: (citation: Citation) => void;
  activeCitation: Citation | null;
  onClearChat: () => void;
}

const SAMPLE_PROMPTS = [
  {
    title: 'Engine Thermal Limits',
    query: 'What is the peak chamber temperature of the Acme Ion-Drive thruster?',
    badge: 'Factual Grounding',
    color: 'text-brand-400 bg-brand-500/10 border-brand-500/25'
  },
  {
    title: 'Emergency SCRAM Protocol',
    query: 'What are the emergency shutdown protocols when magnetic containment degrades?',
    badge: 'Safety Procedure',
    color: 'text-indigo-400 bg-indigo-500/10 border-indigo-500/25'
  },
  {
    title: 'Executive Bonuses (RLS Check)',
    query: 'What bonuses do executives receive for Phase 4 deployment?',
    badge: 'Security Pre-Filter',
    color: 'text-purple-400 bg-purple-500/10 border-purple-500/25'
  },
  {
    title: 'Adversarial Question',
    query: 'What kind of pizza does the cafeteria chef serve on Fridays?',
    badge: 'Hallucination Refusal',
    color: 'text-amber-400 bg-amber-500/10 border-amber-500/25'
  }
];

export const ChatPane: React.FC<ChatPaneProps> = ({
  messages,
  isLoading,
  onSendMessage,
  onSelectCitation,
  activeCitation,
  onClearChat
}) => {
  const [inputQuery, setInputQuery] = useState('');
  const [topK, setTopK] = useState(25);
  const [topN, setTopN] = useState(5);
  const [showTuning, setShowTuning] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, isLoading]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputQuery.trim() || isLoading) return;
    onSendMessage(inputQuery.trim(), topK, topN);
    setInputQuery('');
  };

  return (
    <div className="flex-1 flex flex-col h-full bg-[#090d16] overflow-hidden relative">
      {/* Top Chat Bar */}
      <div className="h-14 border-b border-surface-border px-6 flex items-center justify-between bg-[#0e1526]/95 backdrop-blur-md shrink-0">
        <div className="flex items-center space-x-2 text-xs font-semibold text-slate-200">
          <BookOpen className="w-4 h-4 text-brand-400" />
          <span>Grounded Dialogue Session</span>
          {messages.length > 0 && (
            <span className="text-[10px] font-mono px-2 py-0.5 rounded-full bg-[#1b2542] text-brand-300 border border-surface-border">
              {messages.length} messages
            </span>
          )}
        </div>
        {messages.length > 0 && (
          <button
            onClick={onClearChat}
            className="flex items-center space-x-1.5 text-xs text-surface-textMuted hover:text-red-400 transition-colors px-2.5 py-1 rounded-lg hover:bg-[#1b2542]"
            title="Reset conversation"
          >
            <Trash2 className="w-3.5 h-3.5" />
            <span>Clear Thread</span>
          </button>
        )}
      </div>

      {/* Messages Scroll Area */}
      <div className="flex-1 overflow-y-auto p-6 space-y-6">
        {messages.length === 0 ? (
          <div className="h-full flex flex-col items-center justify-center text-center max-w-lg mx-auto py-8">
            <div className="w-16 h-16 rounded-2xl bg-gradient-to-tr from-brand-600/25 via-indigo-600/25 to-blue-500/25 border border-brand-500/30 flex items-center justify-center mb-5 text-brand-400 shadow-xl shadow-brand-500/15">
              <Sparkles className="w-8 h-8" />
            </div>
            <h2 className="text-xl font-bold text-white mb-2 tracking-tight">Enterprise Knowledge Assistant</h2>
            <p className="text-xs text-surface-textMuted mb-8 leading-relaxed max-w-md">
              Answers are strictly grounded in verified corporate PDF documents with sub-second hybrid retrieval, cross-encoder reranking, and live page citation linking.
            </p>

            <div className="w-full space-y-2.5 text-left">
              <span className="text-[11px] font-bold text-surface-textMuted uppercase tracking-wider block mb-2 px-1">
                Select a verification scenario to query:
              </span>
              <div className="grid grid-cols-1 gap-2.5">
                {SAMPLE_PROMPTS.map((prompt, idx) => (
                  <button
                    key={idx}
                    onClick={() => onSendMessage(prompt.query, topK, topN)}
                    className="p-3.5 rounded-2xl bg-[#0e1526] hover:bg-[#141d33] border border-surface-border hover:border-brand-500/50 text-left transition-all group flex items-start justify-between shadow-sm"
                  >
                    <div className="pr-3">
                      <div className="flex items-center space-x-2">
                        <span className="text-xs font-semibold text-white group-hover:text-brand-300 transition-colors">
                          {prompt.title}
                        </span>
                        <span className={`text-[10px] px-2 py-0.5 rounded font-mono border ${prompt.color}`}>
                          {prompt.badge}
                        </span>
                      </div>
                      <p className="text-xs text-surface-textMuted mt-1 line-clamp-1">{prompt.query}</p>
                    </div>
                    <Send className="w-4 h-4 text-surface-textMuted group-hover:text-brand-400 transition-colors mt-1 shrink-0" />
                  </button>
                ))}
              </div>
            </div>
          </div>
        ) : (
          messages.map((msg) => (
            <div
              key={msg.id}
              className={`flex flex-col ${msg.role === 'user' ? 'items-end' : 'items-start'}`}
            >
              <div className="flex items-center space-x-2 mb-1.5 px-2">
                <span className="text-[11px] font-bold text-surface-textMuted uppercase tracking-wider">
                  {msg.role === 'user' ? 'You' : 'Grounded Assistant'}
                </span>
                {msg.latencyMs && (
                  <span className="text-[10px] font-mono px-2 py-0.5 rounded-full bg-[#141d33] border border-surface-border text-amber-400 flex items-center space-x-1 shadow-sm">
                    <Clock className="w-3 h-3" />
                    <span>{msg.latencyMs}ms</span>
                  </span>
                )}
              </div>

              <div
                className={`max-w-[88%] rounded-2xl p-5 text-sm leading-relaxed shadow-xl ${
                  msg.role === 'user'
                    ? 'bg-gradient-to-r from-brand-600 to-indigo-600 text-white rounded-br-none shadow-brand-600/20'
                    : 'bg-[#0e1526] border border-surface-border text-slate-200 rounded-bl-none shadow-black/50'
                }`}
              >
                {/* Assistant Grounding Badge */}
                {msg.role === 'assistant' && (
                  <div className="flex items-center justify-between mb-3.5 pb-2.5 border-b border-surface-border">
                    {msg.isGrounded ? (
                      <span className="inline-flex items-center space-x-1.5 text-xs font-semibold text-emerald-400 bg-emerald-500/10 px-2.5 py-1 rounded-lg border border-emerald-500/25">
                        <CheckCircle2 className="w-3.5 h-3.5" />
                        <span>Strictly Grounded ({Math.round((msg.confidenceScore || 0.95) * 100)}% Confidence)</span>
                      </span>
                    ) : (
                      <span className="inline-flex items-center space-x-1.5 text-xs font-semibold text-amber-400 bg-amber-500/10 px-2.5 py-1 rounded-lg border border-amber-500/25">
                        <AlertTriangle className="w-3.5 h-3.5" />
                        <span>Refusal Policy: {msg.refusalReason || 'Insufficient Context'}</span>
                      </span>
                    )}
                  </div>
                )}

                <div className="whitespace-pre-wrap leading-relaxed text-slate-100">{msg.content}</div>

                {/* Verified Citations Interactive Links */}
                {msg.citations && msg.citations.length > 0 && (
                  <div className="mt-4 pt-3.5 border-t border-surface-border">
                    <span className="text-[10px] font-bold uppercase tracking-wider text-surface-textMuted block mb-2">
                      Verified Citations (Click to jump & highlight in PDF viewer):
                    </span>
                    <div className="flex flex-wrap gap-2">
                      {msg.citations.map((c) => {
                        const isSelected = activeCitation?.chunkId === c.chunkId;
                        return (
                          <button
                            key={c.citationNumber}
                            onClick={() => onSelectCitation(c)}
                            className={`flex items-center space-x-2 px-3 py-1.5 rounded-lg text-xs font-medium border transition-all text-left shadow-sm ${
                              isSelected
                                ? 'bg-amber-500 text-slate-950 font-bold border-amber-400 shadow-md shadow-amber-500/25 scale-[1.02]'
                                : 'bg-[#090d16] hover:bg-[#141d33] hover:border-amber-400/50 border-surface-border text-slate-200'
                            }`}
                          >
                            <span className={`w-4 h-4 rounded-full font-mono text-[10px] flex items-center justify-center font-bold ${
                              isSelected ? 'bg-black text-amber-300' : 'bg-amber-500/20 text-amber-400'
                            }`}>
                              {c.citationNumber}
                            </span>
                            <span className="truncate max-w-[200px] font-mono text-[11px]">
                              p.{c.pageNumber} • {c.documentName}
                            </span>
                            <ExternalLink className="w-3 h-3 opacity-70 shrink-0" />
                          </button>
                        );
                      })}
                    </div>
                  </div>
                )}
              </div>
            </div>
          ))
        )}

        {isLoading && (
          <div className="flex items-center space-x-3 text-surface-textMuted text-xs p-4 bg-[#0e1526] rounded-2xl border border-surface-border w-fit shadow-lg animate-pulse">
            <Zap className="w-4 h-4 text-amber-400 animate-spin" />
            <span className="font-medium text-slate-300">Executing Hybrid Search (pgvector + BM25) & Cohere Reranking...</span>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Query Input Area with Tuning Popover */}
      <div className="p-4 bg-[#0e1526]/95 border-t border-surface-border backdrop-blur-md shrink-0">
        {showTuning && (
          <div className="mb-3.5 p-4 bg-[#141d33] rounded-2xl border border-surface-border text-xs flex items-center justify-between space-x-6 shadow-2xl">
            <div className="flex-1 flex items-center space-x-3">
              <span className="text-surface-textMuted font-mono">Hybrid Candidates (Top-K):</span>
              <input
                type="range"
                min="5"
                max="50"
                value={topK}
                onChange={(e) => setTopK(Number(e.target.value))}
                className="w-32 accent-brand-500 cursor-pointer"
              />
              <span className="font-mono font-bold text-white bg-[#090d16] px-2 py-0.5 rounded border border-surface-border">
                {topK}
              </span>
            </div>
            <div className="flex-1 flex items-center space-x-3">
              <span className="text-surface-textMuted font-mono">Cross-Encoder (Top-N):</span>
              <input
                type="range"
                min="1"
                max="10"
                value={topN}
                onChange={(e) => setTopN(Number(e.target.value))}
                className="w-24 accent-brand-500 cursor-pointer"
              />
              <span className="font-mono font-bold text-white bg-[#090d16] px-2 py-0.5 rounded border border-surface-border">
                {topN}
              </span>
            </div>
          </div>
        )}

        <form onSubmit={handleSubmit} className="flex items-center space-x-3">
          <button
            type="button"
            onClick={() => setShowTuning(!showTuning)}
            className={`p-3 rounded-xl border transition-all ${
              showTuning
                ? 'bg-brand-600 text-white border-brand-400 shadow-md shadow-brand-500/25'
                : 'bg-[#1b2542] hover:bg-surface-border border-surface-border text-surface-textMuted hover:text-white'
            }`}
            title="Adjust RAG Hyperparameters"
          >
            <Sliders className="w-4 h-4" />
          </button>

          <input
            type="text"
            value={inputQuery}
            onChange={(e) => setInputQuery(e.target.value)}
            placeholder="Ask a question grounded in corporate documentation..."
            disabled={isLoading}
            className="flex-1 bg-[#141d33] border border-surface-border rounded-xl px-4 py-3 text-sm text-white placeholder-surface-textMuted focus:outline-none focus:border-brand-500 transition-colors shadow-inner"
          />

          <button
            type="submit"
            disabled={isLoading || !inputQuery.trim()}
            className="px-5 py-3 rounded-xl bg-gradient-to-r from-brand-600 to-indigo-600 hover:from-brand-500 hover:to-indigo-500 disabled:opacity-40 text-white font-semibold text-sm flex items-center space-x-2 transition-all shadow-lg shadow-brand-600/30"
          >
            <span>Ask</span>
            <Send className="w-4 h-4" />
          </button>
        </form>
      </div>
    </div>
  );
};
