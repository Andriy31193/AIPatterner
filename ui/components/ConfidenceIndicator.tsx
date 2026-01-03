// Visual confidence indicator with soft language
'use client';

import React from 'react';
import { ProgressRing } from './ProgressRing';

interface ConfidenceIndicatorProps {
  confidence: number; // 0.0 to 1.0
  threshold?: number;
  showLabel?: boolean;
  size?: 'sm' | 'md' | 'lg';
}

export function ConfidenceIndicator({ 
  confidence, 
  threshold = 0.7,
  showLabel = true,
  size = 'md'
}: ConfidenceIndicatorProps) {
  const getConfidenceLevel = (conf: number): 'high' | 'medium' | 'low' | 'learning' => {
    if (conf >= threshold) return 'high';
    if (conf >= 0.4) return 'medium';
    if (conf > 0) return 'low';
    return 'learning';
  };

  const getLabel = (level: string): string => {
    switch (level) {
      case 'high': return 'Ready';
      case 'medium': return 'Learning';
      case 'low': return 'Still learning';
      default: return 'Just started';
    }
  };

  const getColor = (level: string): 'green' | 'yellow' | 'gray' | 'blue' => {
    switch (level) {
      case 'high': return 'green';
      case 'medium': return 'yellow';
      case 'low': return 'gray';
      default: return 'blue';
    }
  };

  const getSize = (size: string): number => {
    switch (size) {
      case 'sm': return 40;
      case 'lg': return 80;
      default: return 60;
    }
  };

  const level = getConfidenceLevel(confidence);
  const color = getColor(level);
  const ringSize = getSize(size);

  return (
    <div className="flex flex-col items-center gap-2">
      <ProgressRing value={confidence} size={ringSize} color={color} />
      {showLabel && (
        <span className={`text-xs font-medium ${
          level === 'high' ? 'text-green-700' :
          level === 'medium' ? 'text-yellow-700' :
          'text-gray-600'
        }`}>
          {getLabel(level)}
        </span>
      )}
    </div>
  );
}

