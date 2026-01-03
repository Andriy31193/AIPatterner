// Badge component for learning status
'use client';

import React from 'react';

interface LearningBadgeProps {
  status: 'learning' | 'ready' | 'active' | 'paused';
  className?: string;
}

export function LearningBadge({ status, className = '' }: LearningBadgeProps) {
  const config = {
    learning: {
      label: 'Still learning',
      icon: '🌱',
      bg: 'bg-blue-50',
      text: 'text-blue-700',
      border: 'border-blue-200',
    },
    ready: {
      label: 'Ready',
      icon: '✨',
      bg: 'bg-green-50',
      text: 'text-green-700',
      border: 'border-green-200',
    },
    active: {
      label: 'Active',
      icon: '🟢',
      bg: 'bg-emerald-50',
      text: 'text-emerald-700',
      border: 'border-emerald-200',
    },
    paused: {
      label: 'Paused',
      icon: '⏸️',
      bg: 'bg-gray-50',
      text: 'text-gray-700',
      border: 'border-gray-200',
    },
  };

  const { label, icon, bg, text, border } = config[status];

  return (
    <span className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-medium border ${bg} ${text} ${border} ${className}`}>
      <span>{icon}</span>
      <span>{label}</span>
    </span>
  );
}

