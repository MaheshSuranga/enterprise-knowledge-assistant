import React, { useState, useEffect } from 'react';
import { api } from '../api/client';
import { X, UploadCloud, FileCheck, AlertCircle, Loader2, ShieldCheck, FileText } from 'lucide-react';

interface UploadModalProps {
  isOpen: boolean;
  onClose: () => void;
  onUploadSuccess: () => void;
}

export const UploadModal: React.FC<UploadModalProps> = ({
  isOpen,
  onClose,
  onUploadSuccess
}) => {
  const [file, setFile] = useState<File | null>(null);
  const [aclRoles, setAclRoles] = useState<string>('Engineering,General');
  const [isUploading, setIsUploading] = useState<boolean>(false);
  const [isDragging, setIsDragging] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [successInfo, setSuccessInfo] = useState<string | null>(null);

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

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      setFile(e.target.files[0]);
      setError(null);
    }
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      const droppedFile = e.dataTransfer.files[0];
      if (droppedFile.type === 'application/pdf' || droppedFile.name.endsWith('.pdf')) {
        setFile(droppedFile);
        setError(null);
      } else {
        setError('Please drop a valid PDF document.');
      }
    }
  };

  const handleUpload = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!file) {
      setError('Please select a PDF document.');
      return;
    }

    setIsUploading(true);
    setError(null);
    setSuccessInfo(null);

    try {
      const res = await api.uploadDocument(file, aclRoles);
      setSuccessInfo(`Indexed ${res.filename} (${res.pageCount} pages, ${res.chunkCount} chunks, SHA: ${res.checksum?.substring(0, 10) || 'verified'}...)`);
      setTimeout(() => {
        onUploadSuccess();
        onClose();
      }, 1500);
    } catch (err: any) {
      setError(err.message || 'Upload failed');
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 bg-black/80 backdrop-blur-md flex items-center justify-center p-4 sm:p-6 transition-opacity animate-fadeIn"
      onClick={onClose}
    >
      <div
        className="w-full max-w-lg bg-[#0e1526] border border-surface-border rounded-2xl shadow-2xl overflow-hidden relative z-10 flex flex-col max-h-[90vh]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="h-16 px-6 border-b border-surface-border flex items-center justify-between bg-[#141d33] shrink-0">
          <div className="flex items-center space-x-3">
            <div className="p-2.5 rounded-xl bg-brand-500/15 text-brand-400 border border-brand-500/25">
              <UploadCloud className="w-5 h-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-white flex items-center space-x-2">
                <span>Ingest Corporate Document</span>
                <span className="text-[10px] font-mono uppercase px-2 py-0.5 rounded bg-brand-500/10 text-brand-300 border border-brand-500/20">
                  RAG Ingestion
                </span>
              </h2>
              <p className="text-xs text-surface-textMuted">PdfPig layout extraction, SHA-256 chunking, and pgvector indexing</p>
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

        {/* Form */}
        <form onSubmit={handleUpload} className="p-6 space-y-5 overflow-y-auto">
          {error && (
            <div className="p-3.5 bg-red-500/10 border border-red-500/30 rounded-xl text-xs text-red-400 flex items-center space-x-2.5">
              <AlertCircle className="w-4 h-4 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          {successInfo && (
            <div className="p-3.5 bg-emerald-500/10 border border-emerald-500/30 rounded-xl text-xs text-emerald-400 flex items-center space-x-2.5">
              <FileCheck className="w-4 h-4 shrink-0" />
              <span>{successInfo}</span>
            </div>
          )}

          {/* File Upload Drop Zone */}
          <div
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            className={`border-2 border-dashed rounded-2xl p-6 text-center transition-all bg-[#141d33]/50 cursor-pointer ${
              isDragging
                ? 'border-brand-400 bg-brand-500/10 scale-[1.01]'
                : file
                ? 'border-emerald-500/50 bg-emerald-500/5'
                : 'border-surface-border hover:border-brand-500/50 hover:bg-[#141d33]'
            }`}
          >
            <input
              type="file"
              accept=".pdf"
              onChange={handleFileChange}
              className="hidden"
              id="pdf-upload-input"
            />
            <label htmlFor="pdf-upload-input" className="cursor-pointer flex flex-col items-center">
              {file ? (
                <div className="p-3 rounded-full bg-emerald-500/20 text-emerald-400 mb-2">
                  <FileText className="w-8 h-8" />
                </div>
              ) : (
                <div className="p-3 rounded-full bg-brand-500/10 text-brand-400 mb-2">
                  <UploadCloud className="w-8 h-8" />
                </div>
              )}
              <span className="text-sm font-semibold text-white">
                {file ? file.name : 'Choose a PDF document'}
              </span>
              <span className="text-xs text-surface-textMuted mt-1">
                {file
                  ? `${(file.size / 1024).toFixed(1)} KB • Ready for layout parsing`
                  : 'Drag & drop or browse from local filesystem (max 50 MB)'}
              </span>
            </label>
          </div>

          {/* ACL Security Roles Input */}
          <div className="space-y-1.5">
            <label className="text-xs font-semibold text-slate-300 uppercase tracking-wider flex items-center space-x-1.5">
              <ShieldCheck className="w-3.5 h-3.5 text-emerald-400" />
              <span>Access Control Roles (Row-Level Security):</span>
            </label>
            <input
              type="text"
              value={aclRoles}
              onChange={(e) => setAclRoles(e.target.value)}
              placeholder="e.g. Engineering, General, Executive"
              className="w-full bg-[#1b2542] border border-surface-border rounded-xl px-4 py-2.5 text-xs text-white placeholder-surface-textMuted focus:outline-none focus:border-brand-500 transition-colors"
            />
            <span className="text-[11px] text-surface-textMuted block leading-relaxed">
              Every chunk extracted from this document will be tagged with these roles. Queries will enforce pre-filtering via RLS.
            </span>
          </div>

          {/* Submit Action Buttons */}
          <div className="flex items-center justify-end space-x-3 pt-3 border-t border-surface-border">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2.5 rounded-xl bg-[#1b2542] hover:bg-surface-border text-surface-textMuted hover:text-white text-xs font-semibold transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isUploading || !file}
              className="px-5 py-2.5 rounded-xl bg-gradient-to-r from-brand-600 to-indigo-600 hover:from-brand-500 hover:to-indigo-500 disabled:opacity-40 text-white text-xs font-semibold flex items-center space-x-2 transition-all shadow-lg shadow-brand-600/30"
            >
              {isUploading ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  <span>Parsing & Indexing...</span>
                </>
              ) : (
                <span>Upload & Index</span>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
