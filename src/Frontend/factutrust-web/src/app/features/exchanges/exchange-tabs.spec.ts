import { isExchangeTabKey, parseExchangeTab } from './exchange-tabs';

describe('exchange-tabs', () => {
  it('recognizes canonical tab keys', () => {
    expect(isExchangeTabKey('conversation')).toBe(true);
    expect(isExchangeTabKey('documents')).toBe(true);
    expect(isExchangeTabKey('demandes')).toBe(true);
    expect(isExchangeTabKey('taches')).toBe(true);
    expect(isExchangeTabKey('historique')).toBe(true);
    expect(isExchangeTabKey('partage')).toBe(false);
  });

  it('defaults null/empty to conversation', () => {
    expect(parseExchangeTab(null)).toBe('conversation');
    expect(parseExchangeTab(undefined)).toBe('conversation');
    expect(parseExchangeTab('')).toBe('conversation');
    expect(parseExchangeTab('  ')).toBe('conversation');
  });

  it('parses canonical tabs case-insensitively', () => {
    expect(parseExchangeTab('documents')).toBe('documents');
    expect(parseExchangeTab('DEMANDES')).toBe('demandes');
    expect(parseExchangeTab('Historique')).toBe('historique');
  });

  it('maps legacy aliases', () => {
    expect(parseExchangeTab('partage')).toBe('documents');
    expect(parseExchangeTab('messagerie')).toBe('conversation');
    expect(parseExchangeTab('notifications')).toBe('conversation');
  });

  it('falls back unknown values to conversation', () => {
    expect(parseExchangeTab('unknown-tab')).toBe('conversation');
  });
});
