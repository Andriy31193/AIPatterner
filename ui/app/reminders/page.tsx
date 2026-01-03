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

  const [activeTab, setActiveTab] = useState<'high' | 'low'>('high');

  const renderReminderCard = (candidate: ReminderCandidateDto) => {
    const isExecuting = executingReminders.has(candidate.id);
    const isHighProbability = (candidate.confidence || 0) >= CONFIDENCE_THRESHOLD;
    const cardBgColor = isHighProbability 
      ? 'bg-green-50 border-green-200 hover:border-green-300' 
      : 'bg-yellow-50 border-yellow-200 hover:border-yellow-300';
    
    return (
      <div
        key={candidate.id}
        className={`border rounded-lg p-4 transition-all ${cardBgColor} ${isExecuting ? 'ring-2 ring-indigo-500' : ''}`}
      >
        <div className="flex items-start justify-between mb-3">
          <div className="flex-1">
            <h3 className="font-semibold text-gray-900 mb-1">{candidate.suggestedAction}</h3>
            <p className="text-xs text-gray-500">{candidate.personId}</p>
          </div>
          <div className="flex items-center gap-2">
            {isExecuting && <LoadingSpinner size="sm" />}
            <StatusBadge status={candidate.status} />
          </div>
        </div>
        
        <div className="flex items-center justify-between mb-3">
          <div className="flex items-center gap-4">
            <div>
              <p className="text-xs text-gray-500 mb-1">Confidence</p>
              <ConfidenceBadge confidence={candidate.confidence || 0} threshold={CONFIDENCE_THRESHOLD} />
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">Time Window</p>
              <p className="text-sm text-gray-700">
                <DateTimeDisplay date={candidate.checkAtUtc} />
              </p>
            </div>
            {candidate.occurrence && (
              <div>
                <p className="text-xs text-gray-500 mb-1">Pattern</p>
                <p className="text-sm text-gray-700">{candidate.occurrence}</p>
              </div>
            )}
          </div>
        </div>

        {isExecuting && (
          <div className="mb-3 p-2 bg-indigo-50 border border-indigo-200 rounded text-xs text-indigo-700">
            ⏳ Executing...
          </div>
        )}

        <div className="flex items-center justify-end gap-2 pt-3 border-t border-gray-200">
          <button
            onClick={() => handleForceCheck(candidate.id)}
            disabled={isExecuting}
            className="text-xs px-3 py-1.5 text-indigo-600 hover:bg-indigo-50 rounded disabled:opacity-50"
            title="Check"
          >
            Check
          </button>
          <button
            onClick={() => handleExecuteNow(candidate.id)}
            disabled={isExecuting}
            className="text-xs px-3 py-1.5 text-green-600 hover:bg-green-50 rounded disabled:opacity-50"
            title="Execute now"
          >
            Execute
          </button>
          <button
            onClick={() => handleEdit(candidate)}
            disabled={isExecuting}
            className="text-xs px-3 py-1.5 text-blue-600 hover:bg-blue-50 rounded disabled:opacity-50"
            title="Edit"
          >
            Edit
          </button>
          {isAdmin && (
            <button
              onClick={() => handleDelete(candidate.id, candidate.suggestedAction)}
              disabled={isExecuting}
              className="text-xs px-3 py-1.5 text-red-600 hover:bg-red-50 rounded disabled:opacity-50"
              title="Delete"
            >
              Delete
            </button>
          )}
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
            {/* Tabs */}
            <div className="bg-white shadow rounded-lg mb-6">
              <div className="border-b border-gray-200">
                <nav className="flex -mb-px">
                  <button
                    onClick={() => setActiveTab('high')}
                    className={`py-4 px-6 text-sm font-medium border-b-2 ${
                      activeTab === 'high'
                        ? 'border-green-500 text-green-600'
                        : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                    }`}
                  >
                    High Probability ({highProbabilityCandidates.length})
                  </button>
                  <button
                    onClick={() => setActiveTab('low')}
                    className={`py-4 px-6 text-sm font-medium border-b-2 ${
                      activeTab === 'low'
                        ? 'border-yellow-500 text-yellow-600'
                        : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                    }`}
                  >
                    Low Probability ({lowProbabilityCandidates.length})
                  </button>
                </nav>
              </div>

              <div className="p-6">
                {activeTab === 'high' ? (
                  <>
                    <div className="mb-4">
                      <p className="text-sm text-gray-600">
                        These reminders are likely to be executed automatically when due. They have high confidence based on observed patterns.
                      </p>
                    </div>
                    {highProbabilityCandidates.length === 0 ? (
                      <div className="text-center py-8">
                        <p className="text-gray-500 text-sm">No high probability reminders</p>
                        <p className="text-gray-400 text-xs mt-1">Reminders appear here as the system learns your patterns</p>
                      </div>
                    ) : (
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        {highProbabilityCandidates.map(renderReminderCard)}
                      </div>
                    )}
                  </>
                ) : (
                  <>
                    <div className="mb-4">
                      <p className="text-sm text-gray-600">
                        These reminders are still learning. They won&apos;t execute automatically and require manual execution.
                      </p>
                    </div>
                    {lowProbabilityCandidates.length === 0 ? (
                      <div className="text-center py-8">
                        <p className="text-gray-500 text-sm">No low probability reminders</p>
                        <p className="text-gray-400 text-xs mt-1">All reminders have high confidence</p>
                      </div>
                    ) : (
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        {lowProbabilityCandidates.map(renderReminderCard)}
                      </div>
                    )}
                  </>
                )}
              </div>
            </div>

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
