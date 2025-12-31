// Unit test for ConfidenceBadge component
import React from 'react';
import { render, screen } from '@testing-library/react';
import { ConfidenceBadge } from '../ConfidenceBadge';
import { ConfidenceLevel } from '@/types';

describe('ConfidenceBadge', () => {
  it('renders confidence level correctly', () => {
    render(<ConfidenceBadge level={ConfidenceLevel.High} />);
    expect(screen.getByText('high')).toBeInTheDocument();
  });

  it('displays percentage when provided', () => {
    render(<ConfidenceBadge level={ConfidenceLevel.Medium} percent={65.5} />);
    expect(screen.getByText(/medium.*66%/i)).toBeInTheDocument();
  });
});

