import {
    matchesGenreFilter,
    matchesCategoryFilter,
    matchesSearchFilter,
    parseDataAttribute,
    calculateCompetitionDays,
    formatTimeRemaining,
    isValidSteamId,
    escapeHtml,
    formatMessageTime,
    truncateText
} from '../../SteamProject/wwwroot/js/utils.js';

describe('matchesGenreFilter', () => {
    test('returns true when no genres selected', () => {
        expect(matchesGenreFilter(['Action', 'RPG'], [])).toBe(true);
        expect(matchesGenreFilter(['Action'], null)).toBe(true);
        expect(matchesGenreFilter([], undefined)).toBe(true);
    });

    test('returns true when game has matching genre', () => {
        expect(matchesGenreFilter(['Action', 'RPG'], ['Action'])).toBe(true);
        expect(matchesGenreFilter(['Action', 'RPG', 'Indie'], ['Indie', 'Strategy'])).toBe(true);
    });

    test('returns false when game has no matching genre', () => {
        expect(matchesGenreFilter(['Action', 'RPG'], ['Strategy'])).toBe(false);
        expect(matchesGenreFilter(['Indie'], ['Action', 'RPG'])).toBe(false);
    });

    test('returns false when game has no genres', () => {
        expect(matchesGenreFilter([], ['Action'])).toBe(false);
        expect(matchesGenreFilter(null, ['Action'])).toBe(false);
    });
});

describe('matchesCategoryFilter', () => {
    test('returns true when no categories selected', () => {
        expect(matchesCategoryFilter(['Single-player'], [])).toBe(true);
        expect(matchesCategoryFilter(['Multi-player'], null)).toBe(true);
    });

    test('returns true when game has matching category', () => {
        expect(matchesCategoryFilter(['Single-player', 'Co-op'], ['Co-op'])).toBe(true);
    });

    test('returns false when game has no matching category', () => {
        expect(matchesCategoryFilter(['Single-player'], ['Multi-player'])).toBe(false);
    });
});

describe('matchesSearchFilter', () => {
    test('returns true when no search term', () => {
        expect(matchesSearchFilter('Half-Life 2', '')).toBe(true);
        expect(matchesSearchFilter('Portal', null)).toBe(true);
        expect(matchesSearchFilter('Doom', '   ')).toBe(true);
    });

    test('returns true for case-insensitive match', () => {
        expect(matchesSearchFilter('Half-Life 2', 'half')).toBe(true);
        expect(matchesSearchFilter('Half-Life 2', 'LIFE')).toBe(true);
        expect(matchesSearchFilter('Counter-Strike', 'strike')).toBe(true);
    });

    test('returns false for no match', () => {
        expect(matchesSearchFilter('Half-Life 2', 'portal')).toBe(false);
        expect(matchesSearchFilter('Doom', 'quake')).toBe(false);
    });

    test('returns false for null game name', () => {
        expect(matchesSearchFilter(null, 'test')).toBe(false);
        expect(matchesSearchFilter(undefined, 'test')).toBe(false);
    });
});

describe('parseDataAttribute', () => {
    test('parses comma-separated values', () => {
        expect(parseDataAttribute('Action,RPG,Indie')).toEqual(['Action', 'RPG', 'Indie']);
    });

    test('trims whitespace', () => {
        expect(parseDataAttribute('Action , RPG , Indie')).toEqual(['Action', 'RPG', 'Indie']);
        expect(parseDataAttribute('  Action  ,  RPG  ')).toEqual(['Action', 'RPG']);
    });

    test('filters empty values', () => {
        expect(parseDataAttribute('Action,,RPG')).toEqual(['Action', 'RPG']);
        expect(parseDataAttribute(',Action,,')).toEqual(['Action']);
    });

    test('returns empty array for invalid input', () => {
        expect(parseDataAttribute('')).toEqual([]);
        expect(parseDataAttribute(null)).toEqual([]);
        expect(parseDataAttribute(undefined)).toEqual([]);
    });
});

describe('calculateCompetitionDays', () => {
    test('calculates days between dates', () => {
        expect(calculateCompetitionDays('2025-01-01', '2025-01-08')).toBe(7);
        expect(calculateCompetitionDays('2025-01-01', '2025-01-02')).toBe(1);
    });

    test('rounds up partial days', () => {
        // 1.5 days should round to 2
        const start = new Date('2025-01-01T00:00:00');
        const end = new Date('2025-01-02T12:00:00');
        expect(calculateCompetitionDays(start, end)).toBe(2);
    });
});

describe('formatTimeRemaining', () => {
    test('formats milliseconds correctly', () => {
        // 1 day, 2 hours, 3 minutes, 4 seconds
        const ms = (1 * 86400000) + (2 * 3600000) + (3 * 60000) + (4 * 1000);
        const result = formatTimeRemaining(ms);
        expect(result.days).toBe(1);
        expect(result.hours).toBe(2);
        expect(result.minutes).toBe(3);
        expect(result.seconds).toBe(4);
    });

    test('handles zero', () => {
        const result = formatTimeRemaining(0);
        expect(result.days).toBe(0);
        expect(result.hours).toBe(0);
        expect(result.minutes).toBe(0);
        expect(result.seconds).toBe(0);
    });

    test('handles negative values (uses absolute)', () => {
        const ms = -3600000; // -1 hour
        const result = formatTimeRemaining(ms);
        expect(result.hours).toBe(1);
    });
});

describe('isValidSteamId', () => {
    test('returns true for valid 17-digit Steam ID', () => {
        expect(isValidSteamId('76561198012345678')).toBe(true);
        expect(isValidSteamId('12345678901234567')).toBe(true);
    });

    test('returns false for invalid Steam IDs', () => {
        expect(isValidSteamId('123')).toBe(false);
        expect(isValidSteamId('1234567890123456')).toBe(false); // 16 digits
        expect(isValidSteamId('123456789012345678')).toBe(false); // 18 digits
        expect(isValidSteamId('7656119801234567a')).toBe(false); // has letter
        expect(isValidSteamId('')).toBe(false);
        expect(isValidSteamId(null)).toBe(false);
    });
});

describe('escapeHtml', () => {
    test('escapes HTML special characters', () => {
        expect(escapeHtml('<script>')).toBe('&lt;script&gt;');
        expect(escapeHtml('"quotes"')).toBe('&quot;quotes&quot;');
        expect(escapeHtml("'apostrophe'")).toBe('&#39;apostrophe&#39;');
        expect(escapeHtml('A & B')).toBe('A &amp; B');
    });

    test('handles multiple special characters', () => {
        expect(escapeHtml('<a href="test">Link</a>')).toBe('&lt;a href=&quot;test&quot;&gt;Link&lt;/a&gt;');
    });

    test('returns empty string for invalid input', () => {
        expect(escapeHtml(null)).toBe('');
        expect(escapeHtml(undefined)).toBe('');
        expect(escapeHtml('')).toBe('');
    });

    test('leaves safe strings unchanged', () => {
        expect(escapeHtml('Hello World')).toBe('Hello World');
        expect(escapeHtml('Game Name 123')).toBe('Game Name 123');
    });
});

describe('truncateText', () => {
    test('truncates long text with ellipsis', () => {
        expect(truncateText('This is a very long game name', 15)).toBe('This is a ve...');
        expect(truncateText('Hello World', 8)).toBe('Hello...');
    });

    test('returns original text if under max length', () => {
        expect(truncateText('Short', 10)).toBe('Short');
        expect(truncateText('Exact', 5)).toBe('Exact');
    });

    test('handles edge cases', () => {
        expect(truncateText('', 10)).toBe('');
        expect(truncateText(null, 10)).toBe('');
        expect(truncateText(undefined, 10)).toBe('');
    });
});

describe('formatMessageTime', () => {
    // The API sends an ISO-8601 UTC instant (trailing Z). We must render a DATE plus a
    // time in a chosen time zone — not echo the raw string and not show a bare clock.
    const UTC_INstant = '2026-10-23T15:04:05Z';

    test('returns empty string for missing/non-string input', () => {
        expect(formatMessageTime('')).toBe('');
        expect(formatMessageTime(null)).toBe('');
        expect(formatMessageTime(undefined)).toBe('');
        expect(formatMessageTime(0)).toBe('');
    });

    test('echoes an unparseable value rather than "Invalid Date"', () => {
        expect(formatMessageTime('not-a-date')).toBe('not-a-date');
    });

    test('renders a full date (not a bare clock) for a valid UTC instant', () => {
        const out = formatMessageTime(UTC_INstant, { timeZone: 'UTC', hour12: false });
        expect(out).not.toBe('Invalid Date');
        expect(out).not.toBe(UTC_INstant);        // was actually formatted, not echoed
        expect(out).toContain('2026');            // year -> a date is present
        expect(out).toContain('23');              // day of month -> a date is present
    });

    test('localises the instant to the requested time zone', () => {
        // Same UTC instant, two very different zones: the rendered time must differ,
        // proving the trailing Z is read as a UTC instant and converted — not treated
        // as the reader's local wall clock or echoed verbatim.
        const inUtc = formatMessageTime(UTC_INstant, { timeZone: 'UTC', hour12: false });
        const inKiritimati = formatMessageTime(UTC_INstant, { timeZone: 'Pacific/Kiritimati', hour12: false });
        expect(inUtc).not.toBe(inKiritimati);
    });
});
