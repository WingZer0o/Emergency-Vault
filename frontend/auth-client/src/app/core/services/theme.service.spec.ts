import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.style.colorScheme = '';
  });

  it('defaults to system mode when nothing is stored', () => {
    const service = TestBed.inject(ThemeService);
    TestBed.flushEffects();
    expect(service.mode()).toBe('system');
    expect(document.documentElement.style.colorScheme).toBe('light dark');
  });

  it('restores a previously saved mode', () => {
    localStorage.setItem('theme-mode', 'dark');
    const service = TestBed.inject(ThemeService);
    TestBed.flushEffects();
    expect(service.mode()).toBe('dark');
    expect(document.documentElement.style.colorScheme).toBe('dark');
  });

  it('updates color-scheme and persists the mode when changed', () => {
    const service = TestBed.inject(ThemeService);
    service.setMode('light');
    TestBed.flushEffects();
    expect(service.mode()).toBe('light');
    expect(document.documentElement.style.colorScheme).toBe('light');
    expect(localStorage.getItem('theme-mode')).toBe('light');
  });
});
