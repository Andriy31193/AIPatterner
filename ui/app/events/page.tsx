// Event feed page showing incoming action events
'use client';

import React, { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { Layout } from '@/components/Layout';
import { DateTimeDisplay } from '@/components/DateTimeDisplay';
import { apiService } from '@/services/api';

export default function EventsPage() {
  const router = useRouter();
  const [personId, setPersonId] = useState('');
  const [actionType, setActionType] = useState('');

  // Note: Backend doesn't have an events list endpoint yet
  // This is a placeholder that would need backend support
  const { data, isLoading } = useQuery({
    queryKey: ['events', { personId, actionType }],
    queryFn: async () => {
      // TODO: Implement events list endpoint in backend
      return { events: [] };
    },
    enabled: false, // Disabled until backend endpoint exists
  });

  return (
    <Layout>
      <div className="px-4 py-6 sm:px-0">
        <div className="flex justify-between items-center mb-6">
          <h1 className="text-3xl font-bold text-gray-900">Event Feed</h1>
          <button
            onClick={() => router.push('/events/create')}
            className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700"
          >
            Create Event
          </button>
        </div>

        {/* Filters */}
        <div className="bg-white shadow rounded-lg mb-6 p-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
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
            <div className="flex items-end">
              <button
                onClick={() => {
                  setPersonId('');
                  setActionType('');
                }}
                className="w-full px-4 py-2 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50"
              >
                Clear Filters
              </button>
            </div>
          </div>
        </div>

        {/* Events List - TODO: Add backend endpoint for listing events */}
        <div className="bg-white shadow rounded-lg p-6">
          <p className="text-gray-500 mb-4">
            Event listing functionality requires backend endpoint implementation. For now, you can create events manually.
          </p>
          <p className="text-sm text-gray-400">
            Note: Events can be deleted from the History page once execution history is recorded.
          </p>
        </div>
      </div>
    </Layout>
  );
}

