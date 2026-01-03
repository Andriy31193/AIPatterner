// Event listing page with pagination, filtering, and search
'use client';

import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { Layout } from '@/components/Layout';
import { DateTimeDisplay } from '@/components/DateTimeDisplay';
import { ConfidenceBadge } from '@/components/ConfidenceBadge';
import { MatchingRemindersModal } from '@/components/MatchingRemindersModal';
import { apiService } from '@/services/api';
import type { ActionEventListDto } from '@/types';
import { ProbabilityAction } from '@/types';

const CONFIDENCE_THRESHOLD = 0.7;

export default function EventsPage() {
  const router = useRouter();
  const [personId, setPersonId] = useState('');
  const [actionType, setActionType] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [selectedEvent, setSelectedEvent] = useState<ActionEventListDto | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);

  const { data, isLoading } = useQuery({
    queryKey: ['events', { personId, actionType, fromDate, toDate, page, pageSize }],
    queryFn: () => apiService.getEvents({
      personId: personId || undefined,
      actionType: actionType || undefined,
      fromUtc: fromDate ? new Date(fromDate).toISOString() : undefined,
      toUtc: toDate ? new Date(toDate).toISOString() : undefined,
      page,
      pageSize,
    }),
  });

  const getProbabilityActionLabel = (action?: ProbabilityAction): string => {
    if (!action) return 'N/A';
    return action === ProbabilityAction.Increase ? 'Increase' : 'Decrease';
  };

  const getProbabilityActionColor = (action?: ProbabilityAction): string => {
    if (!action) return 'text-gray-500';
    return action === ProbabilityAction.Increase ? 'text-green-600' : 'text-red-600';
  };

  return (
    <Layout>
      <div className="px-4 py-6 sm:px-0">
        <div className="flex justify-between items-center mb-6">
          <h1 className="text-3xl font-bold text-gray-900">Events</h1>
          <button
            onClick={() => router.push('/events/create')}
            className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700"
          >
            Create New Event
          </button>
        </div>

        {/* Filters */}
        <div className="bg-white shadow rounded-lg mb-6 p-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-5">
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
              <label htmlFor="fromDate" className="block text-sm font-medium text-gray-700">
                From Date
              </label>
              <input
                type="date"
                id="fromDate"
                value={fromDate}
                onChange={(e) => setFromDate(e.target.value)}
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
              />
            </div>
            <div>
              <label htmlFor="toDate" className="block text-sm font-medium text-gray-700">
                To Date
              </label>
              <input
                type="date"
                id="toDate"
                value={toDate}
                onChange={(e) => setToDate(e.target.value)}
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
              />
            </div>
            <div className="flex items-end">
              <button
                onClick={() => {
                  setPersonId('');
                  setActionType('');
                  setFromDate('');
                  setToDate('');
                  setPage(1);
                }}
                className="w-full px-4 py-2 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50"
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
            {/* Events Timeline */}
            <div className="bg-white shadow rounded-lg overflow-hidden mb-6">
              <div className="px-4 py-5 sm:p-6">
                <h2 className="text-lg font-medium text-gray-900 mb-4">Event Timeline</h2>
                {data && data.items.length === 0 ? (
                  <div className="text-center py-8">
                    <p className="text-sm text-gray-500">No events found.</p>
                    <p className="text-xs text-gray-400 mt-1">Events will appear here as they occur</p>
                  </div>
                ) : (
                  <div className="space-y-4">
                    {data?.items.map((event: ActionEventListDto) => {
                      const isStateChange = (event as any).eventType === 'StateChange';
                      const learningNote = event.probabilityAction 
                        ? (event.probabilityAction === ProbabilityAction.Increase 
                            ? 'This increased the confidence of related reminders'
                            : 'This decreased the confidence of related reminders')
                        : isStateChange
                        ? 'This activated a routine and opened an observation window'
                        : 'This event was recorded for pattern learning';
                      
                      return (
                        <div
                          key={event.id}
                          className="flex gap-4 p-4 border-l-4 border-gray-200 hover:border-indigo-400 bg-gray-50 hover:bg-gray-100 rounded-r-lg transition-colors"
                        >
                          <div className="flex-shrink-0">
                            <div className="w-10 h-10 rounded-full bg-indigo-100 flex items-center justify-center">
                              {isStateChange ? '🎯' : '⚡'}
                            </div>
                          </div>
                          <div className="flex-1 min-w-0">
                            <div className="flex items-start justify-between mb-2">
                              <div>
                                <h3 className="font-medium text-gray-900">{event.actionType}</h3>
                                <p className="text-xs text-gray-500 mt-1">
                                  {event.personId} • <DateTimeDisplay date={event.timestampUtc} showRelative />
                                </p>
                              </div>
                              {event.probabilityValue !== null && event.probabilityValue !== undefined && (
                                <ConfidenceBadge confidence={event.probabilityValue} threshold={CONFIDENCE_THRESHOLD} />
                              )}
                            </div>
                            <div className="mt-2 text-sm text-gray-600">
                              <p>{learningNote}</p>
                            </div>
                            <div className="mt-2 flex items-center gap-4 text-xs text-gray-500">
                              <span>{event.context.timeBucket}</span>
                              <span>{event.context.dayType}</span>
                              {event.context.location && <span>📍 {event.context.location}</span>}
                            </div>
                            {event.relatedReminderId && (
                              <div className="mt-2">
                                <button
                                  onClick={() => {
                                    setSelectedEvent(event);
                                    setIsModalOpen(true);
                                  }}
                                  className="text-xs text-indigo-600 hover:text-indigo-900"
                                >
                                  View related reminders →
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

        {/* Matching Reminders Modal */}
        {selectedEvent && (
          <MatchingRemindersModal
            event={selectedEvent}
            isOpen={isModalOpen}
            onClose={() => {
              setIsModalOpen(false);
              setSelectedEvent(null);
            }}
          />
        )}
      </div>
    </Layout>
  );
}
