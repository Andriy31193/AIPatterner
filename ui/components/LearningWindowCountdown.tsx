// Component for displaying learning window countdown
'use client';

import React, { useState, useEffect } from 'react';

interface LearningWindowCountdownProps {
  windowEndsUtc?: string;
  windowStartUtc?: string;
  className?: string;
}

export function LearningWindowCountdown({ 
  windowEndsUtc, 
  windowStartUtc,
  className = '' 
}: LearningWindowCountdownProps) {
  const [timeRemaining, setTimeRemaining] = useState<string>('');
  const [isActive, setIsActive] = useState(false);

  useEffect(() => {
    if (!windowEndsUtc) {
      setIsActive(false);
      setTimeRemaining('');
      return;
    }

    const updateCountdown = () => {
      const now = new Date();
      const end = new Date(windowEndsUtc);
      const diff = end.getTime() - now.getTime();

      if (diff <= 0) {
        setIsActive(false);
        setTimeRemaining('Window closed');
        return;
      }

      setIsActive(true);
      
      const minutes = Math.floor(diff / 60000);
      const seconds = Math.floor((diff % 60000) / 1000);
      
      if (minutes > 0) {
        setTimeRemaining(`${minutes}m ${seconds}s`);
      } else {
        setTimeRemaining(`${seconds}s`);
      }
    };

    updateCountdown();
    const interval = setInterval(updateCountdown, 1000);

    return () => clearInterval(interval);
  }, [windowEndsUtc]);

  if (!windowEndsUtc) {
    return null;
  }

  if (!isActive) {
    return (
      <div className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-gray-100 text-gray-600 ${className}`}>
        <span>⏸️</span>
        <span>Learning window closed</span>
      </div>
    );
  }

  const isUrgent = timeRemaining.includes('s') && !timeRemaining.includes('m');

  return (
    <div className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium border ${
      isUrgent 
        ? 'bg-red-50 text-red-700 border-red-200' 
        : 'bg-blue-50 text-blue-700 border-blue-200'
    } ${className}`}>
      <span className={isUrgent ? 'animate-pulse' : ''}>⏱️</span>
      <span>Learning: {timeRemaining} left</span>
    </div>
  );
}

