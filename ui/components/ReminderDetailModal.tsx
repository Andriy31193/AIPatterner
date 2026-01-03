'use client';

import React, { useEffect, useState } from 'react';
import { ConfidenceBadge } from './ConfidenceBadge';
import { StatusBadge } from './StatusBadge';
import { DateTimeDisplay } from './DateTimeDisplay';
import type { ReminderCandidateDto } from '@/types';

interface ReminderDetailModalProps {
  reminder: ReminderCandidateDto | null;
  isOpen: boolean;
  onClose: () => void;
  confidenceThreshold?: number;
}

interface Condition {
  type: 'state' | 'time' | 'location' | 'people' | 'other';
  name: string;
  value: string;
  operator?: string;
  description: string;
}

export function ReminderDetailModal({ 
  reminder, 
  isOpen, 
  onClose,
  confidenceThreshold = 0.7 
}: ReminderDetailModalProps) {
  const [showAdvanced, setShowAdvanced] = useState(false);

  // Close on ESC key
  useEffect(() => {
    const handleEsc = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && isOpen) {
        onClose();
      }
    };
    window.addEventListener('keydown', handleEsc);
    return () => window.removeEventListener('keydown', handleEsc);
  }, [isOpen, onClose]);

  if (!isOpen || !reminder) return null;

  // Extract conditions from customData
  const extractConditions = (): Condition[] => {
    const conditions: Condition[] = [];
    
    if (reminder.customData) {
      // All customData entries are potential conditions
      // State signals are typically key-value pairs representing system state
      Object.entries(reminder.customData).forEach(([key, value]) => {
        // Determine type based on key patterns
        let conditionType: 'state' | 'time' | 'location' | 'people' | 'other' = 'other';
        
        if (key.toLowerCase().includes('state') || 
            key.toLowerCase().includes('signal') ||
            key.toLowerCase().includes('device') ||
            key.toLowerCase().includes('music') ||
            key.toLowerCase().includes('light') ||
            key.toLowerCase().includes('temperature')) {
          conditionType = 'state';
        } else if (key.toLowerCase().includes('location') || 
                   key.toLowerCase().includes('home') ||
                   key.toLowerCase().includes('room')) {
          conditionType = 'location';
        } else if (key.toLowerCase().includes('people') || 
                   key.toLowerCase().includes('person') ||
                   key.toLowerCase().includes('present')) {
          conditionType = 'people';
        } else if (key.toLowerCase().includes('time') || 
                   key.toLowerCase().includes('hour') ||
                   key.toLowerCase().includes('minute')) {
          conditionType = 'time';
        }
        
        // Default to 'state' if it's not clearly another type (most customData is state signals)
        if (conditionType === 'other' && value && value.length < 50) {
          conditionType = 'state';
        }
        
        conditions.push({
          type: conditionType,
          name: formatConditionName(key),
          value: formatConditionValue(value),
          description: conditionType === 'state' 
            ? `State signal: ${formatConditionName(key)} is ${formatConditionValue(value)}`
            : `${formatConditionName(key)}: ${formatConditionValue(value)}`
        });
      });
    }

    // Time constraints from occurrence
    if (reminder.occurrence) {
      const timeMatch = reminder.occurrence.match(/at\s+(\d{1,2}:\d{2})/i);
      if (timeMatch) {
        conditions.push({
          type: 'time',
          name: 'Time',
          value: timeMatch[1],
          description: `Scheduled to occur at ${timeMatch[1]}`
        });
      }
    }

    return conditions;
  };

  const formatConditionName = (key: string): string => {
    // Convert camelCase or snake_case to Title Case
    return key
      .replace(/([A-Z])/g, ' $1')
      .replace(/_/g, ' ')
      .replace(/^\w/, c => c.toUpperCase())
      .trim();
  };

  const formatConditionValue = (value: string): string => {
    // Capitalize first letter and handle common values
    const formatted = value.charAt(0).toUpperCase() + value.slice(1);
    return formatted;
  };

  const conditions = extractConditions();
  const hasConditions = conditions.length > 0;

  // Generate human-readable condition explanation
  const getConditionExplanation = (): string => {
    if (!hasConditions) {
      return 'This reminder has no specific conditions and will be considered based on timing alone.';
    }

    const stateConditions = conditions.filter(c => c.type === 'state');
    const timeConditions = conditions.filter(c => c.type === 'time');
    
    const parts: string[] = [];
    
    if (stateConditions.length > 0) {
      const stateList = stateConditions.map(c => `${c.name} is ${c.value}`).join(' and ');
      parts.push(`This reminder will only be considered when ${stateList}.`);
    }
    
    if (timeConditions.length > 0) {
      parts.push(`It is scheduled to occur at the specified time.`);
    }
    
    return parts.join(' ') || 'This reminder has specific conditions that must be met.';
  };

  // Get learning status
  const getLearningStatus = (): string => {
    if (reminder.confidence < 0.5) {
      return 'Still learning - needs more evidence';
    } else if (reminder.confidence < confidenceThreshold) {
      return 'Learning - gaining confidence';
    } else {
      return 'Stable pattern - high confidence';
    }
  };

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto">
      <div 
        className="fixed inset-0 bg-gray-500 bg-opacity-75 transition-opacity"
        onClick={onClose}
        aria-hidden="true"
      ></div>

      <div className="flex items-center justify-center min-h-screen pt-4 px-4 pb-20 text-center sm:block sm:p-0">
        <div 
          className="inline-block align-bottom bg-white rounded-lg text-left overflow-hidden shadow-xl transform transition-all sm:my-8 sm:align-middle sm:max-w-2xl sm:w-full"
          onClick={(e) => e.stopPropagation()}
        >
          {/* Header */}
          <div className="bg-white px-6 pt-6 pb-4 border-b border-gray-200">
            <div className="flex justify-between items-start">
              <div className="flex-1">
                <h3 className="text-2xl font-semibold text-gray-900 mb-2">
                  {reminder.suggestedAction}
                </h3>
                <p className="text-sm text-gray-500">{reminder.personId}</p>
              </div>
              <button
                onClick={onClose}
                className="text-gray-400 hover:text-gray-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 rounded-md p-1"
                aria-label="Close modal"
              >
                <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
          </div>

          {/* Content */}
          <div className="bg-white px-6 py-4 max-h-[calc(100vh-200px)] overflow-y-auto">
            {/* Section 1: Reminder Summary */}
            <div className="mb-6">
              <h4 className="text-lg font-medium text-gray-900 mb-4">Summary</h4>
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-sm text-gray-600">Probability</span>
                  <ConfidenceBadge confidence={reminder.confidence || 0} threshold={confidenceThreshold} />
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-sm text-gray-600">Status</span>
                  <StatusBadge status={reminder.status} />
                </div>
                <div className="flex items-center justify-between">
                  <span className="text-sm text-gray-600">Scheduled Time</span>
                  <span className="text-sm text-gray-900">
                    <DateTimeDisplay date={reminder.checkAtUtc} />
                  </span>
                </div>
                {reminder.occurrence && (
                  <div className="flex items-start justify-between">
                    <span className="text-sm text-gray-600">Occurrence Pattern</span>
                    <span className="text-sm text-gray-900 text-right max-w-xs">
                      {reminder.occurrence}
                    </span>
                  </div>
                )}
              </div>
            </div>

            {/* Section 2: Conditions */}
            {hasConditions && (
              <div className="mb-6 border-t border-gray-200 pt-6">
                <h4 className="text-lg font-medium text-gray-900 mb-4">Conditions</h4>
                <div className="space-y-3">
                  {conditions.map((condition, index) => (
                    <div key={index} className="bg-gray-50 rounded-lg p-3">
                      <div className="flex items-start justify-between mb-1">
                        <span className="text-sm font-medium text-gray-900">{condition.name}</span>
                        <span className="text-sm text-gray-600">{condition.value}</span>
                      </div>
                      <p className="text-xs text-gray-500 mt-1">{condition.description}</p>
                    </div>
                  ))}
                </div>
                <div className="mt-4 p-3 bg-blue-50 rounded-lg">
                  <p className="text-sm text-blue-800">
                    <strong>Meaning:</strong> {getConditionExplanation()}
                  </p>
                </div>
              </div>
            )}

            {/* Section 3: Occurrence & Learning Info */}
            <div className="mb-6 border-t border-gray-200 pt-6">
              <h4 className="text-lg font-medium text-gray-900 mb-4">Learning & Pattern</h4>
              <div className="space-y-3">
                {reminder.occurrence && (
                  <div>
                    <span className="text-sm font-medium text-gray-600">Occurrence Rule</span>
                    <p className="text-sm text-gray-900 mt-1">{reminder.occurrence}</p>
                  </div>
                )}
                <div>
                  <span className="text-sm font-medium text-gray-600">Learning Status</span>
                  <p className="text-sm text-gray-900 mt-1">{getLearningStatus()}</p>
                </div>
                {reminder.sourceEventId && (
                  <div>
                    <span className="text-sm font-medium text-gray-600">Source Event</span>
                    <p className="text-sm text-gray-500 mt-1 font-mono">{reminder.sourceEventId}</p>
                  </div>
                )}
              </div>
            </div>

            {/* Section 4: Advanced (Collapsible) */}
            <div className="border-t border-gray-200 pt-6">
              <button
                onClick={() => setShowAdvanced(!showAdvanced)}
                className="flex items-center justify-between w-full text-left focus:outline-none focus:ring-2 focus:ring-indigo-500 rounded-md p-2 hover:bg-gray-50"
                aria-expanded={showAdvanced}
              >
                <h4 className="text-lg font-medium text-gray-900">Advanced</h4>
                <svg
                  className={`h-5 w-5 text-gray-500 transform transition-transform ${showAdvanced ? 'rotate-180' : ''}`}
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke="currentColor"
                >
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
                </svg>
              </button>
              
              {showAdvanced && (
                <div className="mt-4 space-y-3">
                  <div>
                    <span className="text-sm font-medium text-gray-600">Raw Probability Value</span>
                    <p className="text-sm text-gray-900 mt-1 font-mono">
                      {(reminder.confidence || 0).toFixed(4)}
                    </p>
                  </div>
                  <div>
                    <span className="text-sm font-medium text-gray-600">Reminder ID</span>
                    <p className="text-sm text-gray-500 mt-1 font-mono break-all">{reminder.id}</p>
                  </div>
                  {reminder.transitionId && (
                    <div>
                      <span className="text-sm font-medium text-gray-600">Transition ID</span>
                      <p className="text-sm text-gray-500 mt-1 font-mono">{reminder.transitionId}</p>
                    </div>
                  )}
                  {reminder.customData && Object.keys(reminder.customData).length > 0 && (
                    <div>
                      <span className="text-sm font-medium text-gray-600">Custom Data</span>
                      <pre className="text-xs text-gray-500 mt-1 bg-gray-50 p-2 rounded overflow-auto">
                        {JSON.stringify(reminder.customData, null, 2)}
                      </pre>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>

          {/* Footer */}
          <div className="bg-gray-50 px-6 py-4 border-t border-gray-200">
            <button
              type="button"
              onClick={onClose}
              className="w-full inline-flex justify-center items-center rounded-md border border-gray-300 shadow-sm px-4 py-2 bg-white text-base font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-indigo-500 sm:text-sm"
            >
              Close
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

