// API key management page (admin only)
'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Layout } from '@/components/Layout';
import { apiService } from '@/services/api';
import type { ApiKey, CreateApiKeyRequest } from '@/types';

export default function ApiKeysPage() {
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

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault();
    const request: CreateApiKeyRequest = {
      name: newKeyName,
      role: newKeyRole,
      expiresAtUtc: newKeyExpiresAt || undefined,
    };
    createMutation.mutate(request);
  };

  const handleDelete = (id: string) => {
    if (confirm('Are you sure you want to delete this API key? This action cannot be undone.')) {
      deleteMutation.mutate(id);
    }
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    alert('API key copied to clipboard!');
  };

  return (
    <Layout requireAdmin>
      <div className="px-4 py-6 sm:px-0">
        <div className="flex justify-between items-center mb-6">
          <h1 className="text-3xl font-bold text-gray-900">API Key Management</h1>
          <button
            onClick={() => setShowCreateForm(!showCreateForm)}
            className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700"
          >
            {showCreateForm ? 'Cancel' : 'Generate New API Key'}
          </button>
        </div>

        {showKeyWarning && createdKey && (
          <div className="mb-6 bg-yellow-50 border border-yellow-200 rounded-lg p-4">
            <div className="flex justify-between items-start">
              <div className="flex-1">
                <h3 className="text-lg font-semibold text-yellow-800 mb-2">
                  ⚠️ Important: Save Your API Key
                </h3>
                <p className="text-yellow-700 mb-3">
                  This is the only time you&apos;ll be able to see the full API key. Make sure to copy it now!
                </p>
                <div className="flex items-center gap-2">
                  <code className="flex-1 bg-yellow-100 px-3 py-2 rounded text-sm font-mono text-yellow-900 break-all">
                    {createdKey}
                  </code>
                  <button
                    onClick={() => copyToClipboard(createdKey)}
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
          <div className="mb-6 bg-white shadow rounded-lg p-6">
            <h2 className="text-xl font-semibold text-gray-900 mb-4">Create New API Key</h2>
            <form onSubmit={handleCreate} className="space-y-4">
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
                  {createMutation.isPending ? 'Creating...' : 'Create API Key'}
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
          </div>
        )}

        <div className="bg-white shadow rounded-lg overflow-hidden">
          <div className="px-4 py-5 sm:p-6">
            <h2 className="text-lg font-medium text-gray-900 mb-4">API Keys</h2>
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
                            onClick={() => handleDelete(key.id)}
                            className="text-red-600 hover:text-red-900"
                          >
                            Delete
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
    </Layout>
  );
}
