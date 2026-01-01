// Component for displaying confidence/probability with color coding
'use client';

import React from 'react';

interface ConfidenceBadgeProps {
  confidence: number;
  threshold?: number;
}

export function ConfidenceBadge({ confidence, threshold = 0.7 }: ConfidenceBadgeProps) {
  const percentage = Math.round(confidence * 100);
  const isHigh = confidence >= threshold;
  const isMedium = confidence >= 0.4 && confidence < threshold;
  const isLow = confidence < 0.4;

  // Color coding: high = green, medium = yellow, low = red
  const bgColor = isHigh ? 'bg-green-100' : isMedium ? 'bg-yellow-100' : 'bg-red-100';
  const textColor = isHigh ? 'text-green-800' : isMedium ? 'text-yellow-800' : 'text-red-800';
  const borderColor = isHigh ? 'border-green-300' : isMedium ? 'border-yellow-300' : 'border-red-300';

  return (
    <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${bgColor} ${textColor} border ${borderColor}`}>
      {percentage}%
    </span>
  );
}
