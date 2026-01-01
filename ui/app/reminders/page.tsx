// Reminder candidates management page with High/Low probability lists
'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { Layout } from '@/components/Layout';
import { StatusBadge } from '@/components/StatusBadge';
import { DateTimeDisplay } from '@/components/DateTimeDisplay';
import { ConfidenceBadge } from '@/components/ConfidenceBadge';
import { EditOccurrenceModal } from '@/components/EditOccurrenceModal';
import { LoadingSpinner } from '@/components/LoadingSpinner';
import { apiService } from '@/services/api';
import { useAuth } from '@/context/AuthContext';
import type { ReminderCandidateDto } from '@/types';
import { ReminderCandidateStatus } from '@/types';

const CONFIDENCE_THRESHOLD = 0.7; // High probability threshold

export default function RemindersPage() {
  const router = useRouter();
  const { isAdmin } = useAuth();
  const [personId, setPersonId] = useState('');
  const [actionType, setActionType] = useState('');
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const queryClient = useQueryClient();
  const [editingReminder, setEditingReminder] = useState<ReminderCandidateDto | null>(null);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [executingReminders, setExecutingReminders] = useState<Set<string>>(new Set());

  const { data, isLoading } = useQuery({
    queryKey: ['reminderCandidates', { personId, actionType, status, page, pageSize }],
    queryFn: () => apiService.getReminderCandidates({ 
      personId: personId || undefined,
      actionType: actionType || undefined,
      status: status || undefined, 
      page, 
      pageSize 
    }),
    refetchInterval: 3000, // Refetch every 3 seconds for real-time updates
  });

  const forceCheckMutation = useMutation({
    mutationFn: (candidateId: string) => apiService.processReminderCandidate(candidateId),
    onMutate: (candidateId: string) => {
      setExecutingReminders((prev) => new Set(prev).add(candidateId));
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['reminderCandidates'] });
    },
    onSettled: (_, __, candidateId: string) => {
      setExecutingReminders((prev) => {
        const next = new Set(prev);
        next.delete(candidateId);
        return next;
      });
    },
  });

  const executeNowMutation = useMutation({
    mutationFn: (candidateId: string) => apiService.executeReminderNow(candidateId),
    onMutate: (candidateId: string) => {
      setExecutingReminders((prev) => new Set(prev).add(candidateId));
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['reminderCandidates'] });
    },
    onSettled: (_, __, candidateId: string) => {
      setExecutingReminders((prev) => {
        const next = new Set(prev);
        next.delete(candidateId);
        return next;
      });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => apiService.deleteReminderCandidate(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['reminderCandidates'] });
    },
  });

  const updateOccurrenceMutation = useMutation({
    mutationFn: ({ id, occurrence }: { id: string; occurrence: string | null }) =>
      apiService.updateReminderOccurrence(id, occurrence),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['reminderCandidates'] });
    },
  });

  const handleForceCheck = (candidateId: string) => {
    if (confirm('Force check this reminder candidate now?')) {
      forceCheckMutation.mutate(candidateId);
    }
  };

  const handleExecuteNow = (candidateId: string) => {
    if (confirm('Execute this reminder now (bypassing date/time checks)?')) {
      executeNowMutation.mutate(candidateId);
    }
  };

  const handleDelete = (id: string, action: string) => {
    if (confirm(`Are you sure you want to delete reminder for "${action}"?`)) {
      deleteMutation.mutate(id);
    }
  };

  const handleEdit = (candidate: ReminderCandidateDto) => {
    setEditingReminder(candidate);
    setIsEditModalOpen(true);
  };

  const handleSaveOccurrence = async (occurrence: string | null) => {
    if (editingReminder) {
      await updateOccurrenceMutation.mutateAsync({
        id: editingReminder.id,
        occurrence,
      });
      setIsEditModalOpen(false);
      setEditingReminder(null);
    }
  };

  // Filter candidates by confidence - include all statuses (Scheduled, Executed, etc.)
  // Executed reminders stay in their original lists
  const highProbabilityCandidates = data?.items.filter(
    (c: ReminderCandidateDto) => (c.confidence || 0) >= CONFIDENCE_THRESHOLD
  ) || [];

  const lowProbabilityCandidates = data?.items.filter(
    (c: ReminderCandidateDto) => (c.confidence || 0) < CONFIDENCE_THRESHOLD
  ) || [];

  // Other status reminders (only Skipped and Expired, not Executed)
  const isScheduled = (status: ReminderCandidateStatus | string) => 
    status === 'Scheduled' || status === ReminderCandidateStatus.Scheduled;
  const isExecuted = (status: ReminderCandidateStatus | string) => 
    status === 'Executed' || status === ReminderCandidateStatus.Executed;

  const renderReminderTable = (candidates: ReminderCandidateDto[], title: string, isHighProbability: boolean) => {
    if (candidates.length === 0) {
      return (
        <div className="bg-white shadow rounded-lg p-6 mb-6">
          <h2 className="text-lg font-medium text-gray-900 mb-4">{title}</h2>
          <p className="text-sm text-gray-500">No {isHighProbability ? 'high' : 'low'} probability reminders found.</p>
        </div>
      );
    }

    return (
      <div className={`shadow rounded-lg overflow-hidden mb-6 ${isHighProbability ? 'bg-green-50 border-2 border-green-200' : 'bg-yellow-50 border-2 border-yellow-200'}`}>
        <div className="px-4 py-5 sm:p-6">
          <div className="flex items-center justify-between mb-4">
            <h2 className={`text-lg font-medium ${isHighProbability ? 'text-green-900' : 'text-yellow-900'}`}>
              {title}
            </h2>
            {!isHighProbability && (
              <span className="text-xs font-medium text-yellow-800 bg-yellow-200 px-3 py-1 rounded-full">
                Manual Execution Only
              </span>
            )}
          </div>
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Person</th>
                  <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Action</th>
                  <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Confidence</th>
                  <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Check At</th>
                  <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Occurrence</th>
                  <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Style</th>
                  <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                  <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Actions</th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {candidates.map((candidate: ReminderCandidateDto) => {
                  const isExecuting = executingReminders.has(candidate.id);
                  const isHighProbability = (candidate.confidence || 0) >= CONFIDENCE_THRESHOLD;
                  const rowBgColor = isHighProbability 
                    ? 'bg-green-50 hover:bg-green-100' 
                    : 'bg-yellow-50 hover:bg-yellow-100';
                  
                  return (
                    <tr 
                      key={candidate.id} 
                      className={`transition-colors duration-200 ${isExecuting ? 'bg-indigo-50' : rowBgColor}`}
                    >
                      <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-900">{candidate.personId}</td>
                      <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">{candidate.suggestedAction}</td>
                      <td className="px-3 py-2 whitespace-nowrap text-sm">
                        <ConfidenceBadge confidence={candidate.confidence || 0} threshold={CONFIDENCE_THRESHOLD} />
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">
                        <DateTimeDisplay date={candidate.checkAtUtc} />
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">
                        {candidate.occurrence || <span className="text-gray-400">N/A</span>}
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">{candidate.style}</td>
                      <td className="px-3 py-2 whitespace-nowrap">
                        <div className="flex items-center gap-2">
                          {isExecuting && <LoadingSpinner size="sm" />}
                          <StatusBadge status={candidate.status} />
                          {isExecuting && (
                            <span className="text-xs text-indigo-600 font-medium animate-pulse">
                              Executing...
                            </span>
                          )}
                        </div>
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-sm font-medium space-x-2">
                        <button
                          onClick={() => handleForceCheck(candidate.id)}
                          disabled={isExecuting}
                          className="text-indigo-600 hover:text-indigo-900 disabled:opacity-50 disabled:cursor-not-allowed text-lg"
                          title="Check"
                        >
                          🔍
                        </button>
                        <button
                          onClick={() => handleExecuteNow(candidate.id)}
                          disabled={isExecuting}
                          className="text-green-600 hover:text-green-900 disabled:opacity-50 disabled:cursor-not-allowed text-lg"
                          title="Execute now"
                        >
                          ▶️
                        </button>
                        <button
                          onClick={() => handleEdit(candidate)}
                          disabled={isExecuting}
                          className="text-blue-600 hover:text-blue-900 disabled:opacity-50 disabled:cursor-not-allowed text-lg"
                          title="Edit"
                        >
                          ✏️
                        </button>
                        {isAdmin && (
                          <button
                            onClick={() => handleDelete(candidate.id, candidate.suggestedAction)}
                            disabled={isExecuting}
                            className="text-red-600 hover:text-red-900 disabled:opacity-50 disabled:cursor-not-allowed text-lg"
                            title="Delete"
                          >
                            🗑️
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    );
  };

  return (
    <Layout>
      <div className="px-4 py-6 sm:px-0">
        <div className="flex justify-between items-center mb-6">
          <h1 className="text-3xl font-bold text-gray-900">Reminders</h1>
          <button
            onClick={() => router.push('/reminders/create')}
            className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700 inline-flex items-center gap-2"
          >
            ➕ Create Reminder
          </button>
        </div>

        {/* Filters */}
        <div className="bg-white shadow rounded-lg mb-6 p-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-4">
            <div>
              <label htmlFor="personId" className="block text-sm font-medium text-gray-700">
                Person ID
              </label>
              <input
                type="text"
                id="personId"
                value={personId}
                onChange={(e) => setPersonId(e.target.value)}
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
              />
            </div>
            <div>
              <label htmlFor="actionType" className="block text-sm font-medium text-gray-700">
                Action Type
              </label>
              <input
                type="text"
                id="actionType"
                value={actionType}
                onChange={(e) => setActionType(e.target.value)}
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
              />
            </div>
            <div>
              <label htmlFor="status" className="block text-sm font-medium text-gray-700">
                Status
              </label>
              <select
                id="status"
                value={status}
                onChange={(e) => setStatus(e.target.value)}
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
              >
                <option value="">All</option>
                <option value="Scheduled">Scheduled</option>
                <option value="Executed">Executed</option>
                <option value="Skipped">Skipped</option>
                <option value="Expired">Expired</option>
              </select>
            </div>
            <div className="flex items-end">
              <button
                onClick={() => {
                  setPersonId('');
                  setActionType('');
                  setStatus('');
                  setPage(1);
                }}
                className="w-full px-4 py-2 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 inline-flex items-center justify-center gap-2"
              >
                🗑️ Clear Filters
              </button>
            </div>
          </div>
        </div>

        {isLoading ? (
          <div className="bg-white shadow rounded-lg p-6 text-center text-gray-500">Loading...</div>
        ) : (
          <>
            {/* Info banner */}
            <div className="bg-blue-50 border-l-4 border-blue-400 p-4 mb-6">
              <div className="flex">
                <div className="flex-shrink-0">
                  <svg className="h-5 w-5 text-blue-400" viewBox="0 0 20 20" fill="currentColor">
                    <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
                  </svg>
                </div>
                <div className="ml-3">
                  <p className="text-sm text-blue-700">
                    <strong>High Probability Reminders</strong> (≥{Math.round(CONFIDENCE_THRESHOLD * 100)}%) are automatically executed when due.
                    {' '}<strong>Low Probability Reminders</strong> (&lt;{Math.round(CONFIDENCE_THRESHOLD * 100)}%) require manual execution via the &quot;Execute now&quot; button.
                    Reminders automatically move between lists when their probability changes.
                  </p>
                </div>
              </div>
            </div>

            {renderReminderTable(
              highProbabilityCandidates,
              `High Probability Reminders (≥${Math.round(CONFIDENCE_THRESHOLD * 100)}%) - Auto Execute`,
              true
            )}
            {renderReminderTable(
              lowProbabilityCandidates,
              `Low Probability Reminders (<${Math.round(CONFIDENCE_THRESHOLD * 100)}%) - Manual Execution Only`,
              false
            )}

            {/* Other status reminders (only Skipped and Expired, not Executed) */}
            {data && data.items.filter((c: ReminderCandidateDto) => !isScheduled(c.status) && !isExecuted(c.status)).length > 0 && (
              <div className="bg-white shadow rounded-lg overflow-hidden mb-6">
                <div className="px-4 py-5 sm:p-6">
                  <h2 className="text-lg font-medium text-gray-900 mb-4">Other Reminders</h2>
                  <div className="overflow-x-auto">
                    <table className="min-w-full divide-y divide-gray-200">
                      <thead className="bg-gray-50">
                        <tr>
                          <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Person</th>
                          <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Action</th>
                          <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                          <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Check At</th>
                          <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Actions</th>
                        </tr>
                      </thead>
                      <tbody className="bg-white divide-y divide-gray-200">
                        {data.items
                          .filter((c: ReminderCandidateDto) => !isScheduled(c.status) && !isExecuted(c.status))
                          .map((candidate: ReminderCandidateDto) => (
                            <tr key={candidate.id}>
                              <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-900">{candidate.personId}</td>
                              <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">{candidate.suggestedAction}</td>
                              <td className="px-3 py-2 whitespace-nowrap">
                                <StatusBadge status={candidate.status} />
                              </td>
                              <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">
                                <DateTimeDisplay date={candidate.checkAtUtc} />
                              </td>
                              <td className="px-3 py-2 whitespace-nowrap text-sm font-medium">
                                {isAdmin && (
                                  <button
                                    onClick={() => handleDelete(candidate.id, candidate.suggestedAction)}
                                    className="text-red-600 hover:text-red-900 text-lg"
                                    title="Delete"
                                  >
                                    🗑️
                                  </button>
                                )}
                              </td>
                            </tr>
                          ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>
            )}

            {/* Pagination */}
            {data && data.totalCount > pageSize && (
              <div className="bg-white px-4 py-3 flex items-center justify-between border-t border-gray-200 sm:px-6 rounded-lg shadow">
                <div className="flex-1 flex justify-between sm:hidden">
                  <button
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page === 1}
                    className="relative inline-flex items-center px-4 py-2 border border-gray-300 text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 disabled:opacity-50"
                  >
                    Previous
                  </button>
                  <button
                    onClick={() => setPage((p) => p + 1)}
                    disabled={page * pageSize >= data.totalCount}
                    className="ml-3 relative inline-flex items-center px-4 py-2 border border-gray-300 text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 disabled:opacity-50"
                  >
                    Next
                  </button>
                </div>
                <div className="hidden sm:flex-1 sm:flex sm:items-center sm:justify-between">
                  <div>
                    <p className="text-sm text-gray-700">
                      Showing <span className="font-medium">{(page - 1) * pageSize + 1}</span> to{' '}
                      <span className="font-medium">{Math.min(page * pageSize, data.totalCount)}</span> of{' '}
                      <span className="font-medium">{data.totalCount}</span> results
                    </p>
                  </div>
                  <div>
                    <nav className="relative z-0 inline-flex rounded-md shadow-sm -space-x-px" aria-label="Pagination">
                      <button
                        onClick={() => setPage((p) => Math.max(1, p - 1))}
                        disabled={page === 1}
                        className="relative inline-flex items-center px-2 py-2 rounded-l-md border border-gray-300 bg-white text-sm font-medium text-gray-500 hover:bg-gray-50 disabled:opacity-50"
                      >
                        Previous
                      </button>
                      <button
                        onClick={() => setPage((p) => p + 1)}
                        disabled={page * pageSize >= data.totalCount}
                        className="relative inline-flex items-center px-2 py-2 rounded-r-md border border-gray-300 bg-white text-sm font-medium text-gray-500 hover:bg-gray-50 disabled:opacity-50"
                      >
                        Next
                      </button>
                    </nav>
                  </div>
                </div>
              </div>
            )}
          </>
        )}

        {/* Edit Occurrence Modal */}
        {editingReminder && (
          <EditOccurrenceModal
            isOpen={isEditModalOpen}
            currentOccurrence={editingReminder.occurrence}
            onClose={() => {
              setIsEditModalOpen(false);
              setEditingReminder(null);
            }}
            onSave={handleSaveOccurrence}
          />
        )}
      </div>
    </Layout>
  );
}
