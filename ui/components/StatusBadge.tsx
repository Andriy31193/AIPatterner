// Component for displaying status with color coding
import React from 'react';
import { ReminderCandidateStatus } from '@/types';

interface StatusBadgeProps {
  status: ReminderCandidateStatus;
}

export function StatusBadge({ status }: StatusBadgeProps) {
  const colorClasses = {
    [ReminderCandidateStatus.Scheduled]: 'bg-blue-100 text-blue-800',
    [ReminderCandidateStatus.Executed]: 'bg-green-100 text-green-800',
    [ReminderCandidateStatus.Skipped]: 'bg-gray-100 text-gray-800',
    [ReminderCandidateStatus.Expired]: 'bg-red-100 text-red-800',
  };

  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${colorClasses[status]}`}>
      {status}
    </span>
  );
}

