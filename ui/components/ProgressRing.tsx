// Progress ring component for visual confidence display
'use client';

import React from 'react';

interface ProgressRingProps {
  value: number; // 0.0 to 1.0
  size?: number;
  strokeWidth?: number;
  className?: string;
  color?: 'green' | 'yellow' | 'gray' | 'blue';
}

export function ProgressRing({ 
  value, 
  size = 60, 
  strokeWidth = 6,
  className = '',
  color = 'green'
}: ProgressRingProps) {
  const radius = (size - strokeWidth) / 2;
  const circumference = radius * 2 * Math.PI;
  const offset = circumference - (value * circumference);

  const colorClasses = {
    green: 'stroke-green-500',
    yellow: 'stroke-yellow-500',
    gray: 'stroke-gray-400',
    blue: 'stroke-blue-500',
  };

  return (
    <div className={`relative inline-flex items-center justify-center ${className}`}>
      <svg width={size} height={size} className="transform -rotate-90">
        {/* Background circle */}
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke="currentColor"
          strokeWidth={strokeWidth}
          fill="none"
          className="text-gray-200"
        />
        {/* Progress circle */}
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke="currentColor"
          strokeWidth={strokeWidth}
          fill="none"
          strokeDasharray={circumference}
          strokeDashoffset={offset}
          strokeLinecap="round"
          className={colorClasses[color]}
          style={{ transition: 'stroke-dashoffset 0.5s ease-in-out' }}
        />
      </svg>
      <span className="absolute text-xs font-medium text-gray-700">
        {Math.round(value * 100)}%
      </span>
    </div>
  );
}

