// Utility functions for SteamNexus
// These are pure functions that can be unit tested

/**
 * Check if a game matches the selected genres (ANY match)
 * @param {string[]} gameGenres - Array of game's genres
 * @param {string[]} selectedGenres - Array of selected filter genres
 * @returns {boolean} - True if game matches any selected genre or no genres selected
 */
export function matchesGenreFilter(gameGenres, selectedGenres) {
    if (!selectedGenres || selectedGenres.length === 0) {
        return true;
    }
    if (!gameGenres || gameGenres.length === 0) {
        return false;
    }
    return selectedGenres.some(genre => gameGenres.includes(genre));
}

/**
 * Check if a game matches the selected categories (ANY match)
 * @param {string[]} gameCategories - Array of game's categories
 * @param {string[]} selectedCategories - Array of selected filter categories
 * @returns {boolean} - True if game matches any selected category or no categories selected
 */
export function matchesCategoryFilter(gameCategories, selectedCategories) {
    if (!selectedCategories || selectedCategories.length === 0) {
        return true;
    }
    if (!gameCategories || gameCategories.length === 0) {
        return false;
    }
    return selectedCategories.some(category => gameCategories.includes(category));
}

/**
 * Check if a game name matches the search term (case-insensitive)
 * @param {string} gameName - The game's name
 * @param {string} searchTerm - The search term
 * @returns {boolean} - True if game name includes search term or no search term
 */
export function matchesSearchFilter(gameName, searchTerm) {
    if (!searchTerm || searchTerm.trim() === '') {
        return true;
    }
    if (!gameName) {
        return false;
    }
    return gameName.toUpperCase().includes(searchTerm.toUpperCase());
}

/**
 * Parse a comma-separated string of genres/categories into an array
 * @param {string} dataString - Comma-separated string
 * @returns {string[]} - Array of trimmed non-empty values
 */
export function parseDataAttribute(dataString) {
    if (!dataString || typeof dataString !== 'string') {
        return [];
    }
    return dataString.split(',').map(s => s.trim()).filter(s => s.length > 0);
}

/**
 * Calculate competition length in days
 * @param {Date|string} startDate - Competition start date
 * @param {Date|string} endDate - Competition end date
 * @returns {number} - Number of days (ceiling)
 */
export function calculateCompetitionDays(startDate, endDate) {
    const start = new Date(startDate).getTime();
    const end = new Date(endDate).getTime();
    const msPerDay = 86400000;
    return Math.ceil((end - start) / msPerDay);
}

/**
 * Format time remaining into days, hours, minutes, seconds
 * @param {number} totalMs - Total milliseconds remaining
 * @returns {object} - Object with days, hours, minutes, seconds
 */
export function formatTimeRemaining(totalMs) {
    const msPerSecond = 1000;
    const msPerMinute = 60000;
    const msPerHour = 3600000;
    const msPerDay = 86400000;

    let remaining = Math.abs(totalMs);

    const days = Math.floor(remaining / msPerDay);
    remaining %= msPerDay;

    const hours = Math.floor(remaining / msPerHour);
    remaining %= msPerHour;

    const minutes = Math.floor(remaining / msPerMinute);
    remaining %= msPerMinute;

    const seconds = Math.floor(remaining / msPerSecond);

    return { days, hours, minutes, seconds };
}

/**
 * Validate a Steam ID format (should be 17 digit number)
 * @param {string} steamId - Steam ID to validate
 * @returns {boolean} - True if valid format
 */
export function isValidSteamId(steamId) {
    if (!steamId || typeof steamId !== 'string') {
        return false;
    }
    return /^\d{17}$/.test(steamId);
}

/**
 * Escape HTML characters to prevent XSS
 * @param {string} str - String to escape
 * @returns {string} - Escaped string
 */
export function escapeHtml(str) {
    if (!str || typeof str !== 'string') {
        return '';
    }
    const htmlEntities = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#39;'
    };
    return str.replace(/[&<>"']/g, char => htmlEntities[char]);
}

/**
 * Backoff schedule for the shared-layout messenger unread-count poll.
 * The old behaviour polled once a second forever, on every authenticated page
 * and in every open tab. These are the two real intervals the poll now uses
 * (see nextPollDelayMs): when the message container is open the count is
 * refreshable on a short interval, when it is closed a longer one. A hidden
 * tab polls not at all.
 * @type {{open: number, closed: number}}
 */
export const MESSAGER_POLL_INTERVALS = {
    open: 15000,
    closed: 60000
};

/**
 * Choose the messenger poll delay for the current state.
 * @param {{visibilityState?: string, containerOpen?: boolean}} state -
 *   `visibilityState` is the tab's document.visibilityState ('visible' or
 *   'hidden'); `containerOpen` is whether the message container is open.
 * @returns {number} - Milliseconds to wait before the next poll. 0 means "do
 *   not poll right now" (the tab is hidden); the caller reschedules from the
 *   visibilitychange handler instead of a timer.
 */
export function nextPollDelayMs(state) {
    const containerOpen = Boolean(state && state.containerOpen);
    const visibilityState = state && typeof state.visibilityState === 'string'
        ? state.visibilityState
        : 'visible';
    if (visibilityState === 'hidden') {
        return 0;
    }
    return containerOpen ? MESSAGER_POLL_INTERVALS.open : MESSAGER_POLL_INTERVALS.closed;
}

/**
 * Decide the messenger unread-badge and thread-refresh state for one poll tick.
 *
 * The badge is driven by the count itself, not by a delta: it is visible while
 * there are unread messages and the container is closed, and hidden as soon as
 * the count reaches 0 (or the container is open — displayMsgs() hides the badge
 * for that case too). The old `data > msgCount` comparison had three defects: a
 * message that arrived before the initial count resolved raised no alert, a page
 * landed on with existing unread messages never showed the badge, and reading the
 * messages elsewhere never cleared it because nothing lowered the comparison.
 *
 * The delta (newCount > previousCount) is kept only to decide whether to refresh
 * the open conversation: a count that went UP is the only signal a new message
 * arrived in the thread that is currently showing.
 *
 * @param {{previousCount?: number, newCount?: number, containerVisible?: boolean}} state
 *   `previousCount` is the last observed unread count (undefined until the first
 *   count resolves); `newCount` is the count just fetched; `containerVisible` is
 *   whether the message container is open (msgContainer.style.visibility ===
 *   "visible").
 * @returns {{showBadge: boolean, reloadThread: boolean}}
 */
export function messageBadgeDecision(state) {
    const s = state || {};
    const previousCount = s.previousCount; // may be undefined until the first count resolves
    const newCount = typeof s.newCount === 'number' ? s.newCount : 0;
    const containerVisible = Boolean(s.containerVisible);

    // Badge from the count itself: on while unread and the container is closed,
    // off once the count reaches 0. `newCount > previousCount` is undefined-safe
    // (x > undefined is false), so the very first tick never triggers a reload.
    const showBadge = newCount > 0 && !containerVisible;
    const reloadThread = containerVisible && newCount > previousCount;
    return { showBadge, reloadThread };
}

/**
 * Format a message timestamp for display in the reader's own time zone.
 *
 * The server sends an ISO-8601 UTC instant (e.g. "2026-10-23T15:04:05Z"). We
 * parse it with Date (the trailing Z is UTC), then format it against the reader's
 * local clock — so a message shows a full date plus the local time, instead of the
 * server's bare "hh:mm tt" clock string that had no date and no time zone of record.
 *
 * @param {string} [isoTimestamp] - ISO-8601 UTC string from the API, or empty/missing.
 * @param {Intl.DateTimeFormatOptions} [options] - Optional overrides (e.g. pin a
 *   `timeZone` in tests). Merged over date+time defaults.
 * @returns {string} - Localised date+time. '' when there is no timestamp; the raw
 *   value when it doesn't parse (never "Invalid Date").
 */
export function formatMessageTime(isoTimestamp, options) {
    if (!isoTimestamp || typeof isoTimestamp !== 'string') {
        return '';
    }
    const date = new Date(isoTimestamp);
    if (Number.isNaN(date.getTime())) {
        return isoTimestamp;
    }
    const defaults = {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: 'numeric',
        minute: '2-digit',
        hour12: true
    };
    return date.toLocaleString(undefined, { ...defaults, ...options });
}

/**
 * Truncate text to a max length with ellipsis
 * @param {string} text - Text to truncate
 * @param {number} maxLength - Maximum length
 * @returns {string} - Truncated text
 */
export function truncateText(text, maxLength) {
    if (!text || typeof text !== 'string') {
        return '';
    }
    if (text.length <= maxLength) {
        return text;
    }
    return text.substring(0, maxLength - 3) + '...';
}
