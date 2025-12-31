// Dashboard page showing overview of events, candidates, and transitions
'use client';

import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Layout } from '@/components/Layout';
import { StatusBadge } from '@/components/StatusBadge';
import { ConfidenceBadge } from '@/components/ConfidenceBadge';
import { DateTimeDisplay } from '@/components/DateTimeDisplay';
import { apiService } from '@/services/api';
import type { ReminderCandidateDto, TransitionDto } from '@/types';

export default function DashboardPage() {
  const [selectedPersonId, setSelectedPersonId] = useState<string>('');

  const { data: candidates, isLoading: candidatesLoading } = useQuery({
    queryKey: ['reminderCandidates', { page: 1, pageSize: 10 }],
    queryFn: () => apiService.getReminderCandidates({ page: 1, pageSize: 10 }),
  });

  const { data: transitions, isLoading: transitionsLoading } = useQuery({
    queryKey: ['transitions', selectedPersonId],
    queryFn: () => apiService.getTransitions(selectedPersonId),
    enabled: !!selectedPersonId,
  });

  return (
    <Layout>
      <div className="px-4 py-6 sm:px-0">
        <h1 className="text-3xl font-bold text-gray-900 mb-6">Dashboard</h1>

        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
          {/* Reminder Candidates */}
          <div className="bg-white shadow rounded-lg">
            <div className="px-4 py-5 sm:p-6">
              <h2 className="text-lg font-medium text-gray-900 mb-4">Recent Reminder Candidates</h2>
              {candidatesLoading ? (
                <div className="text-sm text-gray-500">Loading...</div>
              ) : (
                <div className="overflow-hidden">
                  <table className="min-w-full divide-y divide-gray-200">
                    <thead className="bg-gray-50">
                      <tr>
                        <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Person</th>
                        <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Action</th>
                        <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                        <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Check At</th>
                      </tr>
                    </thead>
                    <tbody className="bg-white divide-y divide-gray-200">
                      {candidates?.items.map((candidate: ReminderCandidateDto) => (
                        <tr key={candidate.id}>
                          <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-900">{candidate.personId}</td>
                          <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">{candidate.suggestedAction}</td>
                          <td className="px-3 py-2 whitespace-nowrap">
                            <StatusBadge status={candidate.status} />
                          </td>
                          <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">
                            <DateTimeDisplay date={candidate.checkAtUtc} showRelative />
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>

          {/* Transitions */}
          <div className="bg-white shadow rounded-lg">
            <div className="px-4 py-5 sm:p-6">
              <h2 className="text-lg font-medium text-gray-900 mb-4">Learned Transitions</h2>
              <div className="mb-4">
                <input
                  type="text"
                  placeholder="Enter person ID to view transitions"
                  value={selectedPersonId}
                  onChange={(e) => setSelectedPersonId(e.target.value)}
                  className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                />
              </div>
              {transitionsLoading ? (
                <div className="text-sm text-gray-500">Loading...</div>
              ) : transitions?.transitions.length ? (
                <div className="overflow-hidden">
                  <table className="min-w-full divide-y divide-gray-200">
                    <thead className="bg-gray-50">
                      <tr>
                        <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">From → To</th>
                        <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Confidence</th>
                        <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Count</th>
                      </tr>
                    </thead>
                    <tbody className="bg-white divide-y divide-gray-200">
                      {transitions.transitions.map((transition: TransitionDto) => (
                        <tr key={transition.id}>
                          <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-900">
                            {transition.fromAction} → {transition.toAction}
                          </td>
                          <td className="px-3 py-2 whitespace-nowrap">
                            <ConfidenceBadge confidence={transition.confidencePercent / 100} />
                          </td>
                          <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">{transition.occurrenceCount}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : (
                <div className="text-sm text-gray-500">Enter a person ID to view transitions</div>
              )}
            </div>
          </div>
        </div>
      </div>
    </Layout>
  );
}

