// Dashboard page - overview with calm, non-technical language
'use client';

import React, { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Layout } from '@/components/Layout';
import { ConfidenceIndicator } from '@/components/ConfidenceIndicator';
import { LearningBadge } from '@/components/LearningBadge';
import { DateTimeDisplay } from '@/components/DateTimeDisplay';
import { apiService } from '@/services/api';
import { useAuth } from '@/context/AuthContext';
import type { ReminderCandidateDto, RoutineDto } from '@/types';
import Link from 'next/link';
import { formatDistanceToNow, format, isPast, isToday, isTomorrow, differenceInMinutes, differenceInHours, differenceInDays } from 'date-fns';

const CONFIDENCE_THRESHOLD = 0.7;

// Helper function to format time until execution (shortened format)
function formatTimeUntilExecution(date: string | Date): string {
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

export default function DashboardPage() {
  const { user, isAdmin } = useAuth();
  const [selectedPersonId, setSelectedPersonId] = useState<string>('');
  const [currentTime, setCurrentTime] = useState(new Date());

  // For non-admin users, set personId to their username on mount
  useEffect(() => {
    if (!isAdmin && user?.username) {
      setSelectedPersonId(user.username);
    }
  }, [isAdmin, user]);

  // Update current time every minute for accurate time-before-execution display
  useEffect(() => {
    const interval = setInterval(() => {
      setCurrentTime(new Date());
    }, 60000); // Update every minute
    return () => clearInterval(interval);
  }, []);

  // Fetch personIds for admin dropdown
  const { data: personIdsData } = useQuery({
    queryKey: ['personIds'],
    queryFn: () => apiService.getPersonIds(),
    enabled: isAdmin,
  });

  // Get today's active reminders (high probability only)
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const tomorrow = new Date(today);
  tomorrow.setDate(tomorrow.getDate() + 1);

  const { data: candidates, isLoading: candidatesLoading } = useQuery({
    queryKey: ['reminderCandidates', { page: 1, pageSize: 10, status: 'Scheduled', personId: selectedPersonId || undefined }],
    queryFn: () => apiService.getReminderCandidates({ 
      page: 1, 
      pageSize: 10,
      status: 'Scheduled',
      personId: selectedPersonId || undefined,
    }),
  });

  const { data: routinesData } = useQuery({
    queryKey: ['routines', { page: 1, pageSize: 5, personId: selectedPersonId || undefined }],
    queryFn: () => apiService.getRoutines({ 
      page: 1, 
      pageSize: 5,
      personId: selectedPersonId || undefined,
    }),
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

        {/* Person ID Filter */}
        {isAdmin && (
          <div className="bg-white shadow rounded-lg mb-6 p-4">
            <div className="flex gap-4">
              <div className="flex-1">
                <label htmlFor="personId" className="block text-sm font-medium text-gray-700 mb-1">
                  Filter by Person ID
                  <span className="ml-1 text-gray-400" title="Filter reminders and routines by person">ℹ️</span>
                </label>
                <select
                  id="personId"
                  value={selectedPersonId}
                  onChange={(e) => setSelectedPersonId(e.target.value)}
                  className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                >
                  <option value="">All Persons</option>
                  {personIdsData?.map((p) => (
                    <option key={p.personId} value={p.personId}>
                      {p.displayName} ({p.personId})
                    </option>
                  ))}
                </select>
              </div>
            </div>
          </div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          {/* Today's Active Reminders */}
          <div className="bg-white shadow rounded-lg p-6">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h2 className="text-lg font-semibold text-gray-900">Today&apos;s Active Reminders</h2>
                <p className="text-xs text-gray-500 mt-0.5" title="Reminders that are scheduled and have high confidence">
                  Will execute automatically when due
                </p>
              </div>
              <Link href="/reminders" className="text-sm text-indigo-600 hover:text-indigo-900" title="View all reminders">
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
                    className="flex items-center justify-between p-3 bg-green-50 border border-green-200 rounded-lg hover:border-green-300 transition-colors"
                    title={`Reminder: ${reminder.suggestedAction}. Executes at ${format(new Date(reminder.checkAtUtc), 'PPpp')}`}
                  >
                    <div className="flex-1 min-w-0">
                      <p className="font-medium text-gray-900 truncate" title={reminder.suggestedAction}>
                        {reminder.suggestedAction}
                      </p>
                      <div className="flex items-center gap-2 mt-1">
                        <p className="text-xs text-gray-600 font-medium">
                          {formatTimeUntilExecution(reminder.checkAtUtc)}
                        </p>
                        <span className="text-xs text-gray-400">•</span>
                        <p className="text-xs text-gray-500" title={`Scheduled time: ${format(new Date(reminder.checkAtUtc), 'PPpp')}`}>
                          {format(new Date(reminder.checkAtUtc), 'h:mm a')}
                        </p>
                      </div>
                    </div>
                    <div title={`Confidence: ${((reminder.confidence || 0) * 100).toFixed(0)}%`}>
                      <ConfidenceIndicator 
                        confidence={reminder.confidence || 0} 
                        size="sm" 
                        showLabel={false}
                      />
                    </div>
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
              <div>
                <h2 className="text-lg font-semibold text-gray-900">Active Routines</h2>
                <p className="text-xs text-gray-500 mt-0.5" title="Routines that are currently learning patterns">
                  Currently learning from your actions
                </p>
              </div>
              <Link href="/routines" className="text-sm text-indigo-600 hover:text-indigo-900" title="View all routines">
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
