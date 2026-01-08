// Badge component for time context bucket
'use client';

import React from 'react';

interface TimeContextBucketBadgeProps {
  bucket?: string | null;
  className?: string;
}

export function TimeContextBucketBadge({ bucket, className = '' }: TimeContextBucketBadgeProps) {
  if (!bucket) {
    return null;
  }

  const config: Record<string, { label: string; icon: string; bg: string; text: string; border: string }> = {
    morning: {
      label: 'Morning',
      icon: '🌅',
      bg: 'bg-yellow-50',
      text: 'text-yellow-700',
      border: 'border-yellow-200',
    },
    afternoon: {
      label: 'Afternoon',
      icon: '☀️',
      bg: 'bg-orange-50',
      text: 'text-orange-700',
      border: 'border-orange-200',
    },
    evening: {
      label: 'Evening',
      icon: '🌆',
      bg: 'bg-purple-50',
      text: 'text-purple-700',
      border: 'border-purple-200',
    },
    night: {
      label: 'Night',
      icon: '🌙',
      bg: 'bg-indigo-50',
      text: 'text-indigo-700',
      border: 'border-indigo-200',
    },
  };

  const bucketLower = bucket.toLowerCase();
  const matched = Object.keys(config).find(k => bucketLower.includes(k));
  const { label, icon, bg, text, border } = matched ? config[matched] : {
    label: bucket,
    icon: '🕐',
    bg: 'bg-gray-50',
    text: 'text-gray-700',
    border: 'border-gray-200',
  };

  return (
    <span className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium border ${bg} ${text} ${border} ${className}`}>
      <span>{icon}</span>
      <span>{label}</span>
    </span>
  );
}

