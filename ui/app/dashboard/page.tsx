// Dashboard page - overview with calm, non-technical language
'use client';

import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Layout } from '@/components/Layout';
import { ConfidenceIndicator } from '@/components/ConfidenceIndicator';
import { LearningBadge } from '@/components/LearningBadge';
import { DateTimeDisplay } from '@/components/DateTimeDisplay';
import { apiService } from '@/services/api';
import type { ReminderCandidateDto, RoutineDto } from '@/types';
import Link from 'next/link';

const CONFIDENCE_THRESHOLD = 0.7;

export default function DashboardPage() {
  const [selectedPersonId, setSelectedPersonId] = useState<string>('');

  // Get today's active reminders (high probability only)
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const tomorrow = new Date(today);
  tomorrow.setDate(tomorrow.getDate() + 1);

  const { data: candidates, isLoading: candidatesLoading } = useQuery({
    queryKey: ['reminderCandidates', { page: 1, pageSize: 10, status: 'Scheduled' }],
    queryFn: () => apiService.getReminderCandidates({ 
      page: 1, 
      pageSize: 10,
      status: 'Scheduled',
    }),
  });

  const { data: routinesData } = useQuery({
    queryKey: ['routines', { page: 1, pageSize: 5 }],
    queryFn: () => apiService.getRoutines({ page: 1, pageSize: 5 }),
  });

  // Filter high probability reminders
  const activeReminders = candidates?.items.filter(
    (c: ReminderCandidateDto) => (c.confidence || 0) >= CONFIDENCE_THRESHOLD
  ) || [];

  // Get active routines (with open observation windows or recently activated)
  const activeRoutines = routinesData?.items.filter((r: RoutineDto) => {
    if (!r.observationWindowEndsUtc) return false;
    return new Date(r.observationWindowEndsUtc) > new Date();
  }) || [];

  return (
    <Layout>
      <div className="px-4 py-6 sm:px-0">
        <div className="mb-6">
          <h1 className="text-3xl font-bold text-gray-900">Dashboard</h1>
          <p className="text-sm text-gray-500 mt-1">
            What will the assistant likely do today?
          </p>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Today's Active Reminders */}
          <div className="bg-white shadow rounded-lg p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold text-gray-900">Today&apos;s Active Reminders</h2>
              <Link href="/reminders" className="text-sm text-indigo-600 hover:text-indigo-900">
                View all →
              </Link>
            </div>
            {candidatesLoading ? (
              <div className="text-sm text-gray-500 py-4">Loading...</div>
            ) : activeReminders.length > 0 ? (
              <div className="space-y-3">
                {activeReminders.slice(0, 5).map((reminder: ReminderCandidateDto) => (
                  <div
                    key={reminder.id}
                    className="flex items-center justify-between p-3 bg-green-50 border border-green-200 rounded-lg"
                  >
                    <div className="flex-1">
                      <p className="font-medium text-gray-900">{reminder.suggestedAction}</p>
                      <p className="text-xs text-gray-500 mt-1">
                        <DateTimeDisplay date={reminder.checkAtUtc} showRelative />
                      </p>
                    </div>
                    <ConfidenceIndicator confidence={reminder.confidence || 0} size="sm" showLabel={false} />
                  </div>
                ))}
              </div>
            ) : (
              <div className="text-center py-8">
                <p className="text-gray-500 text-sm">No active reminders for today</p>
                <p className="text-gray-400 text-xs mt-1">The assistant is waiting to learn your patterns</p>
              </div>
            )}
          </div>

          {/* Active Routines */}
          <div className="bg-white shadow rounded-lg p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold text-gray-900">Active Routines</h2>
              <Link href="/routines" className="text-sm text-indigo-600 hover:text-indigo-900">
                View all →
              </Link>
            </div>
            {activeRoutines.length > 0 ? (
              <div className="space-y-3">
                {activeRoutines.map((routine: RoutineDto) => (
                  <div
                    key={routine.id}
                    className="flex items-center justify-between p-3 bg-blue-50 border border-blue-200 rounded-lg"
                  >
                    <div className="flex items-center gap-3 flex-1">
                      <span className="text-2xl">
                        {routine.intentType.toLowerCase().includes('arrival') || routine.intentType.toLowerCase().includes('home') 
                          ? '🏠' 
                          : routine.intentType.toLowerCase().includes('sleep') 
                          ? '😴' 
                          : '🎯'}
                      </span>
                      <div className="flex-1">
                        <p className="font-medium text-gray-900">
                          {routine.intentType.replace(/([A-Z])/g, ' $1').trim()}
                        </p>
                        <p className="text-xs text-gray-500 mt-1">
                          Learning your routine...
                        </p>
                      </div>
                    </div>
                    <LearningBadge status="active" />
                  </div>
                ))}
              </div>
            ) : (
              <div className="text-center py-8">
                <p className="text-gray-500 text-sm">No active routines</p>
                <p className="text-gray-400 text-xs mt-1">Routines activate when you express intents</p>
              </div>
            )}
          </div>
        </div>

        {/* Recent Learning Updates */}
        <div className="mt-6 bg-white shadow rounded-lg p-6">
          <h2 className="text-lg font-semibold text-gray-900 mb-4">Recent Learning Updates</h2>
          <div className="space-y-2">
            {activeReminders.length > 0 && (
              <div className="flex items-center gap-3 p-3 bg-gray-50 rounded-lg">
                <span className="text-lg">📚</span>
                <div className="flex-1">
                  <p className="text-sm text-gray-900">
                    Learning your reminder patterns
                  </p>
                  <p className="text-xs text-gray-500 mt-1">
                    {activeReminders.length} reminder{activeReminders.length !== 1 ? 's' : ''} ready to execute
                  </p>
                </div>
              </div>
            )}
            {activeRoutines.length > 0 && (
              <div className="flex items-center gap-3 p-3 bg-gray-50 rounded-lg">
                <span className="text-lg">🌱</span>
                <div className="flex-1">
                  <p className="text-sm text-gray-900">
                    Learning your arrival routine
                  </p>
                  <p className="text-xs text-gray-500 mt-1">
                    Observing actions after you arrive home
                  </p>
                </div>
              </div>
            )}
            {activeReminders.length === 0 && activeRoutines.length === 0 && (
              <div className="text-center py-8">
                <p className="text-gray-500 text-sm">No recent learning updates</p>
                <p className="text-gray-400 text-xs mt-1">The system is waiting for events to learn from</p>
              </div>
            )}
          </div>
        </div>
      </div>
    </Layout>
  );
}
