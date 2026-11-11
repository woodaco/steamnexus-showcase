// Jest suite for the shared-layout messenger poll backoff helpers in
// src/SteamProject/wwwroot/js/utils.js.
//
// These helpers drive Messages.js's unread-count poll: nextPollDelayMs decides
// how long to wait between polls (0 = pause) and MESSAGER_POLL_INTERVALS holds
// the two real intervals. Messages.js keeps a single self-rescheduling timer
// and pauses while document.visibilityState is 'hidden', resuming from the
// visibilitychange handler.
//
// The old behaviour was `setInterval(() => getCount(), 1000)` — one authenticated
// request a second, forever, on every page and in every open tab. The
// assertions below lock in that the hidden tab polls at all, and that a visible
// tab uses the distinct open/closed intervals instead of the 1s constant.

import { nextPollDelayMs, MESSAGER_POLL_INTERVALS, messageBadgeDecision } from '../../SteamProject/wwwroot/js/utils.js';

describe('MESSAGER_POLL_INTERVALS', () => {
    test('closed interval is a longer base than the open interval', () => {
        expect(MESSAGER_POLL_INTERVALS.closed).toBeGreaterThan(MESSAGER_POLL_INTERVALS.open);
    });

    test('neither interval is the old 1s constant (hardening, not a transport swap)', () => {
        expect(MESSAGER_POLL_INTERVALS.open).toBeGreaterThan(1000);
        expect(MESSAGER_POLL_INTERVALS.closed).toBeGreaterThan(1000);
    });

    test('intervals are positive finite milliseconds', () => {
        for (const ms of [MESSAGER_POLL_INTERVALS.open, MESSAGER_POLL_INTERVALS.closed]) {
            expect(Number.isFinite(ms)).toBe(true);
            expect(ms).toBeGreaterThan(0);
        }
    });
});

describe('nextPollDelayMs', () => {
    test('a hidden document yields no poll (0), even while the container is open', () => {
        expect(nextPollDelayMs({ visibilityState: 'hidden', containerOpen: true })).toBe(0);
        expect(nextPollDelayMs({ visibilityState: 'hidden', containerOpen: false })).toBe(0);
    });

    test('a visible, closed container yields the closed (long base) interval', () => {
        expect(nextPollDelayMs({ visibilityState: 'visible', containerOpen: false }))
            .toBe(MESSAGER_POLL_INTERVALS.closed);
    });

    test('a visible, open container yields the shorter open interval', () => {
        expect(nextPollDelayMs({ visibilityState: 'visible', containerOpen: true }))
            .toBe(MESSAGER_POLL_INTERVALS.open);
    });

    test('open and closed delays are distinct and follow the closed > open ordering', () => {
        const open = nextPollDelayMs({ visibilityState: 'visible', containerOpen: true });
        const closed = nextPollDelayMs({ visibilityState: 'visible', containerOpen: false });
        expect(open).toBe(MESSAGER_POLL_INTERVALS.open);
        expect(closed).toBe(MESSAGER_POLL_INTERVALS.closed);
        expect(closed).toBeGreaterThan(open);
    });

    test('treats a falsy containerOpen as closed (defensive against undefined/0)', () => {
        expect(nextPollDelayMs({ visibilityState: 'visible', containerOpen: undefined }))
            .toBe(MESSAGER_POLL_INTERVALS.closed);
        expect(nextPollDelayMs({ visibilityState: 'visible', containerOpen: null }))
            .toBe(MESSAGER_POLL_INTERVALS.closed);
    });

    test('treats a missing state as visible+closed (defensive default, not a crash)', () => {
        expect(nextPollDelayMs(undefined)).toBe(MESSAGER_POLL_INTERVALS.closed);
        expect(nextPollDelayMs(null)).toBe(MESSAGER_POLL_INTERVALS.closed);
        expect(nextPollDelayMs({})).toBe(MESSAGER_POLL_INTERVALS.closed);
    });
});

// messageBadgeDecision is the single source of truth for the unread badge.
// The badge is driven by the COUNT ITSELF, not a rising-edge delta: on while
// unread and the container is closed, off the moment the count reaches 0. The
// old `data > msgCount` check only ever lit on a RISING edge, so (a) a message
// that arrived before the initial count resolved showed nothing, (b) a page
// landed on with messages already in the queue showed nothing, and (c) reading
// the messages in another tab never cleared the badge. The delta (newCount >
// previousCount) is retained only to decide whether to refresh the OPEN thread:
// a count that went UP is the only signal a new message arrived in it.
describe('messageBadgeDecision', () => {
    // --- Badge driven by the count itself ------------------------------------

    test('shows the badge when there are unread messages and the container is closed', () => {
        expect(messageBadgeDecision({ previousCount: 0, newCount: 3, containerVisible: false }))
            .toEqual({ showBadge: true, reloadThread: false });
    });

    test('hides the badge the moment the count reaches 0 (the fix for "stays lit after reading elsewhere")', () => {
        // Messages were read in another tab: count fell to 0 while we hold 3.
        expect(messageBadgeDecision({ previousCount: 3, newCount: 0, containerVisible: false }))
            .toEqual({ showBadge: false, reloadThread: false });
    });

    test('hides the badge at 0 even with a prior 0 (steady empty state)', () => {
        expect(messageBadgeDecision({ previousCount: 0, newCount: 0, containerVisible: false }))
            .toEqual({ showBadge: false, reloadThread: false });
    });

    test('badge is not the indicator while the container is open (thread is; decision is closed-scoped)', () => {
        // While open, displayMsgs() has hidden the badge and the open thread is
        // the indicator, so the closed-scoped badge decision comes back false.
        expect(messageBadgeDecision({ previousCount: 0, newCount: 5, containerVisible: true }))
            .toEqual({ showBadge: false, reloadThread: true });
    });

    // --- Defect (b): pre-existing unread messages on page load ---------------

    test('lights the badge for messages ALREADY present when the page loads (no rising edge to catch)', () => {
        // First poll after initialCount(): previousCount is the just-set baseline,
        // but the count never had to "rise" from a lower value to be seen.
        expect(messageBadgeDecision({ previousCount: 0, newCount: 7, containerVisible: false }))
            .toEqual({ showBadge: true, reloadThread: false });
        // And it stays lit on the very next tick as long as unread remain,
        // rather than waiting for the count to grow again.
        expect(messageBadgeDecision({ previousCount: 7, newCount: 7, containerVisible: false }))
            .toEqual({ showBadge: true, reloadThread: false });
    });

    // --- Defect (a): a message that arrives is caught on its very first tick --

    test('first tick after the initial count: a single new message lights the badge', () => {
        // Baseline was 0; one message arrived. The OLD code also caught this,
        // but only because it happened to be a rising edge — here the badge is
        // right for the right reason (count > 0), independent of the delta.
        expect(messageBadgeDecision({ previousCount: 0, newCount: 1, containerVisible: false }))
            .toEqual({ showBadge: true, reloadThread: false });
    });

    // --- Thread reload: only a count that went UP refreshes the open thread --

    test('reloads the open thread when the count went UP', () => {
        expect(messageBadgeDecision({ previousCount: 2, newCount: 3, containerVisible: true }))
            .toEqual({ showBadge: false, reloadThread: true });
    });

    test('does NOT reload when the count fell (messages read in another tab are not new here)', () => {
        expect(messageBadgeDecision({ previousCount: 3, newCount: 1, containerVisible: true }))
            .toEqual({ showBadge: false, reloadThread: false });
    });

    test('does NOT reload when the count is unchanged', () => {
        expect(messageBadgeDecision({ previousCount: 2, newCount: 2, containerVisible: true }))
            .toEqual({ showBadge: false, reloadThread: false });
    });

    test('does NOT reload on the very first tick (previousCount undefined: x > undefined is false)', () => {
        // Guards against a spurious thread reload before any baseline exists.
        expect(messageBadgeDecision({ newCount: 1, containerVisible: true }))
            .toEqual({ showBadge: false, reloadThread: false });
    });

    test('reloadThread is false while the container is closed, even if the count rose', () => {
        expect(messageBadgeDecision({ previousCount: 0, newCount: 4, containerVisible: false }))
            .toEqual({ showBadge: true, reloadThread: false });
    });

    // --- Defensive defaults --------------------------------------------------

    test('a non-numeric newCount is treated as 0 (defensive, not a crash)', () => {
        expect(messageBadgeDecision({ previousCount: 1, newCount: undefined, containerVisible: false }))
            .toEqual({ showBadge: false, reloadThread: false });
        expect(messageBadgeDecision({ previousCount: 1, newCount: null, containerVisible: false }))
            .toEqual({ showBadge: false, reloadThread: false });
        expect(messageBadgeDecision({ previousCount: 1, newCount: '3', containerVisible: false }))
            .toEqual({ showBadge: false, reloadThread: false });
    });

    test('treats a falsy containerVisible as closed', () => {
        expect(messageBadgeDecision({ previousCount: 0, newCount: 2, containerVisible: undefined }))
            .toEqual({ showBadge: true, reloadThread: false });
        expect(messageBadgeDecision({ previousCount: 0, newCount: 2, containerVisible: null }))
            .toEqual({ showBadge: true, reloadThread: false });
    });

    test('a missing state is visible+closed with no count (defensive default)', () => {
        expect(messageBadgeDecision(undefined)).toEqual({ showBadge: false, reloadThread: false });
        expect(messageBadgeDecision(null)).toEqual({ showBadge: false, reloadThread: false });
        expect(messageBadgeDecision({})).toEqual({ showBadge: false, reloadThread: false });
    });
});
