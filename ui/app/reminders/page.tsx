// Reminder candidates management page with High/Low probability lists
'use client';

import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { Layout } from '@/components/Layout';
import { StatusBadge } from '@/components/StatusBadge';
import { DateTimeDisplay } from '@/components/DateTimeDisplay';
import { ConfidenceBadge } from '@/components/ConfidenceBadge';
import { EditOccurrenceModal } from '@/components/EditOccurrenceModal';
import { ReminderDetailModal } from '@/components/ReminderDetailModal';
import { LoadingSpinner } from '@/components/LoadingSpinner';
import { apiService } from '@/services/api';
import { useAuth } from '@/context/AuthContext';
import type { ReminderCandidateDto, RoutineDto, RoutineDetailDto, RoutineReminderDto } from '@/types';
import { ReminderCandidateStatus, ReminderStyle } from '@/types';
import { differenceInMinutes, differenceInDays, differenceInHours, format, isPast, isToday, isTomorrow } from 'date-fns';

const CONFIDENCE_THRESHOLD = 0.7; // High probability threshold

// Helper function to format time until execution (for reminder items)
function formatTimeUntilExecutionShort(date: string | Date): string {
  const dateObj = typeof date === 'string' ? new Date(date) : date;
  const now = new Date();
  
  if (isPast(dateObj)) {
    return 'Overdue';
  }
  
  const minutes = differenceInMinutes(dateObj, now);
  const hours = differenceInHours(dateObj, now);
  const days = differenceInDays(dateObj, now);
  
  if (days > 0) {
    const remainingHours = hours % 24;
    if (remainingHours === 0) {
      return `${days}d`;
    }
    return `${days}d ${remainingHours}h`;
  }
  
  if (hours > 0) {
    const remainingMinutes = minutes % 60;
    if (remainingMinutes === 0) {
      return `${hours}h`;
    }
    return `${hours}h ${remainingMinutes}m`;
  }
  
  return `${minutes}m`;
}

// Helper function to convert RoutineReminderDto to ReminderCandidateDto-like object for modal
function routineReminderToReminderCandidate(
  reminder: RoutineReminderDto, 
  routineDetail: RoutineDetailDto
): ReminderCandidateDto {
  return {
    id: reminder.id,
    personId: routineDetail.personId,
    suggestedAction: reminder.suggestedAction,
    checkAtUtc: reminder.lastObservedAtUtc || reminder.createdAtUtc,
    style: ReminderStyle.Suggest,
    status: ReminderCandidateStatus.Scheduled,
    confidence: reminder.confidence,
    occurrence: undefined,
    customData: reminder.customData,
    signalProfile: reminder.signalProfile,
    signalProfileUpdatedAtUtc: reminder.signalProfileUpdatedAtUtc,
    signalProfileSamplesCount: reminder.signalProfileSamplesCount,
  };
}

export default function RemindersPage() {
  const router = useRouter();
  const { user, isAdmin } = useAuth();
  const [personId, setPersonId] = useState('');
  const [actionType, setActionType] = useState('');
  const [status, setStatus] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const queryClient = useQueryClient();
  const [editingReminder, setEditingReminder] = useState<ReminderCandidateDto | null>(null);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [detailReminder, setDetailReminder] = useState<ReminderCandidateDto | null>(null);
  const [isDetailModalOpen, setIsDetailModalOpen] = useState(false);
  const [executingReminders, setExecutingReminders] = useState<Set<string>>(new Set());
  const [activeTab, setActiveTab] = useState<'high' | 'low' | 'routines'>('high');
  const [expandedRoutines, setExpandedRoutines] = useState<Set<string>>(new Set());

  // For non-admin users, set personId to their username on mount
  useEffect(() => {
    if (!isAdmin && user?.username) {
      setPersonId(user.username);
    }
  }, [isAdmin, user]);

  // Fetch personIds for admin dropdown
  const { data: personIdsData } = useQuery({
    queryKey: ['personIds'],
    queryFn: () => apiService.getPersonIds(),
    enabled: isAdmin,
  });

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
    enabled: activeTab !== 'routines', // Only fetch when not on routines tab
  });

  // Fetch routines for the Routines tab (always fetch to get count for tab label)
  const { data: routinesData, isLoading: routinesLoading } = useQuery({
    queryKey: ['routines', { personId: personId || undefined, page: 1, pageSize: 100 }],
    queryFn: () => apiService.getRoutines({ 
      personId: personId || undefined,
      page: 1, 
      pageSize: 100 
    }),
    staleTime: 5000, // Consider data fresh for 5 seconds
  });

  // Fetch routine details for all routines (with reminders) - only when on routines tab
  const { data: routineDetails, isLoading: routineDetailsLoading } = useQuery({
    queryKey: ['routineDetails', routinesData?.items.map((r: RoutineDto) => r.id) || []],
    queryFn: async () => {
      if (!routinesData?.items || routinesData.items.length === 0) return [];
      const details = await Promise.all(
        routinesData.items.map((routine: RoutineDto) => apiService.getRoutine(routine.id))
      );
      return details;
    },
    enabled: activeTab === 'routines' && !!routinesData?.items && routinesData.items.length > 0,
    staleTime: 5000, // Consider data fresh for 5 seconds
  });

  // Calculate total reminder count - fetch routine details if needed for count
  const { data: routineDetailsForCount } = useQuery({
    queryKey: ['routineDetailsForCount', routinesData?.items.map((r: RoutineDto) => r.id) || []],
    queryFn: async () => {
      if (!routinesData?.items || routinesData.items.length === 0) return [];
      const details = await Promise.all(
        routinesData.items.map((routine: RoutineDto) => apiService.getRoutine(routine.id))
      );
      return details;
    },
    enabled: !!routinesData?.items && routinesData.items.length > 0 && activeTab !== 'routines',
    staleTime: 10000, // Consider data fresh for 10 seconds (only used for count)
  });

  // Calculate total reminder count from routineDetails for the tab label
  const routinesReminderCount = (activeTab === 'routines' ? routineDetails : routineDetailsForCount)?.reduce((sum, r) => sum + (r.reminders?.length || 0), 0) || 0;

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

  const handleCardClick = (candidate: ReminderCandidateDto, e: React.MouseEvent) => {
    // Don't open modal if clicking on buttons
    const target = e.target as HTMLElement;
    if (target.closest('button')) {
      return;
    }
    setDetailReminder(candidate);
    setIsDetailModalOpen(true);
  };

  // Extract state signal conditions from customData
  const extractStateSignals = (candidate: ReminderCandidateDto): string[] => {
    if (!candidate.customData) return [];
    
    const signals: string[] = [];
    Object.entries(candidate.customData).forEach(([key, value]) => {
      // Check if it looks like a state signal
      if (key.toLowerCase().includes('state') || 
          key.toLowerCase().includes('signal') ||
          key.toLowerCase().includes('location') ||
          key.toLowerCase().includes('home') ||
          key.toLowerCase().includes('music') ||
          key.toLowerCase().includes('device')) {
        // Format as human-readable
        const name = key
          .replace(/([A-Z])/g, ' $1')
          .replace(/_/g, ' ')
          .replace(/^\w/, c => c.toUpperCase())
          .trim();
        signals.push(name);
      }
    });
    return signals;
  };

  // Format condition name for display
  const formatConditionName = (key: string): string => {
    return key
      .replace(/([A-Z])/g, ' $1')
      .replace(/_/g, ' ')
      .replace(/^\w/, c => c.toUpperCase())
      .trim();
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

  // Helper function to format intent type for display
  const getIntentDisplayName = (intentType: string): string => {
    return intentType
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, str => str.toUpperCase())
      .trim();
  };

  // Helper function to get intent icon
  const getIntentIcon = (intentType: string): string => {
    const lower = intentType.toLowerCase();
    if (lower.includes('arrival') || lower.includes('home')) return '🏠';
    if (lower.includes('sleep') || lower.includes('bed')) return '😴';
    if (lower.includes('work') || lower.includes('office')) return '💼';
    if (lower.includes('leave') || lower.includes('depart')) return '🚪';
    return '🎯';
  };

  // Toggle routine expansion
  const toggleRoutine = (routineId: string) => {
    setExpandedRoutines(prev => {
      const next = new Set(prev);
      if (next.has(routineId)) {
        next.delete(routineId);
      } else {
        next.add(routineId);
      }
      return next;
    });
  };

  const renderReminderCard = (candidate: ReminderCandidateDto) => {
    const isExecuting = executingReminders.has(candidate.id);
    const isHighProbability = (candidate.confidence || 0) >= CONFIDENCE_THRESHOLD;
    const cardBgColor = isHighProbability 
      ? 'bg-green-50 border-green-200 hover:border-green-300' 
      : 'bg-yellow-50 border-yellow-200 hover:border-yellow-300';
    
    const stateSignals = extractStateSignals(candidate);
    const maxVisibleTags = 2;
    const visibleTags = stateSignals.slice(0, maxVisibleTags);
    const hasMoreTags = stateSignals.length > maxVisibleTags;
    // Show "More..." as 3rd item if there are 3+ conditions
    const showMoreButton = stateSignals.length >= 3;
    
    return (
      <div
        key={candidate.id}
        className={`border rounded-lg p-4 transition-all ${cardBgColor} ${isExecuting ? 'ring-2 ring-indigo-500' : ''} cursor-pointer`}
        onClick={(e) => handleCardClick(candidate, e)}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            handleCardClick(candidate, e as any);
          }
        }}
        aria-label={`View details for reminder: ${candidate.suggestedAction}`}
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
            <div title={`Confidence level: ${((candidate.confidence || 0) * 100).toFixed(0)}%. ${isHighProbability ? 'High confidence - will execute automatically' : 'Low confidence - requires manual execution'}`}>
              <p className="text-xs text-gray-500 mb-1">
                Confidence
                {!isHighProbability && (
                  <span className="ml-1 text-yellow-600" title="This reminder needs more learning before it can execute automatically">
                    ⚠️
                  </span>
                )}
              </p>
              <ConfidenceBadge confidence={candidate.confidence || 0} threshold={CONFIDENCE_THRESHOLD} />
            </div>
            <div title={`Scheduled check time: ${new Date(candidate.checkAtUtc).toLocaleString()}`}>
              <p className="text-xs text-gray-500 mb-1">Time Window</p>
              <p className="text-sm text-gray-700">
                <DateTimeDisplay date={candidate.checkAtUtc} />
              </p>
            </div>
          </div>
        </div>

        {isExecuting && (
          <div className="mb-3 p-2 bg-indigo-50 border border-indigo-200 rounded text-xs text-indigo-700">
            ⏳ Executing...
          </div>
        )}

        {/* Occurrence Pattern and Condition Tags */}
        {(candidate.occurrence || stateSignals.length > 0) && (
          <div className="mb-3 space-y-2">
            {candidate.occurrence && (
              <div title={`Occurrence pattern: ${candidate.occurrence}. This defines when the reminder should be checked.`}>
                <p className="text-xs text-gray-500 mb-1">Pattern</p>
                <p className="text-sm text-gray-700">{candidate.occurrence}</p>
              </div>
            )}
            {stateSignals.length > 0 && (
              <div className="flex flex-wrap items-center gap-2">
                {visibleTags.map((signal, index) => (
                  <span
                    key={index}
                    className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-700 border border-gray-300"
                  >
                    {signal}
                  </span>
                ))}
                {showMoreButton && (
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      setDetailReminder(candidate);
                      setIsDetailModalOpen(true);
                    }}
                    className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-700 border border-gray-300 hover:bg-gray-200 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                    aria-label={`View ${stateSignals.length - maxVisibleTags} more conditions`}
                  >
                    More…
                  </button>
                )}
              </div>
            )}
          </div>
        )}

        <div className="flex items-center justify-between pt-3 border-t border-gray-200">
          {/* Time until execution - bottom left (only for high probability reminders) */}
          {isHighProbability && (
            <div className="text-xs text-gray-600 font-medium" title={`Time until execution: ${formatTimeUntilExecutionShort(candidate.checkAtUtc)}`}>
              {formatTimeUntilExecutionShort(candidate.checkAtUtc)}
            </div>
          )}
          {!isHighProbability && <div></div>}
          {/* Action buttons - bottom right */}
          <div className="flex items-center gap-2">
            <button
              onClick={(e) => {
                e.stopPropagation();
                handleForceCheck(candidate.id);
              }}
              disabled={isExecuting}
              className="text-xs px-3 py-1.5 text-indigo-600 hover:bg-indigo-50 rounded disabled:opacity-50"
              title="Check this reminder now (force evaluation)"
            >
              Check
            </button>
            <button
              onClick={(e) => {
                e.stopPropagation();
                handleExecuteNow(candidate.id);
              }}
              disabled={isExecuting}
              className="text-xs px-3 py-1.5 text-green-600 hover:bg-green-50 rounded disabled:opacity-50"
              title="Execute this reminder immediately (bypass time check)"
            >
              Execute
            </button>
            <button
              onClick={(e) => {
                e.stopPropagation();
                handleEdit(candidate);
              }}
              disabled={isExecuting}
              className="text-xs px-3 py-1.5 text-blue-600 hover:bg-blue-50 rounded disabled:opacity-50"
              title="Edit the occurrence pattern for this reminder"
            >
              Edit
            </button>
            {isAdmin && (
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  handleDelete(candidate.id, candidate.suggestedAction);
                }}
                disabled={isExecuting}
                className="text-xs px-3 py-1.5 text-red-600 hover:bg-red-50 rounded disabled:opacity-50"
                title="Delete"
              >
                Delete
              </button>
            )}
          </div>
        </div>
      </div>
    );
  };

  return (
    <Layout>
      <div className="px-4 py-6 sm:px-0">
        <div className="flex justify-between items-center mb-6">
          <div>
            <h1 className="text-3xl font-bold text-gray-900">Reminders</h1>
            <p className="text-sm text-gray-500 mt-1">
              Manage reminders organized by confidence and routines
            </p>
          </div>
          <button
            onClick={() => router.push('/reminders/create')}
            className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700 inline-flex items-center gap-2"
            title="Create a new manual reminder"
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
                <span className="ml-1 text-gray-400" title="Filter reminders by person">ℹ️</span>
              </label>
              {isAdmin ? (
                <select
                  id="personId"
                  value={personId}
                  onChange={(e) => {
                    setPersonId(e.target.value);
                    setPage(1);
                  }}
                  className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                >
                  <option value="">All Persons</option>
                  {personIdsData?.map((p) => (
                    <option key={p.personId} value={p.personId}>
                      {p.displayName} ({p.personId})
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  type="text"
                  id="personId"
                  value={personId}
                  disabled
                  className="mt-1 block w-full rounded-md border-gray-300 shadow-sm bg-gray-100 text-gray-600 sm:text-sm cursor-not-allowed"
                  title="Your personId is fixed to your username"
                />
              )}
            </div>
            <div>
              <label htmlFor="actionType" className="block text-sm font-medium text-gray-700">
                Action Type
                <span className="ml-1 text-gray-400" title="Filter by action type (e.g., play_music, turn_on_lights)">ℹ️</span>
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
                <span className="ml-1 text-gray-400" title="Filter by reminder status (Scheduled, Executed, Skipped, Expired)">ℹ️</span>
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

        {(isLoading && activeTab !== 'routines') || (routinesLoading && activeTab === 'routines') || (routineDetailsLoading && activeTab === 'routines') ? (
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
                    title="Reminders with high confidence that will execute automatically"
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
                    title="Reminders that are still learning and require manual execution"
                  >
                    Low Probability ({lowProbabilityCandidates.length})
                  </button>
                  <button
                    onClick={() => setActiveTab('routines')}
                    className={`py-4 px-6 text-sm font-medium border-b-2 ${
                      activeTab === 'routines'
                        ? 'border-blue-500 text-blue-600'
                        : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                    }`}
                    title="Reminders organized by routines (intent-triggered patterns)"
                  >
                    Routines ({routinesReminderCount})
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
                ) : activeTab === 'low' ? (
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
                ) : (
                  <>
                    <div className="mb-4">
                      <p className="text-sm text-gray-600">
                        Reminders organized by routines. Routines are patterns triggered by specific intents (like arriving home). Click on a routine to see its reminders.
                      </p>
                    </div>
                    {!routineDetails || routineDetails.length === 0 ? (
                      <div className="text-center py-8">
                        <p className="text-gray-500 text-sm">No routines found</p>
                        <p className="text-gray-400 text-xs mt-1">Routines are created automatically when you express intents</p>
                      </div>
                    ) : (
                      <div className="space-y-3">
                        {routineDetails.map((routineDetail: RoutineDetailDto) => {
                          const isExpanded = expandedRoutines.has(routineDetail.id);
                          const reminderCount = routineDetail.reminders?.length || 0;
                          
                          return (
                            <div key={routineDetail.id} className="border border-gray-200 rounded-lg overflow-hidden">
                              {/* Routine Header (Folder-like) */}
                              <button
                                onClick={() => toggleRoutine(routineDetail.id)}
                                className="w-full px-4 py-3 bg-blue-50 hover:bg-blue-100 transition-colors flex items-center justify-between text-left"
                              >
                                <div className="flex items-center gap-3 flex-1">
                                  <svg
                                    className={`w-5 h-5 text-gray-600 transition-transform ${isExpanded ? 'transform rotate-90' : ''}`}
                                    fill="none"
                                    viewBox="0 0 24 24"
                                    stroke="currentColor"
                                  >
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
                                  </svg>
                                  <span className="text-2xl">{getIntentIcon(routineDetail.intentType)}</span>
                                  <div className="flex-1">
                                    <h3 className="font-semibold text-gray-900">
                                      {getIntentDisplayName(routineDetail.intentType)}
                                    </h3>
                                    <p className="text-xs text-gray-500 mt-0.5">
                                      {reminderCount} reminder{reminderCount !== 1 ? 's' : ''} • Routine
                                    </p>
                                  </div>
                                </div>
                                <ConfidenceBadge 
                                  confidence={reminderCount > 0 
                                    ? routineDetail.reminders.reduce((sum, r) => sum + r.confidence, 0) / reminderCount
                                    : 0
                                  } 
                                  threshold={CONFIDENCE_THRESHOLD} 
                                />
                              </button>

                              {/* Routine Reminders (Collapsible Content) */}
                              {isExpanded && (
                                <div className="p-4 bg-white border-t border-gray-200">
                                  {reminderCount === 0 ? (
                                    <div className="text-center py-6 text-gray-500 text-sm">
                                      <p>No reminders learned for this routine yet</p>
                                      <p className="text-xs text-gray-400 mt-1">
                                        Reminders will appear here as the system learns your actions after this intent
                                      </p>
                                    </div>
                                  ) : (
                                    <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                                      {routineDetail.reminders.map((reminder: RoutineReminderDto) => {
                                        const convertedReminder = routineReminderToReminderCandidate(reminder, routineDetail);
                                        return (
                                          <div
                                            key={reminder.id}
                                            className="border border-gray-200 rounded-lg p-3 bg-gray-50 hover:bg-gray-100 transition-colors cursor-pointer"
                                            title={`Reminder: ${reminder.suggestedAction} (part of ${getIntentDisplayName(routineDetail.intentType)} routine). Click to view details.`}
                                            onClick={(e) => {
                                              e.stopPropagation();
                                              setDetailReminder(convertedReminder);
                                              setIsDetailModalOpen(true);
                                            }}
                                            role="button"
                                            tabIndex={0}
                                            onKeyDown={(e) => {
                                              if (e.key === 'Enter' || e.key === ' ') {
                                                e.preventDefault();
                                                setDetailReminder(convertedReminder);
                                                setIsDetailModalOpen(true);
                                              }
                                            }}
                                          >
                                            <div className="flex items-start justify-between mb-2">
                                              <div className="flex-1 min-w-0">
                                                <h4 className="font-medium text-gray-900 text-sm truncate" title={reminder.suggestedAction}>
                                                  {reminder.suggestedAction}
                                                </h4>
                                                <p className="text-xs text-gray-500 mt-0.5">
                                                  Part of: <span className="font-medium">{getIntentDisplayName(routineDetail.intentType)}</span>
                                                  <span className="ml-1 text-blue-500" title="This reminder belongs to this routine">🔗</span>
                                                </p>
                                              </div>
                                              <div title={`Confidence: ${(reminder.confidence * 100).toFixed(0)}%`}>
                                                <ConfidenceBadge 
                                                  confidence={reminder.confidence} 
                                                  threshold={CONFIDENCE_THRESHOLD}
                                                />
                                              </div>
                                            </div>
                                            {reminder.lastObservedAtUtc && (
                                              <p className="text-xs text-gray-500 mt-2" title={`Last observed: ${new Date(reminder.lastObservedAtUtc).toLocaleString()}`}>
                                                Last seen: <DateTimeDisplay date={reminder.lastObservedAtUtc} showRelative />
                                              </p>
                                            )}
                                          </div>
                                        );
                                      })}
                                    </div>
                                  )}
                                </div>
                              )}
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </>
                )}
              </div>
            </div>

            {/* Other status reminders (only Skipped and Expired, not Executed) - Only show when not on routines tab */}
            {activeTab !== 'routines' && data && data.items.filter((c: ReminderCandidateDto) => !isScheduled(c.status) && !isExecuted(c.status)).length > 0 && (
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

        {/* Reminder Detail Modal */}
        <ReminderDetailModal
          reminder={detailReminder}
          isOpen={isDetailModalOpen}
          onClose={() => {
            setIsDetailModalOpen(false);
            setDetailReminder(null);
          }}
          confidenceThreshold={CONFIDENCE_THRESHOLD}
        />
      </div>
    </Layout>
  );
}
