// Component for displaying delay learning statistics
'use client';

import React from 'react';

interface DelayStatsDisplayProps {
  medianDelaySeconds?: number | null;
  p90DelaySeconds?: number | null;
  sampleCount: number;
  evidenceCount: number;
  emaDelaySeconds?: number | null;
  lastUpdatedUtc?: string | null;
  className?: string;
}

export function DelayStatsDisplay({
  medianDelaySeconds,
  p90DelaySeconds,
  sampleCount,
  evidenceCount,
  emaDelaySeconds,
  lastUpdatedUtc,
  className = '',
}: DelayStatsDisplayProps) {
  const formatDelay = (seconds: number | null | undefined): string => {
    if (!seconds && seconds !== 0) return 'N/A';
    if (seconds < 60) return `${Math.round(seconds)}s`;
    if (seconds < 3600) return `${Math.round(seconds / 60)}m`;
    return `${Math.round(seconds / 3600)}h`;
  };

  const hasData = sampleCount > 0 || evidenceCount > 0;

  if (!hasData) {
    return (
      <div className={`text-xs text-gray-500 ${className}`}>
        <span className="inline-flex items-center gap-1">
          <span>📊</span>
          <span>No delay data yet</span>
        </span>
      </div>
    );
  }

  return (
    <div className={`space-y-1.5 ${className}`}>
      <div className="text-xs font-medium text-gray-700 flex items-center gap-1">
        <span>📊</span>
        <span>Delay Statistics</span>
      </div>
      
      <div className="grid grid-cols-2 gap-2 text-xs">
        {medianDelaySeconds !== null && medianDelaySeconds !== undefined ? (
          <div className="bg-blue-50 rounded px-2 py-1 border border-blue-200">
            <div className="text-blue-600 font-medium">Median</div>
            <div className="text-blue-800 text-sm font-semibold">{formatDelay(medianDelaySeconds)}</div>
          </div>
        ) : emaDelaySeconds !== null && emaDelaySeconds !== undefined ? (
          <div className="bg-blue-50 rounded px-2 py-1 border border-blue-200">
            <div className="text-blue-600 font-medium">Avg (EMA)</div>
            <div className="text-blue-800 text-sm font-semibold">{formatDelay(emaDelaySeconds)}</div>
          </div>
        ) : null}
        
        {p90DelaySeconds !== null && p90DelaySeconds !== undefined && (
          <div className="bg-purple-50 rounded px-2 py-1 border border-purple-200">
            <div className="text-purple-600 font-medium">P90</div>
            <div className="text-purple-800 text-sm font-semibold">{formatDelay(p90DelaySeconds)}</div>
          </div>
        )}
      </div>

      <div className="flex items-center justify-between text-xs text-gray-600 pt-1 border-t border-gray-200">
        <div className="flex items-center gap-2">
          <span>📈 {Math.round(sampleCount)} samples</span>
          {evidenceCount > 0 && (
            <span className="text-gray-500">• {evidenceCount} evidence</span>
          )}
        </div>
        {lastUpdatedUtc && (
          <span className="text-gray-500" title={new Date(lastUpdatedUtc).toLocaleString()}>
            Updated {new Date(lastUpdatedUtc).toLocaleDateString()}
          </span>
        )}
      </div>
    </div>
  );
}

