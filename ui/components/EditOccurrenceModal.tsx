'use client';

import React, { useState, useEffect } from 'react';

interface EditOccurrenceModalProps {
  isOpen: boolean;
  currentOccurrence: string | null | undefined;
  onClose: () => void;
  onSave: (occurrence: string | null) => Promise<void>;
}

type FrequencyType = 'daily' | 'weekly' | 'everyXDays' | 'weekdays' | 'weekends' | 'custom';

export function EditOccurrenceModal({ isOpen, currentOccurrence, onClose, onSave }: EditOccurrenceModalProps) {
  const [frequencyType, setFrequencyType] = useState<FrequencyType>('daily');
  const [customText, setCustomText] = useState('');
  const [everyXDays, setEveryXDays] = useState<number>(1);
  const [time, setTime] = useState('09:00');
  const [selectedDays, setSelectedDays] = useState<number[]>([]); // 0=Sunday, 6=Saturday
  const [isSaving, setIsSaving] = useState(false);

  const dayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

  // Parse current occurrence to populate form
  useEffect(() => {
    if (currentOccurrence) {
      const lower = currentOccurrence.toLowerCase();
      
      // Extract time if present
      const timeMatch = currentOccurrence.match(/\b(\d{1,2}):(\d{2})\b/);
      if (timeMatch) {
        setTime(`${timeMatch[1].padStart(2, '0')}:${timeMatch[2]}`);
      }

      // Parse frequency type
      if (lower.includes('daily') || lower.includes('every day')) {
        setFrequencyType('daily');
      } else if (lower.includes('weekdays') || lower.includes('weekday')) {
        setFrequencyType('weekdays');
      } else if (lower.includes('weekend')) {
        setFrequencyType('weekends');
      } else if (lower.includes('every') && lower.match(/\bevery\s+(\d+)\s+day/i)) {
        const match = lower.match(/\bevery\s+(\d+)\s+day/i);
        if (match) {
          setFrequencyType('everyXDays');
          setEveryXDays(parseInt(match[1]));
        }
      } else if (lower.includes('monday') || lower.includes('tuesday') || lower.includes('wednesday') || 
                 lower.includes('thursday') || lower.includes('friday') || lower.includes('saturday') || lower.includes('sunday')) {
        setFrequencyType('weekly');
        const days: number[] = [];
        if (lower.includes('sunday')) days.push(0);
        if (lower.includes('monday')) days.push(1);
        if (lower.includes('tuesday')) days.push(2);
        if (lower.includes('wednesday')) days.push(3);
        if (lower.includes('thursday')) days.push(4);
        if (lower.includes('friday')) days.push(5);
        if (lower.includes('saturday')) days.push(6);
        setSelectedDays(days);
      } else {
        setFrequencyType('custom');
        setCustomText(currentOccurrence);
      }
    } else {
      // Default values
      setFrequencyType('daily');
      setTime('09:00');
      setEveryXDays(1);
      setSelectedDays([]);
      setCustomText('');
    }
  }, [currentOccurrence, isOpen]);

  const toggleDay = (day: number) => {
    setSelectedDays(prev => 
      prev.includes(day) 
        ? prev.filter(d => d !== day)
        : [...prev, day].sort()
    );
  };

  const generateOccurrenceString = (): string => {
    const timeStr = time ? ` at ${time}` : '';
    
    switch (frequencyType) {
      case 'daily':
        return `daily${timeStr}`;
      case 'weekdays':
        return `weekdays${timeStr}`;
      case 'weekends':
        return `weekends${timeStr}`;
      case 'everyXDays':
        return `every ${everyXDays} day${everyXDays !== 1 ? 's' : ''}${timeStr}`;
      case 'weekly':
        if (selectedDays.length === 0) {
          return customText || 'weekly';
        }
        const dayNamesList = selectedDays.map(d => dayNames[d]).join(', ');
        return `every ${dayNamesList}${timeStr}`;
      case 'custom':
        return customText;
      default:
        return '';
    }
  };

  const handleSave = async () => {
    setIsSaving(true);
    try {
      const occurrence = generateOccurrenceString();
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

        <div className="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-2xl sm:w-full">
          <div className="bg-white px-4 pt-5 pb-4 sm:p-6 sm:pb-4">
            <div className="flex justify-between items-center mb-6">
              <h3 className="text-xl leading-6 font-semibold text-gray-900">
                Edit Reminder Occurrence Pattern
              </h3>
              <button
                onClick={onClose}
                className="text-gray-400 hover:text-gray-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 rounded-md p-1"
              >
                <span className="sr-only">Close</span>
                <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>

            <div className="space-y-6">
              {/* Frequency Type Selection */}
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-3">
                  Frequency
                  <span className="ml-1 text-gray-400" title="How often should this reminder occur?">ℹ️</span>
                </label>
                <div className="grid grid-cols-2 gap-3">
                  <button
                    type="button"
                    onClick={() => setFrequencyType('daily')}
                    className={`px-4 py-3 rounded-lg border-2 text-left transition-all ${
                      frequencyType === 'daily'
                        ? 'border-indigo-500 bg-indigo-50 text-indigo-900'
                        : 'border-gray-200 bg-white text-gray-700 hover:border-gray-300'
                    }`}
                  >
                    <div className="font-medium">Daily</div>
                    <div className="text-xs text-gray-500 mt-1">Every day</div>
                  </button>
                  
                  <button
                    type="button"
                    onClick={() => setFrequencyType('weekdays')}
                    className={`px-4 py-3 rounded-lg border-2 text-left transition-all ${
                      frequencyType === 'weekdays'
                        ? 'border-indigo-500 bg-indigo-50 text-indigo-900'
                        : 'border-gray-200 bg-white text-gray-700 hover:border-gray-300'
                    }`}
                  >
                    <div className="font-medium">Weekdays</div>
                    <div className="text-xs text-gray-500 mt-1">Mon - Fri</div>
                  </button>
                  
                  <button
                    type="button"
                    onClick={() => setFrequencyType('weekends')}
                    className={`px-4 py-3 rounded-lg border-2 text-left transition-all ${
                      frequencyType === 'weekends'
                        ? 'border-indigo-500 bg-indigo-50 text-indigo-900'
                        : 'border-gray-200 bg-white text-gray-700 hover:border-gray-300'
                    }`}
                  >
                    <div className="font-medium">Weekends</div>
                    <div className="text-xs text-gray-500 mt-1">Sat - Sun</div>
                  </button>
                  
                  <button
                    type="button"
                    onClick={() => setFrequencyType('everyXDays')}
                    className={`px-4 py-3 rounded-lg border-2 text-left transition-all ${
                      frequencyType === 'everyXDays'
                        ? 'border-indigo-500 bg-indigo-50 text-indigo-900'
                        : 'border-gray-200 bg-white text-gray-700 hover:border-gray-300'
                    }`}
                  >
                    <div className="font-medium">Every X Days</div>
                    <div className="text-xs text-gray-500 mt-1">Custom interval</div>
                  </button>
                  
                  <button
                    type="button"
                    onClick={() => setFrequencyType('weekly')}
                    className={`px-4 py-3 rounded-lg border-2 text-left transition-all ${
                      frequencyType === 'weekly'
                        ? 'border-indigo-500 bg-indigo-50 text-indigo-900'
                        : 'border-gray-200 bg-white text-gray-700 hover:border-gray-300'
                    }`}
                  >
                    <div className="font-medium">Weekly</div>
                    <div className="text-xs text-gray-500 mt-1">Select days</div>
                  </button>
                  
                  <button
                    type="button"
                    onClick={() => setFrequencyType('custom')}
                    className={`px-4 py-3 rounded-lg border-2 text-left transition-all ${
                      frequencyType === 'custom'
                        ? 'border-indigo-500 bg-indigo-50 text-indigo-900'
                        : 'border-gray-200 bg-white text-gray-700 hover:border-gray-300'
                    }`}
                  >
                    <div className="font-medium">Custom</div>
                    <div className="text-xs text-gray-500 mt-1">Free text</div>
                  </button>
                </div>
              </div>

              {/* Every X Days Input */}
              {frequencyType === 'everyXDays' && (
                <div>
                  <label htmlFor="everyXDays" className="block text-sm font-medium text-gray-700 mb-2">
                    Repeat every X days
                  </label>
                  <div className="flex items-center gap-3">
                    <input
                      type="number"
                      id="everyXDays"
                      min="1"
                      max="365"
                      value={everyXDays}
                      onChange={(e) => setEveryXDays(parseInt(e.target.value) || 1)}
                      className="block w-24 rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                    />
                    <span className="text-sm text-gray-600">day{everyXDays !== 1 ? 's' : ''}</span>
                  </div>
                </div>
              )}

              {/* Weekly Day Selection */}
              {frequencyType === 'weekly' && (
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-3">
                    Select Days
                    <span className="ml-1 text-gray-400" title="Choose which days of the week">ℹ️</span>
                  </label>
                  <div className="flex flex-wrap gap-2">
                    {dayNames.map((day, index) => (
                      <button
                        key={index}
                        type="button"
                        onClick={() => toggleDay(index)}
                        className={`px-4 py-2 rounded-lg border-2 text-sm font-medium transition-all ${
                          selectedDays.includes(index)
                            ? 'border-indigo-500 bg-indigo-500 text-white'
                            : 'border-gray-200 bg-white text-gray-700 hover:border-gray-300'
                        }`}
                      >
                        {day}
                      </button>
                    ))}
                  </div>
                  {selectedDays.length === 0 && (
                    <p className="text-xs text-yellow-600 mt-2">⚠️ Please select at least one day</p>
                  )}
                </div>
              )}

              {/* Custom Text Input */}
              {frequencyType === 'custom' && (
                <div>
                  <label htmlFor="customText" className="block text-sm font-medium text-gray-700 mb-2">
                    Custom Pattern
                    <span className="ml-1 text-gray-400" title="Enter any text pattern">ℹ️</span>
                  </label>
                  <input
                    type="text"
                    id="customText"
                    value={customText}
                    onChange={(e) => setCustomText(e.target.value)}
                    className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                    placeholder="e.g., every 2 weeks, first Monday of month"
                  />
                </div>
              )}

              {/* Time Selection */}
              {frequencyType !== 'custom' && (
                <div>
                  <label htmlFor="time" className="block text-sm font-medium text-gray-700 mb-2">
                    Time
                    <span className="ml-1 text-gray-400" title="What time should the reminder execute?">ℹ️</span>
                  </label>
                  <input
                    type="time"
                    id="time"
                    value={time}
                    onChange={(e) => setTime(e.target.value)}
                    className="block w-full max-w-xs rounded-md border-gray-300 shadow-sm focus:border-indigo-500 focus:ring-indigo-500 sm:text-sm"
                  />
                </div>
              )}

              {/* Preview */}
              <div className="bg-gray-50 border border-gray-200 rounded-lg p-4">
                <div className="text-xs font-medium text-gray-500 mb-1">Preview:</div>
                <div className="text-sm font-medium text-gray-900">
                  {generateOccurrenceString() || 'No pattern set'}
                </div>
              </div>
            </div>
          </div>

          <div className="bg-gray-50 px-4 py-3 sm:px-6 sm:flex sm:flex-row-reverse">
            <button
              type="button"
              onClick={handleSave}
              disabled={isSaving || (frequencyType === 'weekly' && selectedDays.length === 0)}
              className="w-full inline-flex justify-center items-center gap-2 rounded-md border border-transparent shadow-sm px-4 py-2 bg-indigo-600 text-base font-medium text-white hover:bg-indigo-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:ml-3 sm:w-auto sm:text-sm disabled:opacity-50 disabled:cursor-not-allowed"
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