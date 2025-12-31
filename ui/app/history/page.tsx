// Execution history page
'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Layout } from '@/components/Layout';
import { DateTimeDisplay } from '@/components/DateTimeDisplay';
import { apiService } from '@/services/api';
import { useAuth } from '@/context/AuthContext';
import type { ExecutionHistoryDto } from '@/types';

export default function HistoryPage() {
  const { isAdmin } = useAuth();
  const [personId, setPersonId] = useState('');
  const [actionType, setActionType] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [page, setPage] = useState(1);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const pageSize = 20;
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['executionHistory', { personId, actionType, fromDate, toDate, page, pageSize }],
    queryFn: () => apiService.getExecutionHistory({
      personId: personId || undefined,
      actionType: actionType || undefined,
      fromUtc: fromDate || undefined,
      toUtc: toDate || undefined,
      page,
      pageSize,
    }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => apiService.deleteExecutionHistory(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['executionHistory'] });
    },
  });

  const handleDelete = (id: string) => {
    if (confirm('Are you sure you want to delete this execution history entry?')) {
      deleteMutation.mutate(id);
    }
  };

  const toggleExpand = (id: string) => {
    setExpandedId(expandedId === id ? null : id);
  };

  const formatJson = (jsonString: string) => {
    try {
      const parsed = JSON.parse(jsonString);
      return JSON.stringify(parsed, null, 2);
    } catch {
      return jsonString;
    }
  };

  return (
    <Layout>
      <div className="px-4 py-6 sm:px-0">
        <h1 className="text-3xl font-bold text-gray-900 mb-6">Execution History</h1>

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
                Clear
              </button>
            </div>
          </div>
        </div>

        {/* History Table */}
        <div className="bg-white shadow rounded-lg overflow-hidden">
          {isLoading ? (
            <div className="p-6 text-center text-gray-500">Loading...</div>
          ) : !data || data.items.length === 0 ? (
            <div className="p-6 text-center text-gray-500">No execution history found.</div>
          ) : (
            <>
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-200">
                  <thead className="bg-gray-50">
                    <tr>
                      <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Timestamp</th>
                      <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Endpoint</th>
                      <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Person</th>
                      <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Action Type</th>
                      <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Details</th>
                      {isAdmin && (
                        <th className="px-3 py-2 text-left text-xs font-medium text-gray-500 uppercase">Actions</th>
                      )}
                    </tr>
                  </thead>
                  <tbody className="bg-white divide-y divide-gray-200">
                    {data.items.map((entry: ExecutionHistoryDto) => (
                      <React.Fragment key={entry.id}>
                        <tr>
                          <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">
                            <DateTimeDisplay date={entry.executedAtUtc} />
                          </td>
                          <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-900">{entry.endpoint}</td>
                          <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">{entry.personId || '-'}</td>
                          <td className="px-3 py-2 whitespace-nowrap text-sm text-gray-500">{entry.actionType || '-'}</td>
                          <td className="px-3 py-2 whitespace-nowrap text-sm">
                            <button
                              onClick={() => toggleExpand(entry.id)}
                              className="text-indigo-600 hover:text-indigo-900"
                            >
                              {expandedId === entry.id ? 'Hide' : 'Show'} Details
                            </button>
                          </td>
                          {isAdmin && (
                            <td className="px-3 py-2 whitespace-nowrap text-sm">
                              <button
                                onClick={() => handleDelete(entry.id)}
                                className="text-red-600 hover:text-red-900"
                              >
                                Delete
                              </button>
                            </td>
                          )}
                        </tr>
                        {expandedId === entry.id && (
                          <tr>
                            <td colSpan={isAdmin ? 6 : 5} className="px-3 py-4 bg-gray-50">
                              <div className="space-y-4">
                                <div>
                                  <h4 className="text-sm font-semibold text-gray-700 mb-2">Request Payload:</h4>
                                  <pre className="bg-white p-3 rounded border text-xs overflow-x-auto">
                                    {formatJson(entry.requestPayload)}
                                  </pre>
                                </div>
                                <div>
                                  <h4 className="text-sm font-semibold text-gray-700 mb-2">Response Payload:</h4>
                                  <pre className="bg-white p-3 rounded border text-xs overflow-x-auto">
                                    {formatJson(entry.responsePayload)}
                                  </pre>
                                </div>
                              </div>
                            </td>
                          </tr>
                        )}
                      </React.Fragment>
                    ))}
                  </tbody>
                </table>
              </div>

              {/* Pagination */}
              {data.totalCount > pageSize && (
                <div className="bg-white px-4 py-3 flex items-center justify-between border-t border-gray-200 sm:px-6">
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
        </div>
      </div>
    </Layout>
  );
}

