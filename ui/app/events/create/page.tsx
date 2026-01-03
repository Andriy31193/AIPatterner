// Enhanced event creation page with live preview and visual feedback
'use client';

import React, { useState, useEffect, useMemo } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';
import { Layout } from '@/components/Layout';
import { ConfidenceIndicator } from '@/components/ConfidenceIndicator';
import { LearningBadge } from '@/components/LearningBadge';
import { apiService } from '@/services/api';
import { useAuth } from '@/context/AuthContext';
import type { ActionEventDto, ReminderCandidateDto, RoutineDto } from '@/types';
import { ProbabilityAction, EventType } from '@/types';

// Known intent types
const KNOWN_INTENTS = [
  { value: 'ArrivalHome', label: 'Arrival Home', icon: '🏠', description: 'When you arrive home' },
  { value: 'LeavingHome', label: 'Leaving Home', icon: '🚪', description: 'When you leave home' },
  { value: 'GoingToSleep', label: 'Going to Sleep', icon: '😴', description: 'When you go to bed' },
  { value: 'StartingWork', label: 'Starting Work', icon: '💼', description: 'When you start work' },
  { value: 'Custom', label: 'Custom Intent', icon: '🎯', description: 'Custom intent type' },
];

// Common tool actions
const COMMON_TOOLS = [
  { value: 'PlayMusic', label: 'Play Music', icon: '🎵' },
  { value: 'TurnOnLights', label: 'Turn On Lights', icon: '💡' },
  { value: 'TurnOffLights', label: 'Turn Off Lights', icon: '🌙' },
  { value: 'AdjustTemperature', label: 'Adjust Temperature', icon: '🌡️' },
  { value: 'LockDoors', label: 'Lock Doors', icon: '🔒' },
  { value: 'Custom', label: 'Custom Action', icon: '⚙️' },
];

export default function CreateEventPage() {
  const router = useRouter();
  const { user, isAdmin } = useAuth();
  const [eventType, setEventType] = useState<EventType>(EventType.Action);
  const [intentType, setIntentType] = useState<string>('');
  const [customIntentType, setCustomIntentType] = useState('');
  const [selectedTool, setSelectedTool] = useState<string>('');
  const [customTool, setCustomTool] = useState('');
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [ignoreForLearning, setIgnoreForLearning] = useState(false);

  const [formData, setFormData] = useState<ActionEventDto>({
    personId: '',
    actionType: '',
    eventType: EventType.Action,
    timestampUtc: new Date().toISOString().slice(0, 16),
    context: {
      timeBucket: getTimeBucket(new Date()),
      dayType: getDayType(new Date()),
      location: '',
      presentPeople: [],
      stateSignals: {},
    },
    probabilityValue: undefined,
    probabilityAction: undefined,
  });

  // For non-admin users, set personId to their username on mount
  useEffect(() => {
    if (!isAdmin && user?.username) {
      setFormData(prev => ({ ...prev, personId: user.username }));
    }
  }, [isAdmin, user]);

  // Fetch personIds for admin dropdown
  const { data: personIdsData } = useQuery({
    queryKey: ['personIds'],
    queryFn: () => apiService.getPersonIds(),
    enabled: isAdmin,
  });

  // Fetch existing reminders and routines for preview
  const { data: remindersData } = useQuery({
    queryKey: ['reminderCandidates', { personId: formData.personId }],
    queryFn: () => apiService.getReminderCandidates({ 
      personId: formData.personId || undefined,
      page: 1,
      pageSize: 50,
    }),
    enabled: !!formData.personId && formData.actionType.length > 0,
  });

  const { data: routinesData } = useQuery({
    queryKey: ['routines', { personId: formData.personId }],
    queryFn: () => apiService.getRoutines({ 
      personId: formData.personId || undefined,
      page: 1,
      pageSize: 20,
    }),
    enabled: !!formData.personId,
  });

  const { data: activeRoutinesData } = useQuery({
    queryKey: ['activeRoutines', { personId: formData.personId }],
    queryFn: () => apiService.getActiveRoutines(formData.personId),
    enabled: !!formData.personId && eventType === EventType.StateChange,
  });

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

  // Helper functions
  function getTimeBucket(date: Date): string {
    const hour = date.getHours();
    if (hour >= 5 && hour < 12) return 'morning';
    if (hour >= 12 && hour < 17) return 'afternoon';
    if (hour >= 17 && hour < 22) return 'evening';
    return 'night';
  }

  function getDayType(date: Date): string {
    const day = date.getDay();
    if (day === 0 || day === 6) return 'weekend';
    return 'weekday';
  }

  // Update action type based on selections
  useEffect(() => {
    if (eventType === EventType.StateChange) {
      const finalIntentType = intentType === 'Custom' ? customIntentType : intentType;
      setFormData(prev => ({
        ...prev,
        actionType: finalIntentType,
        eventType: EventType.StateChange,
      }));
    } else {
      const finalTool = selectedTool === 'Custom' ? customTool : selectedTool;
      setFormData(prev => ({
        ...prev,
        actionType: finalTool,
        eventType: EventType.Action,
      }));
    }
  }, [eventType, intentType, customIntentType, selectedTool, customTool]);

  // Calculate preview data
  const previewData = useMemo(() => {
    if (!formData.personId || !formData.actionType) return null;

    const affectedReminders: ReminderCandidateDto[] = [];
    const affectedRoutines: RoutineDto[] = [];

    // Find matching reminders
    if (remindersData && eventType === EventType.Action) {
      affectedReminders.push(...remindersData.items.filter((r: ReminderCandidateDto) => 
        r.suggestedAction.toLowerCase() === formData.actionType.toLowerCase() ||
        r.suggestedAction.toLowerCase().includes(formData.actionType.toLowerCase())
      ));
    }

    // Find matching routines
    if (routinesData && eventType === EventType.StateChange) {
      affectedRoutines.push(...routinesData.items.filter((r: RoutineDto) => 
        r.intentType.toLowerCase() === formData.actionType.toLowerCase()
      ));
    }

    return {
      affectedReminders,
      affectedRoutines,
      willCreateNewReminder: eventType === EventType.Action && affectedReminders.length === 0,
      willActivateRoutine: eventType === EventType.StateChange && affectedRoutines.length > 0,
      willCreateNewRoutine: eventType === EventType.StateChange && affectedRoutines.length === 0,
    };
  }, [formData, remindersData, routinesData, eventType]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    
    // If "ignore for learning" is checked, set probability values to null
    const event: ActionEventDto = {
      ...formData,
      timestampUtc: new Date(formData.timestampUtc).toISOString(),
      eventType: eventType,
      probabilityValue: ignoreForLearning ? undefined : formData.probabilityValue,
      probabilityAction: ignoreForLearning ? undefined : formData.probabilityAction,
    };
    
    createMutation.mutate(event);
  };

  const handleTimestampChange = (value: string) => {
    const date = new Date(value);
    setFormData(prev => ({
      ...prev,
      timestampUtc: value,
      context: {
        ...prev.context,
        timeBucket: getTimeBucket(date),
        dayType: getDayType(date),
      },
    }));
  };

  return (
    <Layout>
      <div className="px-4 py-6 sm:px-0 max-w-6xl mx-auto">
        <div className="mb-6">
          <button
            onClick={() => router.back()}
            className="text-indigo-600 hover:text-indigo-800 mb-4 flex items-center gap-2"
          >
            ← Back to Events
          </button>
          <div>
            <h1 className="text-3xl font-bold text-gray-900">Create Event</h1>
            <p className="text-sm text-gray-500 mt-1">
              Create an event to help the system learn your patterns and routines
            </p>
          </div>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Form Section */}
          <div className="lg:col-span-2">
            <div className="bg-white shadow rounded-lg p-6">
              <form onSubmit={handleSubmit} className="space-y-6">
                {/* Event Type */}
                <div>
                  <label className="block text-sm font-medium text-gray-900 mb-2">
                    Event Type *
                  </label>
                  <div className="grid grid-cols-3 gap-3">
                    <button
                      type="button"
                      onClick={() => setEventType(EventType.Action)}
                      className={`p-4 border-2 rounded-lg text-center transition-all ${
                        eventType === EventType.Action
                          ? 'border-indigo-500 bg-indigo-50'
                          : 'border-gray-200 hover:border-gray-300'
                      }`}
                    >
                      <div className="text-2xl mb-2">⚡</div>
                      <div className="text-sm font-medium">Tool Execution</div>
                      <div className="text-xs text-gray-500 mt-1">Action performed</div>
                    </button>
                    <button
                      type="button"
                      onClick={() => setEventType(EventType.StateChange)}
                      className={`p-4 border-2 rounded-lg text-center transition-all ${
                        eventType === EventType.StateChange
                          ? 'border-indigo-500 bg-indigo-50'
                          : 'border-gray-200 hover:border-gray-300'
                      }`}
                    >
                      <div className="text-2xl mb-2">🎯</div>
                      <div className="text-sm font-medium">Intent</div>
                      <div className="text-xs text-gray-500 mt-1">State change</div>
                    </button>
                    <button
                      type="button"
                      onClick={() => setEventType(EventType.Action)}
                      className={`p-4 border-2 rounded-lg text-center transition-all ${
                        eventType === EventType.Action && formData.actionType && !KNOWN_INTENTS.some(i => i.value === formData.actionType) && !COMMON_TOOLS.some(t => t.value === formData.actionType)
                          ? 'border-indigo-500 bg-indigo-50'
                          : 'border-gray-200 hover:border-gray-300'
                      }`}
                    >
                      <div className="text-2xl mb-2">🔧</div>
                      <div className="text-sm font-medium">Custom</div>
                      <div className="text-xs text-gray-500 mt-1">Custom event</div>
                    </button>
                  </div>
                  <p className="mt-2 text-xs text-gray-500">
                    {eventType === EventType.StateChange 
                      ? 'Intents activate routines and open observation windows'
                      : 'Tool executions are learned actions that can trigger reminders'}
                  </p>
                </div>

                {/* Intent Type (if StateChange) */}
                {eventType === EventType.StateChange && (
                  <div>
                    <label className="block text-sm font-medium text-gray-900 mb-2">
                      Intent Type *
                    </label>
                    <div className="grid grid-cols-2 gap-2 mb-2">
                      {KNOWN_INTENTS.map(intent => (
                        <button
                          key={intent.value}
                          type="button"
                          onClick={() => {
                            setIntentType(intent.value);
                            if (intent.value !== 'Custom') {
                              setCustomIntentType('');
                            }
                          }}
                          className={`p-3 border rounded-lg text-left transition-all ${
                            intentType === intent.value
                              ? 'border-indigo-500 bg-indigo-50'
                              : 'border-gray-200 hover:border-gray-300'
                          }`}
                        >
                          <div className="flex items-center gap-2">
                            <span className="text-xl">{intent.icon}</span>
                            <div>
                              <div className="text-sm font-medium">{intent.label}</div>
                              <div className="text-xs text-gray-500">{intent.description}</div>
                            </div>
                          </div>
                        </button>
                      ))}
                    </div>
                    {intentType === 'Custom' && (
                      <input
                        type="text"
                        value={customIntentType}
                        onChange={(e) => setCustomIntentType(e.target.value)}
                        placeholder="Enter custom intent type..."
                        className="mt-2 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                      />
                    )}
                  </div>
                )}

                {/* Tool Selection (if Action) */}
                {eventType === EventType.Action && (
                  <div>
                    <label className="block text-sm font-medium text-gray-900 mb-2">
                      Action / Tool *
                    </label>
                    <div className="grid grid-cols-3 gap-2 mb-2">
                      {COMMON_TOOLS.map(tool => (
                        <button
                          key={tool.value}
                          type="button"
                          onClick={() => {
                            setSelectedTool(tool.value);
                            if (tool.value !== 'Custom') {
                              setCustomTool('');
                            }
                          }}
                          className={`p-3 border rounded-lg text-center transition-all ${
                            selectedTool === tool.value
                              ? 'border-indigo-500 bg-indigo-50'
                              : 'border-gray-200 hover:border-gray-300'
                          }`}
                        >
                          <div className="text-2xl mb-1">{tool.icon}</div>
                          <div className="text-xs font-medium">{tool.label}</div>
                        </button>
                      ))}
                    </div>
                    {selectedTool === 'Custom' && (
                      <input
                        type="text"
                        value={customTool}
                        onChange={(e) => setCustomTool(e.target.value)}
                        placeholder="Enter custom action type..."
                        className="mt-2 block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                      />
                    )}
                  </div>
                )}

                {/* Person ID */}
                <div>
                  <label htmlFor="personId" className="block text-sm font-medium text-gray-900 mb-2">
                    Person ID *
                  </label>
                  {isAdmin ? (
                    <select
                      id="personId"
                      value={formData.personId}
                      onChange={(e) => setFormData({ ...formData, personId: e.target.value })}
                      required
                      className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                    >
                      <option value="">Select a person...</option>
                      {personIdsData?.map((p) => (
                        <option key={p.personId} value={p.personId}>
                          {p.displayName} ({p.personId})
                        </option>
                      ))}
                    </select>
                  ) : (
                    <input
                      type="text"
                      id="personId"
                      value={formData.personId}
                      disabled
                      required
                      className="block w-full rounded-md border-gray-300 shadow-sm bg-gray-100 text-gray-600 sm:text-sm cursor-not-allowed"
                      title="Your personId is fixed to your username"
                    />
                  )}
                  <p className="mt-1 text-xs text-gray-500">
                    Who is performing this action or expressing this intent?
                  </p>
                </div>

                {/* Timestamp */}
                <div>
                  <label htmlFor="timestampUtc" className="block text-sm font-medium text-gray-900 mb-2">
                    When did this happen? *
                  </label>
                  <div className="flex gap-2">
                    <input
                      type="datetime-local"
                      id="timestampUtc"
                      value={formData.timestampUtc}
                      onChange={(e) => handleTimestampChange(e.target.value)}
                      required
                      className="flex-1 rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                    />
                    <button
                      type="button"
                      onClick={() => {
                        const now = new Date();
                        handleTimestampChange(now.toISOString().slice(0, 16));
                      }}
                      className="px-4 py-2 bg-gray-100 text-gray-700 rounded-md hover:bg-gray-200 text-sm"
                    >
                      Now
                    </button>
                  </div>
                  <p className="mt-1 text-xs text-gray-500">
                    Detected: {formData.context.timeBucket} • {formData.context.dayType}
                  </p>
                </div>

                {/* Location */}
                <div>
                  <label htmlFor="location" className="block text-sm font-medium text-gray-900 mb-2">
                    Location <span className="text-gray-400">(optional)</span>
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
                    className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                    placeholder="e.g., living_room, bedroom, kitchen"
                  />
                </div>

                {/* Ignore for Learning Option */}
                {eventType === EventType.Action && (
                  <div className="flex items-center p-3 bg-gray-50 rounded-lg border border-gray-200">
                    <input
                      type="checkbox"
                      id="ignoreForLearning"
                      checked={ignoreForLearning}
                      onChange={(e) => setIgnoreForLearning(e.target.checked)}
                      className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500"
                    />
                    <label htmlFor="ignoreForLearning" className="ml-3 flex-1">
                      <div className="text-sm font-medium text-gray-900">Ignore for Learning</div>
                      <div className="text-xs text-gray-500">
                        Skip probability updates and routine learning for this event
                      </div>
                    </label>
                  </div>
                )}

                {/* Advanced Options */}
                <div>
                  <button
                    type="button"
                    onClick={() => setShowAdvanced(!showAdvanced)}
                    className="text-sm text-indigo-600 hover:text-indigo-800 flex items-center gap-2"
                  >
                    {showAdvanced ? '▼' : '▶'} Advanced Options
                  </button>
                  {showAdvanced && (
                    <div className="mt-4 space-y-4 p-4 bg-gray-50 rounded-lg">
                      <div>
                        <label htmlFor="probabilityValue" className="block text-sm font-medium text-gray-700 mb-1">
                          Probability Step Value
                        </label>
                        <input
                          type="number"
                          id="probabilityValue"
                          min="0"
                          max="1"
                          step="0.01"
                          value={formData.probabilityValue ?? ''}
                          onChange={(e) => setFormData({ ...formData, probabilityValue: e.target.value ? parseFloat(e.target.value) : undefined })}
                          className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                          placeholder="0.1"
                          disabled={ignoreForLearning}
                        />
                        <p className="mt-1 text-xs text-gray-500">
                          How much to change the confidence (0.0 to 1.0)
                        </p>
                      </div>
                      <div>
                        <label htmlFor="probabilityAction" className="block text-sm font-medium text-gray-700 mb-1">
                          Probability Action
                        </label>
                        <select
                          id="probabilityAction"
                          value={formData.probabilityAction ?? ''}
                          onChange={(e) => setFormData({ ...formData, probabilityAction: e.target.value ? (e.target.value as ProbabilityAction) : undefined })}
                          className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                          disabled={ignoreForLearning}
                        >
                          <option value="">None (auto-detect)</option>
                          <option value={ProbabilityAction.Increase}>Increase</option>
                          <option value={ProbabilityAction.Decrease}>Decrease</option>
                        </select>
                        <p className="mt-1 text-xs text-gray-500">
                          Whether to increase or decrease confidence (leave as auto-detect for most cases)
                        </p>
                      </div>
                    </div>
                  )}
                </div>

                {/* Submit Buttons */}
                <div className="flex gap-3 pt-4 border-t">
                  <button
                    type="submit"
                    disabled={createMutation.isPending || !formData.personId || !formData.actionType}
                    className="flex-1 px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed font-medium"
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

          {/* Live Preview Panel */}
          <div className="lg:col-span-1">
            <div className="bg-white shadow rounded-lg p-6 sticky top-6">
              <h2 className="text-lg font-semibold text-gray-900 mb-4">Live Preview</h2>
              
              {!formData.personId || !formData.actionType ? (
                <div className="text-center py-8 text-gray-400">
                  <div className="text-4xl mb-2">👆</div>
                  <p className="text-sm">Fill in the form to see what will happen</p>
                </div>
              ) : (
                <div className="space-y-4">
                  {/* Event Summary */}
                  <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
                    <div className="flex items-center gap-2 mb-2">
                      <span className="text-xl">
                        {eventType === EventType.StateChange ? '🎯' : '⚡'}
                      </span>
                      <div className="flex-1">
                        <div className="font-medium text-gray-900">{formData.actionType}</div>
                        <div className="text-xs text-gray-600">
                          {eventType === EventType.StateChange ? 'Intent' : 'Tool Execution'}
                        </div>
                      </div>
                    </div>
                    <div className="text-xs text-gray-600 mt-2">
                      <div>Person: {formData.personId}</div>
                      <div>Time: {new Date(formData.timestampUtc).toLocaleString()}</div>
                    </div>
                  </div>

                  {/* What Will Happen */}
                  {previewData && (
                    <>
                      {eventType === EventType.StateChange && (
                        <div>
                          <h3 className="text-sm font-medium text-gray-900 mb-2">Routine Impact</h3>
                          
                          {/* Warning about closing active windows */}
                          {activeRoutinesData && activeRoutinesData.items.length > 0 && (
                            <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg mb-2">
                              <div className="flex items-center gap-2 mb-1">
                                <span className="text-amber-600">⚠️</span>
                                <span className="text-sm font-medium text-amber-900">
                                  {activeRoutinesData.items.length} Active Window{activeRoutinesData.items.length !== 1 ? 's' : ''} Will Close
                                </span>
                              </div>
                              <p className="text-xs text-amber-700 mb-2">
                                All currently active routine learning windows for {formData.personId} will be closed. Only one routine learns at a time.
                              </p>
                              <div className="space-y-1">
                                {activeRoutinesData.items.map((r: RoutineDto) => (
                                  <div key={r.id} className="text-xs text-amber-800">
                                    • {r.intentType} routine window will close
                                  </div>
                                ))}
                              </div>
                            </div>
                          )}

                          {previewData.willCreateNewRoutine ? (
                            <div className="p-3 bg-green-50 border border-green-200 rounded-lg">
                              <div className="flex items-center gap-2 mb-1">
                                <span className="text-green-600">✨</span>
                                <span className="text-sm font-medium text-green-900">New Routine: {formData.actionType}</span>
                              </div>
                              <p className="text-xs text-green-700 mb-2">
                                A new routine will be created and an observation window will open for {formData.context.timeBucket}
                              </p>
                              <div className="text-xs text-green-600">
                                <div>• Learning window: 45 minutes</div>
                                <div>• Actions during window will be learned</div>
                                <div>• No existing routine reminders yet</div>
                              </div>
                            </div>
                          ) : previewData.willActivateRoutine && previewData.affectedRoutines.length > 0 ? (
                            <div className="space-y-2">
                              {previewData.affectedRoutines.map(routine => {
                                const isWindowOpen = routine.observationWindowEndsUtc 
                                  ? new Date(routine.observationWindowEndsUtc) > new Date()
                                  : false;
                                return (
                                  <div key={routine.id} className="p-3 bg-blue-50 border border-blue-200 rounded-lg">
                                    <div className="flex items-center justify-between mb-2">
                                      <div className="text-sm font-medium text-gray-900">
                                        {routine.intentType} Routine
                                      </div>
                                      {isWindowOpen && (
                                        <LearningBadge status="active" />
                                      )}
                                    </div>
                                    <p className="text-xs text-gray-600 mb-2">
                                      Observation window will reopen and learn from actions in the next 45 minutes
                                    </p>
                                    <div className="text-xs text-blue-600">
                                      <div>• Existing routine will be updated</div>
                                      <div>• Previous learning window will close</div>
                                      <div>• New window opens for this intent</div>
                                    </div>
                                  </div>
                                );
                              })}
                            </div>
                          ) : (
                            <div className="p-3 bg-gray-50 border border-gray-200 rounded-lg">
                              <div className="text-sm font-medium text-gray-900 mb-1">
                                {formData.actionType} Routine
                              </div>
                              <p className="text-xs text-gray-600">
                                Will create a new routine and start learning
                              </p>
                            </div>
                          )}
                        </div>
                      )}

                      {eventType === EventType.Action && (
                        <div>
                          <h3 className="text-sm font-medium text-gray-900 mb-2">Reminder Impact</h3>
                          {previewData.willCreateNewReminder ? (
                            <div className="p-3 bg-yellow-50 border border-yellow-200 rounded-lg">
                              <div className="flex items-center gap-2 mb-1">
                                <span className="text-yellow-600">🌱</span>
                                <span className="text-sm font-medium text-yellow-900">New Reminder</span>
                              </div>
                              <p className="text-xs text-yellow-700">
                                A new reminder will be created with low confidence (learning phase)
                              </p>
                            </div>
                          ) : previewData.affectedReminders.length > 0 ? (
                            <div className="space-y-2">
                              {previewData.affectedReminders.slice(0, 3).map(reminder => (
                                <div key={reminder.id} className="p-3 bg-gray-50 border border-gray-200 rounded-lg">
                                  <div className="flex items-center justify-between mb-2">
                                    <span className="text-sm font-medium text-gray-900">
                                      {reminder.suggestedAction}
                                    </span>
                                    <ConfidenceIndicator 
                                      confidence={reminder.confidence || 0} 
                                      size="sm" 
                                      showLabel={false}
                                    />
                                  </div>
                                  {formData.probabilityAction && (
                                    <div className="text-xs text-gray-600">
                                      Confidence will {formData.probabilityAction === ProbabilityAction.Increase ? 'increase' : 'decrease'} by {formData.probabilityValue || 0.1}
                                    </div>
                                  )}
                                </div>
                              ))}
                            </div>
                          ) : null}
                        </div>
                      )}

                      {/* Safety Warning */}
                      {previewData.affectedReminders.some((r: ReminderCandidateDto) => (r.confidence || 0) >= 0.7) && (
                        <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg">
                          <div className="flex items-center gap-2 mb-1">
                            <span className="text-amber-600">⚠️</span>
                            <span className="text-sm font-medium text-amber-900">High Confidence Reminder</span>
                          </div>
                          <p className="text-xs text-amber-700">
                            This event may trigger automatic execution of high-confidence reminders
                          </p>
                        </div>
                      )}
                    </>
                  )}

                  {/* Example Timeline */}
                  <div className="pt-4 border-t">
                    <h3 className="text-sm font-medium text-gray-900 mb-2">Timeline</h3>
                    <div className="space-y-2 text-xs">
                      <div className="flex items-center gap-2">
                        <div className="w-2 h-2 bg-indigo-500 rounded-full"></div>
                        <span className="text-gray-600">Event created</span>
                      </div>
                      {eventType === EventType.StateChange && (
                        <>
                          <div className="flex items-center gap-2">
                            <div className="w-2 h-2 bg-red-500 rounded-full"></div>
                            <span className="text-gray-600">All active learning windows close</span>
                          </div>
                          <div className="flex items-center gap-2">
                            <div className="w-2 h-2 bg-blue-500 rounded-full"></div>
                            <span className="text-gray-600">Observation window opens (45 min)</span>
                          </div>
                          <div className="flex items-center gap-2">
                            <div className="w-2 h-2 bg-green-500 rounded-full"></div>
                            <span className="text-gray-600">Actions during window are learned</span>
                          </div>
                        </>
                      )}
                      {eventType === EventType.Action && (
                        <>
                          {!ignoreForLearning ? (
                            <>
                              <div className="flex items-center gap-2">
                                <div className="w-2 h-2 bg-blue-500 rounded-full"></div>
                                <span className="text-gray-600">Pattern matching checked</span>
                              </div>
                              <div className="flex items-center gap-2">
                                <div className="w-2 h-2 bg-green-500 rounded-full"></div>
                                <span className="text-gray-600">Reminder confidence updated</span>
                              </div>
                            </>
                          ) : (
                            <div className="flex items-center gap-2">
                              <div className="w-2 h-2 bg-gray-400 rounded-full"></div>
                              <span className="text-gray-500">Learning skipped (ignored)</span>
                            </div>
                          )}
                        </>
                      )}
                    </div>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </Layout>
  );
}
