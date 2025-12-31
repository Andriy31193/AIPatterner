// Configuration management page
'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Layout } from '@/components/Layout';
import { apiService } from '@/services/api';
import { useAuth } from '@/context/AuthContext';
import type { Configuration, CreateConfigurationRequest, UpdateConfigurationRequest } from '@/types';

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

export default function ConfigurationPage() {
  const { isAdmin } = useAuth();
  const queryClient = useQueryClient();
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
              {showCreateForm ? 'Cancel' : 'Add Configuration'}
            </button>
          )}
        </div>

        {/* Category Tabs */}
        <div className="mb-6 border-b border-gray-200">
          <nav className="-mb-px flex space-x-8">
            {CONFIG_CATEGORIES.map((category) => (
              <button
                key={category.value}
                onClick={() => {
                  setSelectedCategory(category.value);
                  setShowCreateForm(false);
                  setEditingKey(null);
                }}
                className={`py-4 px-1 border-b-2 font-medium text-sm ${
                  selectedCategory === category.value
                    ? 'border-indigo-500 text-indigo-600'
                    : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                }`}
              >
                {category.label}
              </button>
            ))}
          </nav>
        </div>

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
                  {createMutation.isPending ? 'Creating...' : 'Create'}
                </button>
                <button
                  type="button"
                  onClick={() => setShowCreateForm(false)}
                  className="px-4 py-2 border border-gray-300 rounded-md text-gray-700 hover:bg-gray-50"
                >
                  Cancel
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
                          Edit
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
                            Save
                          </button>
                          <button
                            onClick={handleCancelEdit}
                            className="px-3 py-1 text-sm border border-gray-300 rounded text-gray-700 hover:bg-gray-50"
                          >
                            Cancel
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
      </div>
    </Layout>
  );
}

