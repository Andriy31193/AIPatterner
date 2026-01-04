// Routines page - card-based list view and detail view
'use client';

import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { Layout } from '@/components/Layout';
import { ConfidenceIndicator } from '@/components/ConfidenceIndicator';
import { LearningBadge } from '@/components/LearningBadge';
import { DateTimeDisplay } from '@/components/DateTimeDisplay';
import { apiService } from '@/services/api';
import { useAuth } from '@/context/AuthContext';
import type { RoutineDto, RoutineDetailDto, RoutineReminderDto, ReminderCandidateDto } from '@/types';
import { ProbabilityAction, ReminderStyle, ReminderCandidateStatus } from '@/types';
import { ReminderDetailModal } from '@/components/ReminderDetailModal';

export default function RoutinesPage() {
  const router = useRouter();
  const { user, isAdmin } = useAuth();
  const [selectedRoutineId, setSelectedRoutineId] = useState<string | null>(null);
  const [personId, setPersonId] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [detailReminder, setDetailReminder] = useState<ReminderCandidateDto | null>(null);
  const [isDetailModalOpen, setIsDetailModalOpen] = useState(false);
  const [isEditingWindow, setIsEditingWindow] = useState(false);
  const [editingWindowMinutes, setEditingWindowMinutes] = useState<number>(60);

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

  const { data: routinesData, isLoading } = useQuery({
    queryKey: ['routines', { personId, page, pageSize }],
    queryFn: () => apiService.getRoutines({ 
      personId: personId || undefined,
      page, 
      pageSize 
    }),
  });

  const queryClient = useQueryClient();

  const { data: routineDetail } = useQuery({
    queryKey: ['routine', selectedRoutineId],
    queryFn: () => apiService.getRoutine(selectedRoutineId!),
    enabled: !!selectedRoutineId,
    refetchInterval: 5000, // Refetch every 5 seconds to show live updates
  });

  const feedbackMutation = useMutation({
    mutationFn: ({ reminderId, action, value }: { reminderId: string; action: ProbabilityAction; value: number }) =>
      apiService.submitRoutineReminderFeedback(selectedRoutineId!, reminderId, action, value),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['routine', selectedRoutineId] });
    },
  });

  const updateRoutineMutation = useMutation({
    mutationFn: (observationWindowMinutes: number) =>
      apiService.updateRoutine(selectedRoutineId!, { observationWindowMinutes }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['routine', selectedRoutineId] });
      queryClient.invalidateQueries({ queryKey: ['routines'] });
      setIsEditingWindow(false);
    },
  });

  const handleFeedback = (reminderId: string, action: 'accept' | 'reject') => {
    const actionType = action === 'accept' ? ProbabilityAction.Increase : ProbabilityAction.Decrease;
    const value = 0.1; // Default step value
    feedbackMutation.mutate({ reminderId, action: actionType, value });
  };

  const getIntentDisplayName = (intentType: string): string => {
    // Convert camelCase/PascalCase to readable format
    return intentType
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, str => str.toUpperCase())
      .trim();
  };

  const getIntentIcon = (intentType: string): string => {
    const lower = intentType.toLowerCase();
    if (lower.includes('arrival') || lower.includes('home')) return '🏠';
    if (lower.includes('sleep') || lower.includes('bed')) return '😴';
    if (lower.includes('work') || lower.includes('office')) return '💼';
    if (lower.includes('leave') || lower.includes('depart')) return '🚪';
    return '🎯';
  };

  const isWindowOpen = (routine: RoutineDto): boolean => {
    if (!routine.observationWindowEndsUtc) return false;
    return new Date(routine.observationWindowEndsUtc) > new Date();
  };

  // Helper function to convert RoutineReminderDto to ReminderCandidateDto-like object for modal
  const routineReminderToReminderCandidate = (
    reminder: RoutineReminderDto, 
    routineDetail: RoutineDetailDto
  ): ReminderCandidateDto => {
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
  };

  if (selectedRoutineId && routineDetail) {
    return (
      <Layout>
        <div className="px-4 py-6 sm:px-0">
          <button
            onClick={() => setSelectedRoutineId(null)}
            className="mb-4 text-sm text-indigo-600 hover:text-indigo-900 flex items-center gap-2"
          >
            ← Back to Routines
          </button>

          {/* Routine Detail View */}
          <div className="bg-white shadow rounded-lg p-6 mb-6">
            {/* Top Section */}
            <div className="flex items-start justify-between mb-6">
              <div className="flex-1">
                <div className="flex items-center gap-3 mb-2">
                  <span className="text-4xl">{getIntentIcon(routineDetail.intentType)}</span>
                  <div>
                    <h1 className="text-2xl font-semibold text-gray-900">
                      When you {getIntentDisplayName(routineDetail.intentType).toLowerCase()}
                    </h1>
                    <p className="text-sm text-gray-500 mt-1">
                      This routine activates when you say &quot;{getIntentDisplayName(routineDetail.intentType)}&quot;
                    </p>
                  </div>
                </div>
                <div className="flex items-center gap-4 mt-4">
                  <ConfidenceIndicator 
                    confidence={routineDetail.reminders.length > 0 
                      ? routineDetail.reminders.reduce((sum, r) => sum + r.confidence, 0) / routineDetail.reminders.length
                      : 0
                    }
                    showLabel={true}
                    size="md"
                  />
                  {isWindowOpen(routineDetail) && (
                    <LearningBadge status="active" />
                  )}
                  {routineDetail.lastActivatedUtc && (
                    <div className="text-sm text-gray-500">
                      Last activated: <DateTimeDisplay date={routineDetail.lastActivatedUtc} showRelative />
                    </div>
                  )}
                </div>
              </div>
            </div>

            {/* Observation Window Settings */}
            <div className="mt-6 p-4 bg-gray-50 rounded-lg border border-gray-200">
              <div className="flex items-center justify-between">
                <div className="flex-1">
                  <h3 className="text-sm font-medium text-gray-900 mb-1">
                    Learning Window
                    <span className="ml-1 text-gray-400" title="How long the routine observes actions after activation">ℹ️</span>
                  </h3>
                  {!isEditingWindow ? (
                    <div className="flex items-center gap-2">
                      <p className="text-sm text-gray-600">
                        {routineDetail.observationWindowMinutes} minutes
                      </p>
                      <button
                        onClick={() => {
                          setEditingWindowMinutes(routineDetail.observationWindowMinutes);
                          setIsEditingWindow(true);
                        }}
                        className="text-xs text-indigo-600 hover:text-indigo-900 font-medium"
                      >
                        Edit
                      </button>
                    </div>
                  ) : (
                    <div className="flex items-center gap-2 mt-2">
                      <input
                        type="number"
                        min="1"
                        max="1440"
                        value={editingWindowMinutes}
                        onChange={(e) => setEditingWindowMinutes(parseInt(e.target.value) || 1)}
                        className="w-24 px-2 py-1 text-sm border border-gray-300 rounded-md focus:ring-indigo-500 focus:border-indigo-500"
                      />
                      <span className="text-sm text-gray-600">minutes</span>
                      <button
                        onClick={() => updateRoutineMutation.mutate(editingWindowMinutes)}
                        disabled={updateRoutineMutation.isPending}
                        className="px-3 py-1 text-xs bg-indigo-600 text-white rounded-md hover:bg-indigo-700 disabled:opacity-50"
                      >
                        {updateRoutineMutation.isPending ? 'Saving...' : 'Save'}
                      </button>
                      <button
                        onClick={() => {
                          setIsEditingWindow(false);
                          setEditingWindowMinutes(routineDetail.observationWindowMinutes);
                        }}
                        disabled={updateRoutineMutation.isPending}
                        className="px-3 py-1 text-xs bg-gray-200 text-gray-700 rounded-md hover:bg-gray-300 disabled:opacity-50"
                      >
                        Cancel
                      </button>
                    </div>
                  )}
                  <p className="text-xs text-gray-500 mt-1">
                    Actions observed within this window after activation will be associated with this routine
                  </p>
                </div>
              </div>
            </div>

            {/* Actions Section */}
            <div className="mt-8">
              <h2 className="text-lg font-medium text-gray-900 mb-4">
                Learned Actions ({routineDetail.reminders.length})
              </h2>
              {routineDetail.reminders.length === 0 ? (
                <div className="bg-blue-50 border border-blue-200 rounded-lg p-6 text-center">
                  <p className="text-blue-800 font-medium mb-2">Still learning</p>
                  <p className="text-sm text-blue-600">
                    This routine hasn&apos;t learned any actions yet. Actions will appear here as you use them after this intent.
                  </p>
                </div>
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {routineDetail.reminders.map((reminder: RoutineReminderDto) => {
                    const convertedReminder = routineReminderToReminderCandidate(reminder, routineDetail);
                    return (
                      <div
                        key={reminder.id}
                        className="bg-gray-50 border border-gray-200 rounded-lg p-4 hover:shadow-md transition-shadow cursor-pointer"
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
                        title={`Click to view details for: ${reminder.suggestedAction}`}
                      >
                        <div className="flex items-start justify-between mb-3">
                      <div className="flex-1">
                        <h3 className="font-medium text-gray-900 mb-1" title={`Action: ${reminder.suggestedAction}`}>
                          {reminder.suggestedAction}
                        </h3>
                        {reminder.lastObservedAtUtc && (
                          <p className="text-xs text-gray-500" title={`Last observed: ${new Date(reminder.lastObservedAtUtc).toLocaleString()}`}>
                            Last seen: <DateTimeDisplay date={reminder.lastObservedAtUtc} showRelative />
                          </p>
                        )}
                      </div>
                      <div title={`Confidence: ${(reminder.confidence * 100).toFixed(0)}%`}>
                        <ConfidenceIndicator 
                          confidence={reminder.confidence} 
                          size="sm" 
                          showLabel={false}
                        />
                      </div>
                        </div>
                      <div className="mt-3 pt-3 border-t border-gray-200">
                        <div className="flex items-center justify-between text-xs mb-2">
                          <span className="text-gray-600">
                            {reminder.confidence >= 0.7 ? 'Auto' : reminder.confidence >= 0.4 ? 'Ask' : 'Suggest'}
                          </span>
                          <span className="text-gray-500">
                            {reminder.confidence >= 0.7 
                              ? 'Will execute automatically'
                              : reminder.confidence >= 0.4
                              ? 'Will ask before executing'
                              : 'Still learning when to suggest'
                            }
                          </span>
                        </div>
                        {/* Feedback Controls - Only show if routine has active learning window */}
                        {routineDetail.observationWindowEndsUtc && 
                         new Date(routineDetail.observationWindowEndsUtc) > new Date() && (
                          <div className="flex gap-2 mt-2">
                            <button
                              onClick={() => handleFeedback(reminder.id, 'accept')}
                              disabled={feedbackMutation.isPending}
                              className="flex-1 px-2 py-1 text-xs bg-green-100 text-green-700 rounded hover:bg-green-200 disabled:opacity-50"
                              title="This action is good for this routine"
                            >
                              ✓ Good
                            </button>
                            <button
                              onClick={() => handleFeedback(reminder.id, 'reject')}
                              disabled={feedbackMutation.isPending}
                              className="flex-1 px-2 py-1 text-xs bg-red-100 text-red-700 rounded hover:bg-red-200 disabled:opacity-50"
                              title="This action is not part of this routine"
                            >
                              ✗ Not This
                            </button>
                          </div>
                        )}
                      </div>
                    </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>

          {/* Reminder Detail Modal */}
          <ReminderDetailModal
            reminder={detailReminder}
            isOpen={isDetailModalOpen}
            onClose={() => {
              setIsDetailModalOpen(false);
              setDetailReminder(null);
            }}
            confidenceThreshold={0.7}
          />
        </div>
      </Layout>
    );
  }

  return (
    <Layout>
      <div className="px-4 py-6 sm:px-0">
        <div className="flex justify-between items-center mb-6">
          <div>
            <h1 className="text-3xl font-bold text-gray-900">Routines</h1>
            <p className="text-sm text-gray-500 mt-1">
              Intent-anchored learned routines that activate when you express specific intents
            </p>
          </div>
        </div>

        {/* Filter */}
        <div className="bg-white shadow rounded-lg mb-6 p-4">
          <div className="flex gap-4">
            <div className="flex-1">
              <label htmlFor="personId" className="block text-sm font-medium text-gray-700 mb-1">
                Person ID
                <span className="ml-1 text-gray-400" title="Filter routines by person">ℹ️</span>
              </label>
              {isAdmin ? (
                <select
                  id="personId"
                  value={personId}
                  onChange={(e) => {
                    setPersonId(e.target.value);
                    setPage(1);
                  }}
                  className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
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
                  className="block w-full rounded-md border-gray-300 shadow-sm bg-gray-100 text-gray-600 sm:text-sm cursor-not-allowed"
                  title="Your personId is fixed to your username"
                />
              )}
            </div>
          </div>
        </div>

        {isLoading ? (
          <div className="bg-white shadow rounded-lg p-6 text-center text-gray-500">Loading routines...</div>
        ) : routinesData && routinesData.items.length > 0 ? (
          <>
            {/* Routine Cards */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {routinesData.items.map((routine: RoutineDto) => {
                const avgConfidence = 0; // Would need to fetch reminders to calculate
                return (
                    <div
                      key={routine.id}
                      onClick={() => setSelectedRoutineId(routine.id)}
                      className="bg-white border border-gray-200 rounded-lg p-6 hover:shadow-lg transition-all cursor-pointer"
                      title={`Click to view details for ${getIntentDisplayName(routine.intentType)} routine`}
                    >
                    <div className="flex items-start justify-between mb-4">
                      <div className="flex items-center gap-3">
                        <span className="text-3xl">{getIntentIcon(routine.intentType)}</span>
                        <div>
                          <h3 className="font-semibold text-gray-900">
                            {getIntentDisplayName(routine.intentType)}
                          </h3>
                          <p className="text-xs text-gray-500 mt-1">
                            {routine.lastActivatedUtc 
                              ? `Activated ${new Date(routine.lastActivatedUtc).toLocaleDateString()}`
                              : 'Not yet activated'
                            }
                          </p>
                        </div>
                      </div>
                      {isWindowOpen(routine) && (
                        <LearningBadge status="active" />
                      )}
                    </div>
                    <div className="mt-4 pt-4 border-t border-gray-200">
                      <div className="flex items-center justify-between text-sm">
                        <span className="text-gray-600">Learning status</span>
                        <LearningBadge status={routine.lastActivatedUtc ? 'ready' : 'learning'} />
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Pagination */}
            {routinesData.totalCount > pageSize && (
              <div className="bg-white px-4 py-3 flex items-center justify-between border-t border-gray-200 sm:px-6 rounded-lg shadow mt-6">
                <div className="text-sm text-gray-700">
                  Showing <span className="font-medium">{(page - 1) * pageSize + 1}</span> to{' '}
                  <span className="font-medium">{Math.min(page * pageSize, routinesData.totalCount)}</span> of{' '}
                  <span className="font-medium">{routinesData.totalCount}</span> routines
                </div>
                <div className="flex gap-2">
                  <button
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page === 1}
                    className="px-4 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 disabled:opacity-50"
                  >
                    Previous
                  </button>
                  <button
                    onClick={() => setPage((p) => p + 1)}
                    disabled={page * pageSize >= routinesData.totalCount}
                    className="px-4 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 disabled:opacity-50"
                  >
                    Next
                  </button>
                </div>
              </div>
            )}
          </>
        ) : (
          <div className="bg-white shadow rounded-lg p-12 text-center">
            <p className="text-gray-500 mb-2">No routines found</p>
            <p className="text-sm text-gray-400">
              Routines are created automatically when you send StateChange events (intents)
            </p>
          </div>
        )}
      </div>
    </Layout>
  );
}

