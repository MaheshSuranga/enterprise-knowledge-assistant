import React from 'react';
import { Tenant } from '../types';
import { ShieldCheck, Database, UploadCloud, Activity, Building2, UserCircle2, Sparkles } from 'lucide-react';

interface NavbarProps {
  tenants: Tenant[];
  currentTenantId: string;
  onTenantChange: (id: string) => void;
  currentRole: string;
  onRoleChange: (role: string) => void;
  onOpenUpload: () => void;
  onToggleInspector: () => void;
  inspectorOpen: boolean;
  backendOnline: boolean;
}

const ROLES = ['Engineering', 'Executive', 'HR', 'Finance'];

export const Navbar: React.FC<NavbarProps> = ({
  tenants,
  currentTenantId,
  onTenantChange,
  currentRole,
  onRoleChange,
  onOpenUpload,
  onToggleInspector,
  inspectorOpen,
  backendOnline
}) => {
  return (
    <header className="h-16 border-b border-surface-border bg-surface-darker/95 backdrop-blur-md px-6 flex items-center justify-between z-30 shadow-md">
      {/* Brand & Title */}
      <div className="flex items-center space-x-3.5">
        <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-brand-600 via-indigo-600 to-blue-500 flex items-center justify-center shadow-lg shadow-brand-500/25 ring-1 ring-white/20">
          <Database className="w-5 h-5 text-white" />
        </div>
        <div>
          <div className="flex items-center space-x-2.5">
            <span className="font-extrabold text-base tracking-tight text-white">
              Enterprise Knowledge Assistant
            </span>
            <span className="text-[10px] uppercase font-mono px-2 py-0.5 rounded-full bg-brand-500/15 text-brand-300 border border-brand-500/30 flex items-center space-x-1">
              <Sparkles className="w-2.5 h-2.5 text-brand-400" />
              <span>Production RAG</span>
            </span>
          </div>
          <p className="text-[11px] text-surface-textMuted flex items-center space-x-2">
            <span className={`w-2 h-2 rounded-full ${backendOnline ? 'bg-emerald-400 shadow-sm shadow-emerald-400/50 animate-pulse' : 'bg-amber-400'}`}></span>
            <span>Hybrid Search (pgvector + BM25) • Cohere Rerank • Semantic Kernel Grounding</span>
          </p>
        </div>
      </div>

      {/* Center: Tenant & Role Controls (Demonstrating RLS) */}
      <div className="flex items-center space-x-3 bg-surface-dark/90 px-3 py-1.5 rounded-xl border border-surface-border/80 shadow-inner">
        {/* Tenant Switcher */}
        <div className="flex items-center space-x-2 px-2.5 py-1 rounded-lg bg-surface-darker border border-surface-border/60 text-xs text-slate-200">
          <Building2 className="w-3.5 h-3.5 text-brand-400" />
          <span className="font-medium text-surface-textMuted">Tenant:</span>
          <select
            value={currentTenantId}
            onChange={(e) => onTenantChange(e.target.value)}
            className="bg-transparent font-semibold text-white focus:outline-none cursor-pointer pr-1"
          >
            {tenants.map(t => (
              <option key={t.id} value={t.id} className="bg-surface-darker text-white">
                {t.name}
              </option>
            ))}
          </select>
        </div>

        {/* Role Switcher */}
        <div className="flex items-center space-x-1 border-l border-surface-border/60 pl-3">
          <UserCircle2 className="w-3.5 h-3.5 text-brand-400 mr-1" />
          <span className="text-xs font-medium text-surface-textMuted mr-1.5">Role:</span>
          <div className="flex items-center space-x-1 bg-surface-darker p-0.5 rounded-lg border border-surface-border/50">
            {ROLES.map(role => {
              const active = currentRole === role;
              return (
                <button
                  key={role}
                  onClick={() => onRoleChange(role)}
                  className={`text-xs px-2.5 py-1 rounded-md transition-all font-medium ${
                    active
                      ? 'bg-brand-600 text-white shadow-sm shadow-brand-600/30'
                      : 'text-surface-textMuted hover:text-white hover:bg-surface-card'
                  }`}
                >
                  {role}
                </button>
              );
            })}
          </div>
        </div>

        {/* RLS Badge */}
        <div className="flex items-center space-x-1 px-2.5 py-1 rounded-lg text-[11px] font-mono bg-emerald-500/10 text-emerald-400 border border-emerald-500/25">
          <ShieldCheck className="w-3.5 h-3.5" />
          <span>RLS Active</span>
        </div>
      </div>

      {/* Right Controls */}
      <div className="flex items-center space-x-3">
        <button
          onClick={onOpenUpload}
          className="flex items-center space-x-2 px-3.5 py-2 rounded-xl bg-surface-card hover:bg-surface-border border border-surface-border text-xs font-semibold text-white transition-all shadow-sm hover:border-brand-500/50"
        >
          <UploadCloud className="w-4 h-4 text-brand-400" />
          <span>Ingest PDF</span>
        </button>

        <button
          onClick={onToggleInspector}
          className={`flex items-center space-x-2 px-3.5 py-2 rounded-xl text-xs font-semibold border transition-all ${
            inspectorOpen
              ? 'bg-brand-600 text-white border-brand-400 shadow-md shadow-brand-500/30 ring-2 ring-brand-400/20'
              : 'bg-surface-card hover:bg-surface-border border-surface-border text-slate-200'
          }`}
        >
          <Activity className="w-4 h-4 text-amber-400" />
          <span>RAG Inspector</span>
        </button>
      </div>
    </header>
  );
};
