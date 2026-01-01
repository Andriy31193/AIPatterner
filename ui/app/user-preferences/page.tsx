'use client';

import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Layout } from '@/components/Layout';
import { apiService } from '@/services/api';
import { ReminderStyle } from '@/types';

export default function UserPreferencesPage() {
  const queryClient = useQueryClient();
  const [personId, setPersonId] = useState('');
  const [preferences, setPreferences] = useState({
    defaultStyle: ReminderStyle.Suggest,
    dailyLimit: 10,
    minimumInterval: 'PT15M', // 15 minutes in ISO 8601
    enabled: true,
  });
  const [isDirty, setIsDirty] = useState(false);

  const { data: loadedPreferences, isLoading } = useQuery({
    queryKey: ['userPreferences', personId],
    queryFn: () => apiService.getUserPreferences(personId),
    enabled: !!personId,
  });

  const updateMutation = useMutation({
    mutationFn: () => apiService.updateUserPreferences(personId, preferences),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['userPreferences', personId] });
      setIsDirty(false);
      alert('Preferences saved successfully!');
    },
    onError: (error: any) => {
      alert(`Failed to save preferences: ${error.message || 'Unknown error'}`);
    },
  });

  useEffect(() => {
    if (loadedPreferences) {
      const parsedInterval = parseInterval(loadedPreferences.minimumInterval);
      setPreferences({
        defaultStyle: loadedPreferences.defaultStyle,
        dailyLimit: loadedPreferences.dailyLimit,
        minimumInterval: loadedPreferences.minimumInterval,
        enabled: loadedPreferences.enabled,
      });
      setIntervalMinutes(parsedInterval);
      setIsDirty(false);
    }
  }, [loadedPreferences]);

  const handleLoad = () => {
    if (!personId.trim()) {
      alert('Please enter a Person ID');
      return;
    }
    queryClient.invalidateQueries({ queryKey: ['userPreferences', personId] });
  };

  const handleSave = () => {
    if (!personId.trim()) {
      alert('Please enter a Person ID');
      return;
    }
    updateMutation.mutate();
  };

  const parseInterval = (isoString: string): number => {
    // Parse ISO 8601 duration (e.g., "PT15M" = 15 minutes, "PT1H" = 1 hour)
    const match = isoString.match(/PT(?:(\d+)H)?(?:(\d+)M)?/);
    if (!match) return 15;
    const hours = parseInt(match[1] || '0', 10);
    const minutes = parseInt(match[2] || '0', 10);
    return hours * 60 + minutes;
  };

  const formatInterval = (minutes: number): string => {
    if (minutes < 60) {
      return `PT${minutes}M`;
    }
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    if (mins === 0) {
      return `PT${hours}H`;
    }
    return `PT${hours}H${mins}M`;
  };

  const [intervalMinutes, setIntervalMinutes] = useState(15);

  useEffect(() => {
    setIntervalMinutes(parseInterval(preferences.minimumInterval));
  }, [preferences.minimumInterval]);

  const handleIntervalChange = (minutes: number) => {
    setIntervalMinutes(minutes);
    setPreferences({ ...preferences, minimumInterval: formatInterval(minutes) });
    setIsDirty(true);
  };

  return (
    <Layout>
      <div className="px-4 py-6 sm:px-0">
        <div className="flex justify-between items-center mb-6">
          <h1 className="text-3xl font-bold text-gray-900">User Reminder Preferences</h1>
        </div>

        <div className="bg-white shadow rounded-lg p-6 mb-6">
          <div className="mb-4">
            <label htmlFor="personId" className="block text-sm font-medium text-gray-700 mb-2">
              Person ID
            </label>
            <div className="flex gap-2">
              <input
                type="text"
                id="personId"
                value={personId}
                onChange={(e) => setPersonId(e.target.value)}
                className="flex-1 block rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                placeholder="Enter person ID (e.g., alex)"
              />
              <button
                onClick={handleLoad}
                className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700"
              >
                Load
              </button>
            </div>
          </div>
        </div>

        {isLoading ? (
          <div className="bg-white shadow rounded-lg p-6 text-center text-gray-500">Loading...</div>
        ) : (
          <div className="bg-white shadow rounded-lg p-6">
            <h2 className="text-lg font-medium text-gray-900 mb-4">Preferences</h2>
            <p className="text-sm text-gray-600 mb-6">
              Configure reminder preferences for the selected person. These settings control how reminders are delivered and managed.
            </p>

            <div className="space-y-6">
              <div>
                <label htmlFor="enabled" className="flex items-center">
                  <input
                    type="checkbox"
                    id="enabled"
                    checked={preferences.enabled}
                    onChange={(e) => {
                      setPreferences({ ...preferences, enabled: e.target.checked });
                      setIsDirty(true);
                    }}
                    className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <span className="ml-2 text-sm font-medium text-gray-900">Enable Reminders</span>
                </label>
                <p className="mt-1 text-sm text-gray-500">
                  When disabled, no reminders will be sent for this person
                </p>
              </div>

              <div>
                <label htmlFor="defaultStyle" className="block text-sm font-medium text-gray-900 mb-2">
                  Default Reminder Style
                </label>
                <select
                  id="defaultStyle"
                  value={preferences.defaultStyle}
                  onChange={(e) => {
                    setPreferences({ ...preferences, defaultStyle: e.target.value as ReminderStyle });
                    setIsDirty(true);
                  }}
                  className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                >
                  <option value={ReminderStyle.Ask}>Ask</option>
                  <option value={ReminderStyle.Suggest}>Suggest</option>
                  <option value={ReminderStyle.Silent}>Silent</option>
                </select>
                <p className="mt-1 text-sm text-gray-500">
                  Default style for new reminders (Ask = ask user, Suggest = suggest action, Silent = no notification)
                </p>
              </div>

              <div>
                <label htmlFor="dailyLimit" className="block text-sm font-medium text-gray-900 mb-2">
                  Daily Limit
                </label>
                <input
                  type="number"
                  id="dailyLimit"
                  min="0"
                  value={preferences.dailyLimit}
                  onChange={(e) => {
                    setPreferences({ ...preferences, dailyLimit: parseInt(e.target.value) || 0 });
                    setIsDirty(true);
                  }}
                  className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                />
                <p className="mt-1 text-sm text-gray-500">
                  Maximum number of reminders that can be executed per day for this person
                </p>
              </div>

              <div>
                <label htmlFor="minimumInterval" className="block text-sm font-medium text-gray-900 mb-2">
                  Minimum Interval (minutes)
                </label>
                <input
                  type="number"
                  id="minimumInterval"
                  min="0"
                  value={intervalMinutes}
                  onChange={(e) => handleIntervalChange(parseInt(e.target.value) || 0)}
                  className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                />
                <p className="mt-1 text-sm text-gray-500">
                  Minimum time between reminder executions (in minutes)
                </p>
              </div>


              <div className="pt-4 border-t border-gray-200">
                <button
                  onClick={handleSave}
                  disabled={!isDirty || !personId.trim() || updateMutation.isPending}
                  className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700 disabled:opacity-50"
                >
                  {updateMutation.isPending ? 'Saving...' : 'Save Preferences'}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </Layout>
  );
}

