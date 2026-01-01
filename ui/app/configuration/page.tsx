// Configuration management page
'use client';

import React, { useState, useEffect } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Layout } from '@/components/Layout';
import { apiService } from '@/services/api';
import { useAuth } from '@/context/AuthContext';
import type { Configuration, CreateConfigurationRequest, UpdateConfigurationRequest, CreateApiKeyRequest, ApiKey } from '@/types';
import { ReminderStyle } from '@/types';

const CONFIG_TABS = [
  { value: 'policies', label: 'Policies', description: 'Reminder matching policies' },
  { value: 'user-preferences', label: 'User Preferences', description: 'User reminder preferences' },
  { value: 'api-keys', label: 'API Keys', description: 'API key management' },
  { value: 'notifications', label: 'Notifications', description: 'Webhook and notification endpoints' },
  { value: 'llm', label: 'LLM', description: 'Large Language Model endpoints' },
  { value: 'memory', label: 'Memory', description: 'Memory service endpoints' },
  { value: 'custom', label: 'Custom', description: 'Custom endpoint configurations' },
];

const CONFIG_CATEGORIES = [
  { value: 'notifications', label: 'Notifications', description: 'Webhook and notification endpoints' },
  { value: 'llm', label: 'LLM', description: 'Large Language Model endpoints' },
  { value: 'memory', label: 'Memory', description: 'Memory service endpoints' },
  { value: 'custom', label: 'Custom', description: 'Custom endpoint configurations' },
];

const DEFAULT_CONFIGS = {
  notifications: [
    { key: 'WebhookUrl', description: 'Webhook URL for reminder notifications' },
  ],
  llm: [
    { key: 'Endpoint', description: 'LLM service endpoint URL' },
    { key: 'Enabled', description: 'Enable LLM service (true/false)' },
  ],
  memory: [
    { key: 'Endpoint', description: 'Memory service endpoint URL' },
    { key: 'Enabled', description: 'Enable memory service (true/false)' },
  ],
};

// Policies Tab Component
function PoliciesTab() {
  const queryClient = useQueryClient();
  const [policy, setPolicy] = useState({
    matchByActionType: true,
    matchByDayType: true,
    matchByPeoplePresent: true,
    matchByStateSignals: true,
    matchByTimeBucket: false,
    matchByLocation: false,
    timeOffsetMinutes: 30,
  });
  const [isDirty, setIsDirty] = useState(false);
  const POLICY_CATEGORY = 'MatchingPolicy';

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

  useEffect(() => {
    if (configurations) {
      const getConfigValue = (key: string, defaultValue: string): string => {
        const config = configurations.find((c) => c.key === key);
        return config?.value || defaultValue;
      };
      setPolicy({
        matchByActionType: getConfigValue('MatchByActionType', 'true') === 'true',
        matchByDayType: getConfigValue('MatchByDayType', 'true') === 'true',
        matchByPeoplePresent: getConfigValue('MatchByPeoplePresent', 'true') === 'true',
        matchByStateSignals: getConfigValue('MatchByStateSignals', 'true') === 'true',
        matchByTimeBucket: getConfigValue('MatchByTimeBucket', 'false') === 'true',
        matchByLocation: getConfigValue('MatchByLocation', 'false') === 'true',
        timeOffsetMinutes: parseInt(getConfigValue('TimeOffsetMinutes', '30'), 10),
      });
      setIsDirty(false);
    }
  }, [configurations]);

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

  if (isLoading) {
    return <div className="bg-white shadow rounded-lg p-6 text-center text-gray-500">Loading...</div>;
  }

  return (
    <div className="bg-white shadow rounded-lg p-6">
      <div className="flex justify-between items-center mb-6">
        <h2 className="text-xl font-semibold text-gray-900">Matching Policies</h2>
        <div className="space-x-2">
          <button
            onClick={() => {
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
            }}
            className="px-4 py-2 border border-gray-300 rounded-md text-gray-700 hover:bg-gray-50"
          >
            🔄 Reset to Defaults
          </button>
          <button
            onClick={handleSave}
            disabled={!isDirty || updateConfigMutation.isPending || createConfigMutation.isPending}
            className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700 disabled:opacity-50 inline-flex items-center gap-2"
          >
            {updateConfigMutation.isPending || createConfigMutation.isPending ? '⏳ Saving...' : '💾 Save Changes'}
          </button>
        </div>
      </div>
      <p className="text-sm text-gray-600 mb-6">
        Configure how reminders are matched to events. When enabled, ALL selected criteria must match exactly for a reminder to be considered a match.
      </p>
      <div className="space-y-4">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {[
            { key: 'matchByActionType', label: 'Match by Action Type', desc: 'Match reminders with the same action type as the event' },
            { key: 'matchByDayType', label: 'Match by Day Type', desc: 'Match reminders based on day type (weekday/weekend)' },
            { key: 'matchByPeoplePresent', label: 'Match by People Present', desc: 'Match reminders when the same people are present' },
            { key: 'matchByStateSignals', label: 'Match by State Signals', desc: 'Match reminders based on state signals (all must match)' },
            { key: 'matchByTimeBucket', label: 'Match by Time Bucket', desc: 'Match reminders based on time bucket (morning/afternoon/evening)' },
            { key: 'matchByLocation', label: 'Match by Location', desc: 'Match reminders based on location' },
          ].map((item) => (
            <label key={item.key} className="flex items-center p-3 border border-gray-200 rounded-md hover:bg-gray-50 cursor-pointer">
              <input
                type="checkbox"
                checked={policy[item.key as keyof typeof policy] as boolean}
                onChange={(e) => {
                  setPolicy({ ...policy, [item.key]: e.target.checked });
                  setIsDirty(true);
                }}
                className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
              />
              <div className="ml-3">
                <span className="text-sm font-medium text-gray-900">{item.label}</span>
                <p className="text-xs text-gray-500">{item.desc}</p>
              </div>
            </label>
          ))}
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
  );
}

// User Preferences Tab Component  
function UserPreferencesTab() {
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const [preferences, setPreferences] = useState({
    defaultStyle: 'Suggest' as ReminderStyle,
    dailyLimit: 10,
    minimumInterval: 'PT15M',
    enabled: true,
  });
  const [isDirty, setIsDirty] = useState(false);
  const [intervalMinutes, setIntervalMinutes] = useState(15);

  // Use username as personId for the logged-in user
  const personId = user?.username || '';

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

  const parseInterval = (isoString: string): number => {
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

  const handleIntervalChange = (minutes: number) => {
    setIntervalMinutes(minutes);
    setPreferences({ ...preferences, minimumInterval: formatInterval(minutes) });
    setIsDirty(true);
  };

  if (!user) {
    return (
      <div className="bg-white shadow rounded-lg p-6 text-center text-gray-500">
        Please log in to view and manage your preferences.
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {isLoading ? (
        <div className="bg-white shadow rounded-lg p-6 text-center text-gray-500">Loading...</div>
      ) : (
        <div className="bg-white shadow rounded-lg p-6">
          <h2 className="text-lg font-medium text-gray-900 mb-4">Preferences</h2>
          <p className="text-sm text-gray-600 mb-6">
            Configure your reminder preferences. These settings control how reminders are delivered and managed for your account.
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
                When disabled, no reminders will be sent
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
                Maximum number of reminders that can be executed per day
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
                onClick={() => {
                  updateMutation.mutate();
                }}
                disabled={!isDirty || updateMutation.isPending}
                className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {updateMutation.isPending ? '⏳ Saving...' : '💾 Save Preferences'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// Api Keys Tab Component
function ApiKeysTab() {
  const queryClient = useQueryClient();
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [newKeyName, setNewKeyName] = useState('');
  const [newKeyRole, setNewKeyRole] = useState<'admin' | 'user'>('user');
  const [newKeyExpiresAt, setNewKeyExpiresAt] = useState('');
  const [createdKey, setCreatedKey] = useState<string | null>(null);
  const [showKeyWarning, setShowKeyWarning] = useState(false);

  const { data: apiKeys, isLoading } = useQuery({
    queryKey: ['apiKeys'],
    queryFn: () => apiService.getApiKeys(),
  });

  const createMutation = useMutation({
    mutationFn: (request: CreateApiKeyRequest) => apiService.createApiKey(request),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['apiKeys'] });
      setCreatedKey(data.fullKey);
      setShowKeyWarning(true);
      setShowCreateForm(false);
      setNewKeyName('');
      setNewKeyRole('user');
      setNewKeyExpiresAt('');
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => apiService.deleteApiKey(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['apiKeys'] });
    },
  });

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <h2 className="text-xl font-semibold text-gray-900">API Key Management</h2>
        <button
          onClick={() => setShowCreateForm(!showCreateForm)}
          className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700"
        >
          {showCreateForm ? '❌ Cancel' : '🔑 Generate New API Key'}
        </button>
      </div>

      {showKeyWarning && createdKey && (
        <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-4">
          <div className="flex justify-between items-start">
            <div className="flex-1">
              <h3 className="text-lg font-semibold text-yellow-800 mb-2">⚠️ Important: Save Your API Key</h3>
              <p className="text-yellow-700 mb-3">This is the only time you&apos;ll be able to see the full API key. Make sure to copy it now!</p>
              <div className="flex items-center gap-2">
                <code className="flex-1 bg-yellow-100 px-3 py-2 rounded text-sm font-mono text-yellow-900 break-all">
                  {createdKey}
                </code>
                <button
                  onClick={() => {
                    navigator.clipboard.writeText(createdKey);
                    alert('API key copied to clipboard!');
                  }}
                  className="px-4 py-2 bg-yellow-600 text-white rounded hover:bg-yellow-700"
                >
                  Copy
                </button>
              </div>
            </div>
            <button
              onClick={() => {
                setShowKeyWarning(false);
                setCreatedKey(null);
              }}
              className="ml-4 text-yellow-600 hover:text-yellow-800"
            >
              ✕
            </button>
          </div>
        </div>
      )}

      {showCreateForm && (
        <div className="bg-white shadow rounded-lg p-6">
          <h3 className="text-xl font-semibold text-gray-900 mb-4">Create New API Key</h3>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              createMutation.mutate({
                name: newKeyName,
                role: newKeyRole,
                expiresAtUtc: newKeyExpiresAt || undefined,
              });
            }}
            className="space-y-4"
          >
            <div>
              <label htmlFor="name" className="block text-sm font-medium text-gray-700">
                Name
              </label>
              <input
                type="text"
                id="name"
                value={newKeyName}
                onChange={(e) => setNewKeyName(e.target.value)}
                required
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                placeholder="e.g., Production API Key"
              />
            </div>
            <div>
              <label htmlFor="role" className="block text-sm font-medium text-gray-700">
                Role
              </label>
              <select
                id="role"
                value={newKeyRole}
                onChange={(e) => setNewKeyRole(e.target.value as 'admin' | 'user')}
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
              >
                <option value="user">User</option>
                <option value="admin">Admin</option>
              </select>
            </div>
            <div>
              <label htmlFor="expiresAt" className="block text-sm font-medium text-gray-700">
                Expires At (Optional)
              </label>
              <input
                type="datetime-local"
                id="expiresAt"
                value={newKeyExpiresAt}
                onChange={(e) => setNewKeyExpiresAt(e.target.value)}
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
              />
            </div>
            <div className="flex gap-2">
              <button
                type="submit"
                disabled={createMutation.isPending}
                className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700 disabled:opacity-50"
              >
                {createMutation.isPending ? '⏳ Creating...' : '🔑 Create API Key'}
              </button>
              <button
                type="button"
                onClick={() => setShowCreateForm(false)}
                className="px-4 py-2 border border-gray-300 rounded-md text-gray-700 hover:bg-gray-50 inline-flex items-center gap-2"
              >
                ❌ Cancel
              </button>
            </div>
          </form>
        </div>
      )}

      <div className="bg-white shadow rounded-lg overflow-hidden">
        <div className="px-4 py-5 sm:p-6">
          <h3 className="text-lg font-medium text-gray-900 mb-4">API Keys</h3>
          {isLoading ? (
            <div className="text-sm text-gray-500">Loading...</div>
          ) : !apiKeys || apiKeys.length === 0 ? (
            <div className="text-sm text-gray-500">No API keys found. Create one to get started.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Name</th>
                    <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Key Prefix</th>
                    <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Role</th>
                    <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Created</th>
                    <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Last Used</th>
                    <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Expires</th>
                    <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                    <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Actions</th>
                  </tr>
                </thead>
                <tbody className="bg-white divide-y divide-gray-200">
                  {apiKeys.map((key) => (
                    <tr key={key.id}>
                      <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-900">{key.name}</td>
                      <td className="px-3 py-2 whitespace-nowrap text-sm font-mono text-gray-500">
                        {key.keyPrefix}...
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">
                        <span className={`px-2 py-1 rounded text-xs ${
                          key.role === 'admin' ? 'bg-purple-100 text-purple-800' : 'bg-blue-100 text-blue-800'
                        }`}>
                          {key.role}
                        </span>
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">
                        {new Date(key.createdAtUtc).toLocaleDateString()}
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">
                        {key.lastUsedAtUtc ? new Date(key.lastUsedAtUtc).toLocaleDateString() : 'Never'}
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">
                        {key.expiresAtUtc ? new Date(key.expiresAtUtc).toLocaleDateString() : 'Never'}
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-sm">
                        <span className={`px-2 py-1 rounded text-xs ${
                          key.isActive ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'
                        }`}>
                          {key.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-sm">
                        <button
                          onClick={() => {
                            if (confirm('Are you sure you want to delete this API key? This action cannot be undone.')) {
                              deleteMutation.mutate(key.id);
                            }
                          }}
                          className="text-red-600 hover:text-red-900"
                        >
                          🗑️ Delete
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default function ConfigurationPage() {
  const { isAdmin } = useAuth();
  const queryClient = useQueryClient();
  const [selectedTab, setSelectedTab] = useState<string>('policies');
  const [selectedCategory, setSelectedCategory] = useState<string>('notifications');
  const [showCreateForm, setShowCreateForm] = useState(false);
  const [editingKey, setEditingKey] = useState<string | null>(null);
  const [newConfig, setNewConfig] = useState<CreateConfigurationRequest>({
    key: '',
    value: '',
    category: 'notifications',
    description: '',
  });
  const [editValue, setEditValue] = useState('');

  const { data: configurations, isLoading } = useQuery({
    queryKey: ['configurations', selectedCategory],
    queryFn: () => apiService.getConfigurations(selectedCategory),
  });

  const createMutation = useMutation({
    mutationFn: (request: CreateConfigurationRequest) => apiService.createConfiguration(request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['configurations'] });
      setShowCreateForm(false);
      setNewConfig({ key: '', value: '', category: selectedCategory, description: '' });
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ category, key, request }: { category: string; key: string; request: UpdateConfigurationRequest }) =>
      apiService.updateConfiguration(category, key, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['configurations'] });
      setEditingKey(null);
      setEditValue('');
    },
  });

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault();
    createMutation.mutate({ ...newConfig, category: selectedCategory });
  };

  const handleEdit = (config: Configuration) => {
    setEditingKey(config.key);
    setEditValue(config.value);
  };

  const handleSaveEdit = (config: Configuration) => {
    updateMutation.mutate({
      category: config.category,
      key: config.key,
      request: { value: editValue, description: config.description },
    });
  };

  const handleCancelEdit = () => {
    setEditingKey(null);
    setEditValue('');
  };

  const createDefaultConfig = (key: string) => {
    const defaultConfig = DEFAULT_CONFIGS[selectedCategory as keyof typeof DEFAULT_CONFIGS]?.find(
      (c) => c.key === key
    );
    if (defaultConfig) {
      setNewConfig({
        key: defaultConfig.key,
        value: '',
        category: selectedCategory,
        description: defaultConfig.description,
      });
      setShowCreateForm(true);
    }
  };

  return (
    <Layout>
      <div className="px-4 py-6 sm:px-0">
        <div className="flex justify-between items-center mb-6">
          <h1 className="text-3xl font-bold text-gray-900">Configuration</h1>
          {isAdmin && (
            <button
              onClick={() => {
                setNewConfig({ key: '', value: '', category: selectedCategory, description: '' });
                setShowCreateForm(!showCreateForm);
              }}
              className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700"
            >
              {showCreateForm ? '❌ Cancel' : '➕ Add Configuration'}
            </button>
          )}
        </div>

        {/* Main Tabs */}
        <div className="mb-6 border-b border-gray-200">
          <nav className="-mb-px flex space-x-8 overflow-x-auto">
            {CONFIG_TABS.map((tab) => (
              <button
                key={tab.value}
                onClick={() => {
                  setSelectedTab(tab.value);
                  setShowCreateForm(false);
                  setEditingKey(null);
                  if (CONFIG_CATEGORIES.some(c => c.value === tab.value)) {
                    setSelectedCategory(tab.value);
                  }
                }}
                className={`py-4 px-1 border-b-2 font-medium text-sm whitespace-nowrap ${
                  selectedTab === tab.value
                    ? 'border-indigo-500 text-indigo-600'
                    : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                }`}
              >
                {tab.label}
              </button>
            ))}
          </nav>
        </div>

        {/* Render content based on selected tab */}
        {selectedTab === 'policies' && (
          <PoliciesTab />
        )}
        {selectedTab === 'user-preferences' && (
          <UserPreferencesTab />
        )}
        {selectedTab === 'api-keys' && isAdmin && (
          <ApiKeysTab />
        )}
        {selectedTab !== 'policies' && selectedTab !== 'user-preferences' && selectedTab !== 'api-keys' && (
          <>
            {/* Create Form */}
            {showCreateForm && isAdmin && (
          <div className="mb-6 bg-white shadow rounded-lg p-6">
            <h2 className="text-xl font-semibold text-gray-900 mb-4">Add Configuration</h2>
            <form onSubmit={handleCreate} className="space-y-4">
              <div>
                <label htmlFor="newKey" className="block text-sm font-medium text-gray-700">
                  Key *
                </label>
                <input
                  type="text"
                  id="newKey"
                  value={newConfig.key}
                  onChange={(e) => setNewConfig({ ...newConfig, key: e.target.value })}
                  required
                  className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                  placeholder="e.g., WebhookUrl"
                />
              </div>
              <div>
                <label htmlFor="newValue" className="block text-sm font-medium text-gray-700">
                  Value *
                </label>
                <input
                  type="text"
                  id="newValue"
                  value={newConfig.value}
                  onChange={(e) => setNewConfig({ ...newConfig, value: e.target.value })}
                  required
                  className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                  placeholder="e.g., https://example.com/webhook"
                />
              </div>
              <div>
                <label htmlFor="newDescription" className="block text-sm font-medium text-gray-700">
                  Description
                </label>
                <textarea
                  id="newDescription"
                  value={newConfig.description}
                  onChange={(e) => setNewConfig({ ...newConfig, description: e.target.value })}
                  rows={2}
                  className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                  placeholder="Optional description"
                />
              </div>
              <div className="flex gap-2">
                <button
                  type="submit"
                  disabled={createMutation.isPending}
                  className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700 disabled:opacity-50"
                >
                  {createMutation.isPending ? '⏳ Creating...' : '➕ Create'}
                </button>
                <button
                  type="button"
                  onClick={() => setShowCreateForm(false)}
                  className="px-4 py-2 border border-gray-300 rounded-md text-gray-700 hover:bg-gray-50 inline-flex items-center gap-2"
                >
                  ❌ Cancel
                </button>
              </div>
            </form>

            {/* Quick Add Defaults */}
            {DEFAULT_CONFIGS[selectedCategory as keyof typeof DEFAULT_CONFIGS] && (
              <div className="mt-4 pt-4 border-t border-gray-200">
                <p className="text-sm text-gray-600 mb-2">Quick add:</p>
                <div className="flex flex-wrap gap-2">
                  {DEFAULT_CONFIGS[selectedCategory as keyof typeof DEFAULT_CONFIGS]
                    ?.filter((c) => !configurations?.some((config) => config.key === c.key))
                    .map((config) => (
                      <button
                        key={config.key}
                        type="button"
                        onClick={() => createDefaultConfig(config.key)}
                        className="px-3 py-1 text-sm bg-gray-100 text-gray-700 rounded hover:bg-gray-200"
                      >
                        + {config.key}
                      </button>
                    ))}
                </div>
              </div>
            )}
          </div>
        )}

        {/* Configurations List */}
        <div className="bg-white shadow rounded-lg overflow-hidden">
          <div className="px-4 py-5 sm:p-6">
            <h2 className="text-lg font-medium text-gray-900 mb-4">
              {CONFIG_CATEGORIES.find((c) => c.value === selectedCategory)?.label} Configurations
            </h2>
            {isLoading ? (
              <div className="text-sm text-gray-500">Loading...</div>
            ) : !configurations || configurations.length === 0 ? (
              <div className="text-sm text-gray-500">
                No configurations found. {isAdmin && 'Add one to get started.'}
              </div>
            ) : (
              <div className="space-y-4">
                {configurations.map((config) => (
                  <div key={config.id} className="border border-gray-200 rounded-lg p-4">
                    <div className="flex justify-between items-start mb-2">
                      <div className="flex-1">
                        <h3 className="text-sm font-semibold text-gray-900">{config.key}</h3>
                        {config.description && (
                          <p className="text-xs text-gray-500 mt-1">{config.description}</p>
                        )}
                      </div>
                      {isAdmin && editingKey !== config.key && (
                        <button
                          onClick={() => handleEdit(config)}
                          className="text-indigo-600 hover:text-indigo-800 text-sm"
                        >
                          ✏️ Edit
                        </button>
                      )}
                    </div>
                    {editingKey === config.key && isAdmin ? (
                      <div className="space-y-2">
                        <input
                          type="text"
                          value={editValue}
                          onChange={(e) => setEditValue(e.target.value)}
                          className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                        />
                        <div className="flex gap-2">
                          <button
                            onClick={() => handleSaveEdit(config)}
                            disabled={updateMutation.isPending}
                            className="px-3 py-1 text-sm bg-indigo-600 text-white rounded hover:bg-indigo-700 disabled:opacity-50"
                          >
                            💾 Save
                          </button>
                          <button
                            onClick={handleCancelEdit}
                            className="px-3 py-1 text-sm border border-gray-300 rounded text-gray-700 hover:bg-gray-50 inline-flex items-center gap-1"
                          >
                            ❌ Cancel
                          </button>
                        </div>
                      </div>
                    ) : (
                      <div className="mt-2">
                        <code className="text-sm bg-gray-50 px-2 py-1 rounded text-gray-800 break-all">
                          {config.value || '(empty)'}
                        </code>
                      </div>
                    )}
                    <div className="mt-2 text-xs text-gray-400">
                      Updated: {new Date(config.updatedAtUtc).toLocaleString()}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
          </>
        )}
      </div>
    </Layout>
  );
}

