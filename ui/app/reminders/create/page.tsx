// Manual reminder creation page
'use client';

import React, { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { Layout } from '@/components/Layout';
import { apiService } from '@/services/api';
import type { CreateManualReminderRequest } from '@/types';
import { ReminderStyle } from '@/types';

export default function CreateReminderPage() {
  const router = useRouter();
  const [formData, setFormData] = useState<CreateManualReminderRequest>({
    personId: '',
    suggestedAction: '',
    checkAtUtc: new Date().toISOString().slice(0, 16),
    style: ReminderStyle.Suggest,
    occurrence: '',
  });

  const createMutation = useMutation({
    mutationFn: (request: CreateManualReminderRequest) => apiService.createManualReminder(request),
    onSuccess: () => {
      alert('Reminder created successfully!');
      router.push('/reminders');
    },
    onError: (error: any) => {
      alert(`Error creating reminder: ${error.response?.data?.message || error.message}`);
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    
    const request: CreateManualReminderRequest = {
      ...formData,
      checkAtUtc: new Date(formData.checkAtUtc).toISOString(),
    };
    
    createMutation.mutate(request);
  };

  return (
    <Layout>
      <div className="px-4 py-6 sm:px-0">
        <div className="mb-6">
          <button
            onClick={() => router.back()}
            className="text-indigo-600 hover:text-indigo-800 mb-4"
          >
            ← Back
          </button>
          <h1 className="text-3xl font-bold text-gray-900">Create Manual Reminder</h1>
        </div>

        <div className="bg-white shadow rounded-lg p-6">
          <form onSubmit={handleSubmit} className="space-y-6">
            <div>
              <label htmlFor="personId" className="block text-sm font-medium text-gray-700">
                Person ID *
              </label>
              <input
                type="text"
                id="personId"
                value={formData.personId}
                onChange={(e) => setFormData({ ...formData, personId: e.target.value })}
                required
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                placeholder="e.g., alex"
              />
            </div>

            <div>
              <label htmlFor="suggestedAction" className="block text-sm font-medium text-gray-700">
                Suggested Action *
              </label>
              <input
                type="text"
                id="suggestedAction"
                value={formData.suggestedAction}
                onChange={(e) => setFormData({ ...formData, suggestedAction: e.target.value })}
                required
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                placeholder="e.g., play_music"
              />
            </div>

            <div>
              <label htmlFor="checkAtUtc" className="block text-sm font-medium text-gray-700">
                Check At (UTC) *
              </label>
              <input
                type="datetime-local"
                id="checkAtUtc"
                value={formData.checkAtUtc}
                onChange={(e) => setFormData({ ...formData, checkAtUtc: e.target.value })}
                required
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
              />
              <p className="mt-1 text-sm text-gray-500">
                The reminder will be checked at this time
              </p>
            </div>

            <div>
              <label htmlFor="style" className="block text-sm font-medium text-gray-700">
                Reminder Style *
              </label>
              <select
                id="style"
                value={formData.style}
                onChange={(e) => setFormData({ ...formData, style: e.target.value as ReminderStyle })}
                required
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
              >
                <option value={ReminderStyle.Ask}>Ask</option>
                <option value={ReminderStyle.Suggest}>Suggest</option>
                <option value={ReminderStyle.Silent}>Silent</option>
              </select>
              <p className="mt-1 text-sm text-gray-500">
                <strong>Ask:</strong> Direct question format<br />
                <strong>Suggest:</strong> Gentle suggestion format<br />
                <strong>Silent:</strong> No speech, just notification
              </p>
            </div>

            <div>
              <label htmlFor="occurrence" className="block text-sm font-medium text-gray-700">
                Occurrence (optional)
              </label>
              <input
                type="text"
                id="occurrence"
                value={formData.occurrence ?? ''}
                onChange={(e) => setFormData({ ...formData, occurrence: e.target.value || undefined })}
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                placeholder="e.g., daily, weekly, every 3 days, weekdays"
              />
              <p className="mt-1 text-sm text-gray-500">
                Define how often the reminder should occur (e.g., &quot;daily&quot;, &quot;weekly&quot;, &quot;every 3 days&quot;, &quot;weekdays&quot;)
              </p>
            </div>

            <div className="flex gap-2">
              <button
                type="submit"
                disabled={createMutation.isPending}
                className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700 disabled:opacity-50"
              >
                {createMutation.isPending ? 'Creating...' : 'Create Reminder'}
              </button>
              <button
                type="button"
                onClick={() => router.back()}
                className="px-4 py-2 border border-gray-300 rounded-md text-gray-700 hover:bg-gray-50"
              >
                Cancel
              </button>
            </div>
          </form>
        </div>
      </div>
    </Layout>
  );
}

