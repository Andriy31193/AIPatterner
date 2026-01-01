'use client';

import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiService } from '@/services/api';
import { DateTimeDisplay } from './DateTimeDisplay';
import { ConfidenceBadge } from './ConfidenceBadge';
import { StatusBadge } from './StatusBadge';
import type { ReminderCandidateDto, ActionEventListDto } from '@/types';

interface MatchingRemindersModalProps {
  event: ActionEventListDto;
  isOpen: boolean;
  onClose: () => void;
}

interface MatchingCriteria {
  matchByActionType: boolean;
  matchByDayType: boolean;
  matchByPeoplePresent: boolean;
  matchByStateSignals: boolean;
  matchByTimeBucket: boolean;
  matchByLocation: boolean;
  timeOffsetMinutes: number;
}

const DEFAULT_CRITERIA: MatchingCriteria = {
  matchByActionType: true,
  matchByDayType: true,
  matchByPeoplePresent: true,
  matchByStateSignals: true,
  matchByTimeBucket: false,
  matchByLocation: false,
  timeOffsetMinutes: 30,
};

export function MatchingRemindersModal({ event, isOpen, onClose }: MatchingRemindersModalProps) {
  const [criteria, setCriteria] = useState<MatchingCriteria>(DEFAULT_CRITERIA);
  const [showMatching, setShowMatching] = useState(false);
  const queryClient = useQueryClient();

  // Reset to related reminders when modal opens
  useEffect(() => {
    if (isOpen) {
      setShowMatching(false);
    }
  }, [isOpen]);

  // Load default criteria from Policies configuration
  const { data: policies } = useQuery({
    queryKey: ['configurations', 'MatchingPolicy'],
    queryFn: () => apiService.getConfigurations('MatchingPolicy'),
    enabled: isOpen,
  });

  useEffect(() => {
    if (policies) {
      const getConfigValue = (key: string, defaultValue: string): string => {
        const config = policies.find((c) => c.key === key);
        return config?.value || defaultValue;
      };

      setCriteria({
        matchByActionType: getConfigValue('MatchByActionType', 'true') === 'true',
        matchByDayType: getConfigValue('MatchByDayType', 'true') === 'true',
        matchByPeoplePresent: getConfigValue('MatchByPeoplePresent', 'true') === 'true',
        matchByStateSignals: getConfigValue('MatchByStateSignals', 'true') === 'true',
        matchByTimeBucket: getConfigValue('MatchByTimeBucket', 'false') === 'true',
        matchByLocation: getConfigValue('MatchByLocation', 'false') === 'true',
        timeOffsetMinutes: parseInt(getConfigValue('TimeOffsetMinutes', '30'), 10),
      });
    }
  }, [policies]);

  // Get related reminders by SourceEventId (primary query)
  const { data: relatedData, isLoading: isLoadingRelated } = useQuery({
    queryKey: ['relatedReminders', event.id],
    queryFn: () => apiService.getRelatedReminders(event.id),
    enabled: isOpen && !showMatching,
    refetchOnWindowFocus: false,
  });

  // Get matching reminders using criteria (secondary query, for matching criteria modal)
  const { data: matchingData, isLoading: isLoadingMatching, refetch } = useQuery({
    queryKey: ['matchingReminders', event.id, criteria],
    queryFn: () => apiService.getMatchingReminders(event.id, criteria),
    enabled: isOpen && showMatching, // Only fetch when explicitly requested
    refetchOnWindowFocus: false,
  });

  // Use related reminders by default, or matching reminders when criteria are applied
  const data = showMatching ? matchingData : relatedData;
  const isLoading = showMatching ? isLoadingMatching : isLoadingRelated;

  const executeNowMutation = useMutation({
    mutationFn: (candidateId: string) => apiService.executeReminderNow(candidateId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['matchingReminders'] });
      queryClient.invalidateQueries({ queryKey: ['relatedReminders', event.id] });
      queryClient.invalidateQueries({ queryKey: ['reminderCandidates'] });
    },
  });

  const handleExecuteNow = (candidateId: string) => {
    if (confirm('Execute this reminder now (bypassing date/time checks)?')) {
      executeNowMutation.mutate(candidateId);
    }
  };

  const handleResetCriteria = () => {
    setCriteria(DEFAULT_CRITERIA);
  };

  const handleApplyCriteria = () => {
    setShowMatching(true);
    refetch(); // Fetch matching reminders with criteria
  };

  const handleShowRelated = () => {
    setShowMatching(false); // Switch back to showing related reminders
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto">
      <div className="flex items-center justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
        <div className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity" onClick={onClose}></div>

        <div className="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-4xl sm:w-full">
          <div className="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
            <div className="flex justify-between items-center mb-4">
              <h3 className="text-lg leading-6 font-medium text-gray-900">
                Related Reminders for Event &quot;{event.actionType}&quot;
              </h3>
              <button
                onClick={onClose}
                className="text-gray-400 hover:text-gray-500"
              >
                <span className="sr-only">Close</span>
                <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>

            {/* Event Info */}
            <div className="mb-4 p-3 bg-gray-50 rounded-md">
              <p className="text-sm text-gray-600">
                <span className="font-medium">Person:</span> {event.personId} |{' '}
                <span className="font-medium">Action:</span> {event.actionType} |{' '}
                <span className="font-medium">Time:</span> <DateTimeDisplay date={event.timestampUtc} />
              </p>
              <p className="text-sm text-gray-600 mt-1">
                <span className="font-medium">Context:</span> {event.context.timeBucket} / {event.context.dayType}
                {event.context.location && ` | 📍 ${event.context.location}`}
              </p>
            </div>

            {/* Toggle between Related and Matching */}
            <div className="mb-4 flex gap-2">
              <button
                onClick={handleShowRelated}
                className={`px-4 py-2 rounded-md text-sm font-medium inline-flex items-center gap-2 ${
                  !showMatching
                    ? 'bg-indigo-600 text-white'
                    : 'bg-gray-200 text-gray-700 hover:bg-gray-300'
                }`}
              >
                🔗 Related Reminders
              </button>
              <button
                onClick={handleApplyCriteria}
                className={`px-4 py-2 rounded-md text-sm font-medium inline-flex items-center gap-2 ${
                  showMatching
                    ? 'bg-indigo-600 text-white'
                    : 'bg-gray-200 text-gray-700 hover:bg-gray-300'
                }`}
              >
                🔍 Find Matching
              </button>
            </div>

            {/* Matching Criteria Configuration */}
            {showMatching && (
              <div className="mb-4 p-4 border border-gray-200 rounded-md bg-gray-50">
                <div className="flex justify-between items-center mb-3">
                  <h4 className="text-sm font-medium text-gray-900">Matching Criteria</h4>
                  <button
                    onClick={handleResetCriteria}
                    className="text-sm text-indigo-600 hover:text-indigo-900 inline-flex items-center gap-1"
                  >
                    🔄 Reset to Defaults
                  </button>
                </div>
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
                <label className="flex items-center">
                  <input
                    type="checkbox"
                    checked={criteria.matchByActionType}
                    onChange={(e) => setCriteria({ ...criteria, matchByActionType: e.target.checked })}
                    className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <span className="ml-2 text-sm text-gray-700">Action Type</span>
                </label>
                <label className="flex items-center">
                  <input
                    type="checkbox"
                    checked={criteria.matchByDayType}
                    onChange={(e) => setCriteria({ ...criteria, matchByDayType: e.target.checked })}
                    className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <span className="ml-2 text-sm text-gray-700">Day Type</span>
                </label>
                <label className="flex items-center">
                  <input
                    type="checkbox"
                    checked={criteria.matchByPeoplePresent}
                    onChange={(e) => setCriteria({ ...criteria, matchByPeoplePresent: e.target.checked })}
                    className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <span className="ml-2 text-sm text-gray-700">People Present</span>
                </label>
                <label className="flex items-center">
                  <input
                    type="checkbox"
                    checked={criteria.matchByStateSignals}
                    onChange={(e) => setCriteria({ ...criteria, matchByStateSignals: e.target.checked })}
                    className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <span className="ml-2 text-sm text-gray-700">State Signals</span>
                </label>
                <label className="flex items-center">
                  <input
                    type="checkbox"
                    checked={criteria.matchByTimeBucket}
                    onChange={(e) => setCriteria({ ...criteria, matchByTimeBucket: e.target.checked })}
                    className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <span className="ml-2 text-sm text-gray-700">Time Bucket</span>
                </label>
                <label className="flex items-center">
                  <input
                    type="checkbox"
                    checked={criteria.matchByLocation}
                    onChange={(e) => setCriteria({ ...criteria, matchByLocation: e.target.checked })}
                    className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <span className="ml-2 text-sm text-gray-700">Location</span>
                </label>
              </div>
              <div className="mt-3">
                <label className="block text-sm font-medium text-gray-700">
                  Time Offset (minutes)
                </label>
                <input
                  type="number"
                  min="0"
                  value={criteria.timeOffsetMinutes}
                  onChange={(e) => setCriteria({ ...criteria, timeOffsetMinutes: parseInt(e.target.value) || 0 })}
                  className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                />
              </div>
              <button
                onClick={handleApplyCriteria}
                className="mt-3 px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700 text-sm inline-flex items-center gap-2"
              >
                ✅ Apply Criteria
              </button>
            </div>
            )}

            {/* Reminders List */}
            <div className="mb-3 text-sm text-gray-600">
              {showMatching ? (
                <span>Showing {data?.items.length || 0} matching reminder(s) based on criteria</span>
              ) : (
                <span>Showing {data?.items.length || 0} reminder(s) created from this event</span>
              )}
            </div>
            {isLoading ? (
              <div className="text-center text-gray-500 py-4">Loading reminders...</div>
            ) : data && data.items.length > 0 ? (
              <>
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-200">
                  <thead className="bg-gray-50">
                    <tr>
                      <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Person</th>
                      <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Action</th>
                      <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Confidence</th>
                      <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Check At</th>
                      <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Occurrence</th>
                      <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                      <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="bg-white divide-y divide-gray-200">
                    {data.items.map((reminder: ReminderCandidateDto) => (
                      <tr key={reminder.id}>
                        <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-900">{reminder.personId}</td>
                        <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">{reminder.suggestedAction}</td>
                        <td className="px-3 py-2 whitespace-nowrap text-sm">
                          <ConfidenceBadge confidence={reminder.confidence || 0} threshold={0.7} />
                        </td>
                        <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">
                          <DateTimeDisplay date={reminder.checkAtUtc} />
                        </td>
                        <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">
                          {reminder.occurrence || <span className="text-gray-400">N/A</span>}
                        </td>
                        <td className="px-3 py-2 whitespace-nowrap">
                          <StatusBadge status={reminder.status} />
                        </td>
                        <td className="px-3 py-2 whitespace-nowrap text-sm font-medium">
                            <button
                              onClick={() => handleExecuteNow(reminder.id)}
                              className="text-green-600 hover:text-green-900 text-lg"
                              title="Execute now"
                            >
                              ▶️
                            </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              </>
            ) : (
              <div className="text-center text-gray-500 py-4">
                {showMatching ? 'No matching reminders found with the selected criteria.' : 'No reminders found for this event.'}
              </div>
            )}
          </div>
          <div className="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
            <button
              type="button"
              onClick={onClose}
              className="w-full inline-flex justify-center items-center gap-2 rounded-md border border-transparent shadow-sm px-4 py-2 bg-indigo-600 text-base font-medium text-white hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:ml-3 sm:w-auto sm:text-sm"
            >
              ❌ Close
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

