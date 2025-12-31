// Component for displaying dates in human-friendly format
import React from 'react';
import { format, formatDistanceToNow } from 'date-fns';

interface DateTimeDisplayProps {
  date: string | Date;
  showRelative?: boolean;
}

export function DateTimeDisplay({ date, showRelative = false }: DateTimeDisplayProps) {
  const dateObj = typeof date === 'string' ? new Date(date) : date;

  return (
    <span title={format(dateObj, 'PPpp')}>
      {showRelative ? formatDistanceToNow(dateObj, { addSuffix: true }) : format(dateObj, 'PPp')}
    </span>
  );
}

