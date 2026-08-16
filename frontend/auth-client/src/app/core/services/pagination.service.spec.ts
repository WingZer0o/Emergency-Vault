import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { PaginationService } from './pagination.service';

describe('PaginationService', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('defaults to 10 per page when nothing is stored', () => {
    const service = TestBed.inject(PaginationService);
    expect(service.pageSize()).toBe(10);
  });

  it('restores a previously saved page size', () => {
    localStorage.setItem('page-size', '25');
    const service = TestBed.inject(PaginationService);
    expect(service.pageSize()).toBe(25);
  });

  it('falls back to the default when the stored value is not an offered size', () => {
    localStorage.setItem('page-size', '999');
    const service = TestBed.inject(PaginationService);
    expect(service.pageSize()).toBe(10);
  });

  it('persists a new page size', () => {
    const service = TestBed.inject(PaginationService);
    service.setPageSize(50);
    TestBed.flushEffects();
    expect(service.pageSize()).toBe(50);
    expect(localStorage.getItem('page-size')).toBe('50');
  });

  it('ignores a page size that is not on the offered list', () => {
    const service = TestBed.inject(PaginationService);
    service.setPageSize(7);
    expect(service.pageSize()).toBe(10);
  });

  it('slices the source down to the requested page', () => {
    const service = TestBed.inject(PaginationService);
    const source = signal(Array.from({ length: 23 }, (_, i) => i));
    const pageIndex = signal(0);
    const paged = service.slice(source, pageIndex);

    expect(paged()).toHaveLength(10);
    expect(paged()[0]).toBe(0);

    pageIndex.set(2);
    expect(paged()).toHaveLength(3);
    expect(paged()[0]).toBe(20);
  });

  it('reslices when the page size changes', () => {
    const service = TestBed.inject(PaginationService);
    const source = signal(Array.from({ length: 23 }, (_, i) => i));
    const paged = service.slice(source, signal(0));

    expect(paged()).toHaveLength(10);
    service.setPageSize(25);
    expect(paged()).toHaveLength(23);
  });

  it('reslices when the source list changes', () => {
    const service = TestBed.inject(PaginationService);
    const source = signal([1, 2, 3]);
    const paged = service.slice(source, signal(0));

    expect(paged()).toHaveLength(3);
    source.set([1, 2]);
    expect(paged()).toHaveLength(2);
  });
});
