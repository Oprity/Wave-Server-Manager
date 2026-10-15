// AC Server Manager — AGPL-3.0-or-later (see LICENSE at repo root).

import { describe, expect, it } from 'vitest';
import { isValidContentName, joinCarsCsv, parseCarsCsv, validateProfileFields } from './cars';

describe('parseCarsCsv', () => {
  it('splits and trims', () => {
    expect(parseCarsCsv('car_a, car_b ,,')).toEqual(['car_a', 'car_b']);
  });

  it('handles empty input', () => {
    expect(parseCarsCsv('')).toEqual([]);
  });
});

describe('joinCarsCsv', () => {
  it('normalizes spacing', () => {
    expect(joinCarsCsv(['car_a ', ' car_b', ''])).toBe('car_a,car_b');
  });
});

describe('isValidContentName', () => {
  it('accepts folder names, rejects traversal', () => {
    expect(isValidContentName('ks_monza')).toBe(true);
    expect(isValidContentName('../evil')).toBe(false);
    expect(isValidContentName('a/b')).toBe(false);
    expect(isValidContentName('')).toBe(false);
  });
});

describe('validateProfileFields', () => {
  it('rejects bad track and http collision', () => {
    expect(
      validateProfileFields({ track: '../x', carsCsv: 'a', httpPort: 8081, tcpPort: 9600, udpPort: 9600 }),
    ).not.toBeNull();
    expect(
      validateProfileFields({ track: 'ks_monza', carsCsv: 'a', httpPort: 9600, tcpPort: 9600, udpPort: 9600 }),
    ).not.toBeNull();
  });

  it('accepts a valid profile', () => {
    expect(
      validateProfileFields({ track: 'ks_monza', carsCsv: 'a,b', httpPort: 8081, tcpPort: 9600, udpPort: 9600 }),
    ).toBeNull();
  });
});
