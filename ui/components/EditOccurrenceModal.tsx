'use client';

import React, { useState } from 'react';

interface EditOccurrenceModalProps {
  isOpen: boolean;
  currentOccurrence: string | null | undefined;
  onClose: () => void;
  onSave: (occurrence: string | null) => Promise<void>;
}

export function EditOccurrenceModal({ isOpen, currentOccurrence, onClose, onSave }: EditOccurrenceModalProps) {
  const [occurrence, setOccurrence] = useState(currentOccurrence || '');
  const [isSaving, setIsSaving] = useState(false);

  const handleSave = async () => {
    setIsSaving(true);
    try {
      await onSave(occurrence.trim() || null);
      onClose();
    } catch (error) {
      console.error('Failed to update occurrence:', error);
      alert('Failed to update occurrence. Please try again.');
    } finally {
      setIsSaving(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto">
      <div className="flex items-center justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
        <div className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity" onClick={onClose}></div>

        <div className="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-lg sm:w-full">
          <div className="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
            <div className="flex justify-between items-center mb-4">
              <h3 className="text-lg leading-6 font-medium text-gray-900">
                Edit Reminder Occurrence
              </h3>
              <button
                onClick={onClose}
                className="text-gray-400 hover:text-gray-500"
              >
                <span className="sr-only">Close</span>
                <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>

            <div>
              <label htmlFor="occurrence" className="block text-sm font-medium text-gray-700">
                Occurrence Pattern
              </label>
              <input
                type="text"
                id="occurrence"
                value={occurrence}
                onChange={(e) => setOccurrence(e.target.value)}
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                placeholder="e.g., daily, weekly, every 3 days, weekdays"
              />
              <p className="mt-1 text-sm text-gray-500">
                Define how often the reminder should occur (e.g., &quot;daily&quot;, &quot;weekly&quot;, &quot;every 3 days&quot;, &quot;weekdays&quot;)
              </p>
            </div>
          </div>
          <div className="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
            <button
              type="button"
              onClick={handleSave}
              disabled={isSaving}
              className="w-full inline-flex justify-center items-center gap-2 rounded-md border border-transparent shadow-sm px-4 py-2 bg-indigo-600 text-base font-medium text-white hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:ml-3 sm:w-auto sm:text-sm disabled:opacity-50"
            >
              {isSaving ? '⏳ Saving...' : '💾 Save'}
            </button>
            <button
              type="button"
              onClick={onClose}
              className="mt-3 w-full inline-flex justify-center items-center gap-2 rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:mt-0 sm:ml-3 sm:w-auto sm:text-sm"
            >
              ❌ Cancel
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

