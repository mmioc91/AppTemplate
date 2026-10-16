import { TestBed } from '@angular/core/testing';
import { REFRESH_TOKEN_KEY } from './storage-keys';
import { TokenStore } from './token-store';

describe('TokenStore', () => {
  beforeEach(() => localStorage.clear());

  afterEach(() => vi.restoreAllMocks());

  it('keeps the refresh token in storage, for the next visit and the other tabs', () => {
    // Act
    TestBed.inject(TokenStore).write('refresh-1');

    // Assert
    expect(localStorage.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-1');
  });

  it('keeps the refresh token in memory when storage refuses it, so the session lasts as long as the tab', () => {
    // Arrange: a browser blocking site data throws on any access.
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('Blocked', 'SecurityError');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('Blocked', 'SecurityError');
    });
    const store = TestBed.inject(TokenStore);

    // Act
    store.write('refresh-1');

    // Assert
    expect(store.read()).toBe('refresh-1');
  });
});
