// Manual event creation page
'use client';

import React, { useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { Layout } from '@/components/Layout';
import { apiService } from '@/services/api';
import type { ActionEventDto } from '@/types';

export default function CreateEventPage() {
  const router = useRouter();
  const [formData, setFormData] = useState<ActionEventDto>({
    personId: '',
    actionType: '',
    timestampUtc: new Date().toISOString().slice(0, 16),
    context: {
      timeBucket: 'morning',
      dayType: 'weekday',
      location: '',
      presentPeople: [],
      stateSignals: {},
    },
  });
  const [presentPeopleInput, setPresentPeopleInput] = useState('');
  const [stateSignalKey, setStateSignalKey] = useState('');
  const [stateSignalValue, setStateSignalValue] = useState('');

  const createMutation = useMutation({
    mutationFn: (event: ActionEventDto) => apiService.ingestEvent(event),
    onSuccess: () => {
      alert('Event created successfully!');
      router.push('/events');
    },
    onError: (error: any) => {
      alert(`Error creating event: ${error.response?.data?.message || error.message}`);
    },
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    
    // Convert timestamp to ISO string
    const event: ActionEventDto = {
      ...formData,
      timestampUtc: new Date(formData.timestampUtc).toISOString(),
    };
    
    createMutation.mutate(event);
  };

  const addPresentPerson = () => {
    if (presentPeopleInput.trim()) {
      setFormData({
        ...formData,
        context: {
          ...formData.context,
          presentPeople: [...(formData.context.presentPeople || []), presentPeopleInput.trim()],
        },
      });
      setPresentPeopleInput('');
    }
  };

  const removePresentPerson = (index: number) => {
    const newPeople = [...(formData.context.presentPeople || [])];
    newPeople.splice(index, 1);
    setFormData({
      ...formData,
      context: {
        ...formData.context,
        presentPeople: newPeople,
      },
    });
  };

  const addStateSignal = () => {
    if (stateSignalKey.trim() && stateSignalValue.trim()) {
      setFormData({
        ...formData,
        context: {
          ...formData.context,
          stateSignals: {
            ...(formData.context.stateSignals || {}),
            [stateSignalKey.trim()]: stateSignalValue.trim(),
          },
        },
      });
      setStateSignalKey('');
      setStateSignalValue('');
    }
  };

  const removeStateSignal = (key: string) => {
    const newSignals = { ...(formData.context.stateSignals || {}) };
    delete newSignals[key];
    setFormData({
      ...formData,
      context: {
        ...formData.context,
        stateSignals: newSignals,
      },
    });
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
          <h1 className="text-3xl font-bold text-gray-900">Create Manual Event</h1>
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
              <label htmlFor="actionType" className="block text-sm font-medium text-gray-700">
                Action Type *
              </label>
              <input
                type="text"
                id="actionType"
                value={formData.actionType}
                onChange={(e) => setFormData({ ...formData, actionType: e.target.value })}
                required
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                placeholder="e.g., play_music"
              />
            </div>

            <div>
              <label htmlFor="timestampUtc" className="block text-sm font-medium text-gray-700">
                Timestamp *
              </label>
              <input
                type="datetime-local"
                id="timestampUtc"
                value={formData.timestampUtc}
                onChange={(e) => setFormData({ ...formData, timestampUtc: e.target.value })}
                required
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
              />
            </div>

            <div>
              <label htmlFor="timeBucket" className="block text-sm font-medium text-gray-700">
                Time Bucket *
              </label>
              <select
                id="timeBucket"
                value={formData.context.timeBucket}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    context: { ...formData.context, timeBucket: e.target.value },
                  })
                }
                required
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
              >
                <option value="morning">Morning</option>
                <option value="afternoon">Afternoon</option>
                <option value="evening">Evening</option>
                <option value="night">Night</option>
              </select>
            </div>

            <div>
              <label htmlFor="dayType" className="block text-sm font-medium text-gray-700">
                Day Type *
              </label>
              <select
                id="dayType"
                value={formData.context.dayType}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    context: { ...formData.context, dayType: e.target.value },
                  })
                }
                required
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
              >
                <option value="weekday">Weekday</option>
                <option value="weekend">Weekend</option>
                <option value="holiday">Holiday</option>
              </select>
            </div>

            <div>
              <label htmlFor="location" className="block text-sm font-medium text-gray-700">
                Location
              </label>
              <input
                type="text"
                id="location"
                value={formData.context.location || ''}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    context: { ...formData.context, location: e.target.value },
                  })
                }
                className="mt-1 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                placeholder="e.g., living_room"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Present People
              </label>
              <div className="flex gap-2 mb-2">
                <input
                  type="text"
                  value={presentPeopleInput}
                  onChange={(e) => setPresentPeopleInput(e.target.value)}
                  onKeyPress={(e) => e.key === 'Enter' && (e.preventDefault(), addPresentPerson())}
                  className="flex-1 rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                  placeholder="Enter person ID and press Enter"
                />
                <button
                  type="button"
                  onClick={addPresentPerson}
                  className="px-4 py-2 bg-gray-200 text-gray-700 rounded-md hover:bg-gray-300"
                >
                  Add
                </button>
              </div>
              {formData.context.presentPeople && formData.context.presentPeople.length > 0 && (
                <div className="flex flex-wrap gap-2">
                  {formData.context.presentPeople.map((person, index) => (
                    <span
                      key={index}
                      className="inline-flex items-center px-3 py-1 rounded-full text-sm bg-blue-100 text-blue-800"
                    >
                      {person}
                      <button
                        type="button"
                        onClick={() => removePresentPerson(index)}
                        className="ml-2 text-blue-600 hover:text-blue-800"
                      >
                        ×
                      </button>
                    </span>
                  ))}
                </div>
              )}
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                State Signals
              </label>
              <div className="flex gap-2 mb-2">
                <input
                  type="text"
                  value={stateSignalKey}
                  onChange={(e) => setStateSignalKey(e.target.value)}
                  placeholder="Key"
                  className="flex-1 rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                />
                <input
                  type="text"
                  value={stateSignalValue}
                  onChange={(e) => setStateSignalValue(e.target.value)}
                  placeholder="Value"
                  className="flex-1 rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                />
                <button
                  type="button"
                  onClick={addStateSignal}
                  className="px-4 py-2 bg-gray-200 text-gray-700 rounded-md hover:bg-gray-300"
                >
                  Add
                </button>
              </div>
              {formData.context.stateSignals &&
                Object.keys(formData.context.stateSignals).length > 0 && (
                  <div className="space-y-1">
                    {Object.entries(formData.context.stateSignals).map(([key, value]) => (
                      <div
                        key={key}
                        className="flex items-center justify-between px-3 py-1 bg-gray-50 rounded"
                      >
                        <span className="text-sm">
                          <strong>{key}:</strong> {value}
                        </span>
                        <button
                          type="button"
                          onClick={() => removeStateSignal(key)}
                          className="text-red-600 hover:text-red-800"
                        >
                          ×
                        </button>
                      </div>
                    ))}
                  </div>
                )}
            </div>

            <div className="flex gap-2">
              <button
                type="submit"
                disabled={createMutation.isPending}
                className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700 disabled:opacity-50"
              >
                {createMutation.isPending ? 'Creating...' : 'Create Event'}
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

