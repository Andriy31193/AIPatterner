'use client';

import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Layout } from '@/components/Layout';
import { apiService } from '@/services/api';
import type { Configuration } from '@/types';

interface MatchingPolicy {
  matchByActionType: boolean;
  matchByDayType: boolean;
  matchByPeoplePresent: boolean;
  matchByStateSignals: boolean;
  matchByTimeBucket: boolean;
  matchByLocation: boolean;
  timeOffsetMinutes: number;
}

const POLICY_CATEGORY = 'MatchingPolicy';

export default function PoliciesPage() {
  const queryClient = useQueryClient();
  const [policy, setPolicy] = useState<MatchingPolicy>({
    matchByActionType: true,
    matchByDayType: true,
    matchByPeoplePresent: true,
    matchByStateSignals: true,
    matchByTimeBucket: false,
    matchByLocation: false,
    timeOffsetMinutes: 30,
  });
  const [isDirty, setIsDirty] = useState(false);

  const { data: configurations, isLoading } = useQuery({
    queryKey: ['configurations', POLICY_CATEGORY],
    queryFn: () => apiService.getConfigurations(POLICY_CATEGORY),
  });

  const updateConfigMutation = useMutation({
    mutationFn: ({ key, value, description }: { key: string; value: string; description?: string }) =>
      apiService.updateConfiguration(POLICY_CATEGORY, key, { value, description }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['configurations'] });
      setIsDirty(false);
    },
  });

  const createConfigMutation = useMutation({
    mutationFn: ({ key, value, description }: { key: string; value: string; description?: string }) =>
      apiService.createConfiguration({ key, value, category: POLICY_CATEGORY, description }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['configurations'] });
      setIsDirty(false);
    },
  });

  const getConfigValue = React.useCallback((key: string, defaultValue: string): string => {
    const config = configurations?.find((c) => c.key === key);
    return config?.value || defaultValue;
  }, [configurations]);

  useEffect(() => {
    if (configurations) {
      const loadedPolicy: MatchingPolicy = {
        matchByActionType: getConfigValue('MatchByActionType', 'true') === 'true',
        matchByDayType: getConfigValue('MatchByDayType', 'true') === 'true',
        matchByPeoplePresent: getConfigValue('MatchByPeoplePresent', 'true') === 'true',
        matchByStateSignals: getConfigValue('MatchByStateSignals', 'true') === 'true',
        matchByTimeBucket: getConfigValue('MatchByTimeBucket', 'false') === 'true',
        matchByLocation: getConfigValue('MatchByLocation', 'false') === 'true',
        timeOffsetMinutes: parseInt(getConfigValue('TimeOffsetMinutes', '30'), 10),
      };
      setPolicy(loadedPolicy);
      setIsDirty(false);
    }
  }, [configurations, getConfigValue]);

  const saveConfig = async (key: string, value: string, description?: string) => {
    const existing = configurations?.find((c) => c.key === key);
    if (existing) {
      await updateConfigMutation.mutateAsync({ key, value, description });
    } else {
      await createConfigMutation.mutateAsync({ key, value, description });
    }
  };

  const handleSave = async () => {
    await Promise.all([
      saveConfig('MatchByActionType', policy.matchByActionType.toString(), 'Match reminders by action type'),
      saveConfig('MatchByDayType', policy.matchByDayType.toString(), 'Match reminders by day type'),
      saveConfig('MatchByPeoplePresent', policy.matchByPeoplePresent.toString(), 'Match reminders by people present'),
      saveConfig('MatchByStateSignals', policy.matchByStateSignals.toString(), 'Match reminders by state signals'),
      saveConfig('MatchByTimeBucket', policy.matchByTimeBucket.toString(), 'Match reminders by time bucket'),
      saveConfig('MatchByLocation', policy.matchByLocation.toString(), 'Match reminders by location'),
      saveConfig('TimeOffsetMinutes', policy.timeOffsetMinutes.toString(), 'Time offset in minutes for matching reminders'),
    ]);
  };

  const handleReset = () => {
    setPolicy({
      matchByActionType: true,
      matchByDayType: true,
      matchByPeoplePresent: true,
      matchByStateSignals: true,
      matchByTimeBucket: false,
      matchByLocation: false,
      timeOffsetMinutes: 30,
    });
    setIsDirty(true);
  };

  return (
    <Layout>
      <div className="px-4 py-6 sm:px-0">
        <div className="flex justify-between items-center mb-6">
          <h1 className="text-3xl font-bold text-gray-900">Matching Policies</h1>
          <div className="space-x-2">
            <button
              onClick={handleReset}
              className="px-4 py-2 border border-gray-300 rounded-md text-gray-700 hover:bg-gray-50"
            >
              Reset to Defaults
            </button>
            <button
              onClick={handleSave}
              disabled={!isDirty || updateConfigMutation.isPending || createConfigMutation.isPending}
              className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700 disabled:opacity-50"
            >
              {updateConfigMutation.isPending || createConfigMutation.isPending ? 'Saving...' : 'Save Changes'}
            </button>
          </div>
        </div>

        {isLoading ? (
          <div className="bg-white shadow rounded-lg p-6 text-center text-gray-500">Loading...</div>
        ) : (
          <div className="bg-white shadow rounded-lg p-6">
            <div className="mb-6">
              <h2 className="text-lg font-medium text-gray-900 mb-4">Event-Reminder Matching Criteria</h2>
              <p className="text-sm text-gray-600 mb-4">
                Configure how reminders are matched to events. These settings control which reminders are shown when viewing an event.
              </p>
            </div>

            <div className="space-y-4">
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <label className="flex items-center p-3 border border-gray-200 rounded-md hover:bg-gray-50 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={policy.matchByActionType}
                    onChange={(e) => {
                      setPolicy({ ...policy, matchByActionType: e.target.checked });
                      setIsDirty(true);
                    }}
                    className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <div className="ml-3">
                    <span className="text-sm font-medium text-gray-900">Match by Action Type</span>
                    <p className="text-xs text-gray-500">Match reminders with the same action type as the event</p>
                  </div>
                </label>

                <label className="flex items-center p-3 border border-gray-200 rounded-md hover:bg-gray-50 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={policy.matchByDayType}
                    onChange={(e) => {
                      setPolicy({ ...policy, matchByDayType: e.target.checked });
                      setIsDirty(true);
                    }}
                    className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <div className="ml-3">
                    <span className="text-sm font-medium text-gray-900">Match by Day Type</span>
                    <p className="text-xs text-gray-500">Match reminders based on day type (weekday/weekend)</p>
                  </div>
                </label>

                <label className="flex items-center p-3 border border-gray-200 rounded-md hover:bg-gray-50 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={policy.matchByPeoplePresent}
                    onChange={(e) => {
                      setPolicy({ ...policy, matchByPeoplePresent: e.target.checked });
                      setIsDirty(true);
                    }}
                    className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <div className="ml-3">
                    <span className="text-sm font-medium text-gray-900">Match by People Present</span>
                    <p className="text-xs text-gray-500">Match reminders when the same people are present</p>
                  </div>
                </label>

                <label className="flex items-center p-3 border border-gray-200 rounded-md hover:bg-gray-50 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={policy.matchByStateSignals}
                    onChange={(e) => {
                      setPolicy({ ...policy, matchByStateSignals: e.target.checked });
                      setIsDirty(true);
                    }}
                    className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <div className="ml-3">
                    <span className="text-sm font-medium text-gray-900">Match by State Signals</span>
                    <p className="text-xs text-gray-500">Match reminders based on state signals</p>
                  </div>
                </label>

                <label className="flex items-center p-3 border border-gray-200 rounded-md hover:bg-gray-50 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={policy.matchByTimeBucket}
                    onChange={(e) => {
                      setPolicy({ ...policy, matchByTimeBucket: e.target.checked });
                      setIsDirty(true);
                    }}
                    className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <div className="ml-3">
                    <span className="text-sm font-medium text-gray-900">Match by Time Bucket</span>
                    <p className="text-xs text-gray-500">Match reminders based on time bucket (morning/afternoon/evening)</p>
                  </div>
                </label>

                <label className="flex items-center p-3 border border-gray-200 rounded-md hover:bg-gray-50 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={policy.matchByLocation}
                    onChange={(e) => {
                      setPolicy({ ...policy, matchByLocation: e.target.checked });
                      setIsDirty(true);
                    }}
                    className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                  />
                  <div className="ml-3">
                    <span className="text-sm font-medium text-gray-900">Match by Location</span>
                    <p className="text-xs text-gray-500">Match reminders based on location</p>
                  </div>
                </label>
              </div>

              <div className="mt-6 pt-6 border-t border-gray-200">
                <label htmlFor="timeOffset" className="block text-sm font-medium text-gray-900 mb-2">
                  Time Offset (minutes)
                </label>
                <input
                  type="number"
                  id="timeOffset"
                  min="0"
                  value={policy.timeOffsetMinutes}
                  onChange={(e) => {
                    setPolicy({ ...policy, timeOffsetMinutes: parseInt(e.target.value) || 0 });
                    setIsDirty(true);
                  }}
                  className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                />
                <p className="mt-1 text-sm text-gray-500">
                  Maximum time difference (in minutes) between event timestamp and reminder check time for matching
                </p>
              </div>
            </div>
          </div>
        )}
      </div>
    </Layout>
  );
}

