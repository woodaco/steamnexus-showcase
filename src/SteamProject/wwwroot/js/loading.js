/**
 * SteamNexus Loading State Management
 * Handles page loading, skeleton screens, and refresh overlays
 */

const SteamNexusLoader = {
    /**
     * Initialize the loading system
     */
    init: function() {
        // Wait for DOM to be ready
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.onDOMReady());
        } else {
            this.onDOMReady();
        }

        // Handle full page load (including images, stylesheets, etc.)
        window.addEventListener('load', () => this.onPageLoad());
    },

    /**
     * Called when DOM is ready (but images may still be loading)
     */
    onDOMReady: function() {
        // Add loaded class to elements that should fade in
        const fadeElements = document.querySelectorAll('.sn-loading-hidden');
        fadeElements.forEach(el => {
            el.classList.add('loaded');
        });

        // Initialize any skeleton loaders that need to be replaced
        this.initSkeletonReplacements();
    },

    /**
     * Called when page is fully loaded (including all resources)
     */
    onPageLoad: function() {
        // Hide the page loader if present
        const pageLoader = document.querySelector('.sn-page-loader');
        if (pageLoader) {
            pageLoader.classList.add('loaded');
            // Remove from DOM after animation completes
            setTimeout(() => {
                pageLoader.remove();
            }, 300);
        }

        // Replace any remaining skeletons with actual content
        this.replaceAllSkeletons();
    },

    /**
     * Initialize skeleton replacement observers
     */
    initSkeletonReplacements: function() {
        // Watch for dynamically loaded content
        const contentAreas = document.querySelectorAll('[data-skeleton-target]');
        contentAreas.forEach(area => {
            const targetId = area.getAttribute('data-skeleton-target');
            const skeleton = document.getElementById(targetId);
            if (skeleton && area.children.length > 0) {
                skeleton.style.display = 'none';
                area.style.display = '';
            }
        });
    },

    /**
     * Replace all skeleton loaders with real content
     */
    replaceAllSkeletons: function() {
        // Hide skeleton containers that have a sibling content container
        const skeletons = document.querySelectorAll('.sn-skeleton-container');
        skeletons.forEach(skeleton => {
            const content = skeleton.nextElementSibling;
            if (content && content.classList.contains('sn-content-container')) {
                skeleton.style.display = 'none';
                content.classList.add('sn-fade-in');
                content.style.display = '';
            }
        });
    },

    /**
     * Show a refresh overlay (for library refresh, etc.)
     * @param {string} message - The message to display
     * @param {string} subMessage - Optional sub-message
     */
    showRefreshOverlay: function(message = 'Refreshing...', subMessage = 'This may take a moment') {
        // Create overlay if it doesn't exist
        let overlay = document.getElementById('sn-refresh-overlay');
        if (!overlay) {
            overlay = document.createElement('div');
            overlay.id = 'sn-refresh-overlay';
            overlay.className = 'sn-refresh-overlay';
            overlay.innerHTML = `
                <div class="sn-spinner sn-spinner-xl"></div>
                <div class="sn-refresh-overlay-text">${message}</div>
                <div class="sn-refresh-overlay-subtext">${subMessage}</div>
            `;
            document.body.appendChild(overlay);
        } else {
            overlay.querySelector('.sn-refresh-overlay-text').textContent = message;
            overlay.querySelector('.sn-refresh-overlay-subtext').textContent = subMessage;
        }

        // Show overlay
        requestAnimationFrame(() => {
            overlay.classList.add('active');
        });

        return overlay;
    },

    /**
     * Hide the refresh overlay
     */
    hideRefreshOverlay: function() {
        const overlay = document.getElementById('sn-refresh-overlay');
        if (overlay) {
            overlay.classList.remove('active');
            setTimeout(() => {
                overlay.remove();
            }, 200);
        }
    },

    /**
     * Show section loading spinner
     * @param {HTMLElement} section - The section element to show loading on
     */
    showSectionLoader: function(section) {
        // Add relative positioning if not already set
        const position = window.getComputedStyle(section).position;
        if (position === 'static') {
            section.style.position = 'relative';
        }

        // Create loader if it doesn't exist
        let loader = section.querySelector('.sn-section-loader');
        if (!loader) {
            loader = document.createElement('div');
            loader.className = 'sn-section-loader';
            loader.innerHTML = '<div class="sn-spinner sn-spinner-lg"></div>';
            section.appendChild(loader);
        }

        requestAnimationFrame(() => {
            loader.classList.add('loading');
        });
    },

    /**
     * Hide section loading spinner
     * @param {HTMLElement} section - The section element
     */
    hideSectionLoader: function(section) {
        const loader = section.querySelector('.sn-section-loader');
        if (loader) {
            loader.classList.remove('loading');
        }
    },

    /**
     * Create skeleton cards for a grid
     * @param {number} count - Number of skeleton cards to create
     * @returns {string} HTML string of skeleton cards
     */
    createSkeletonCards: function(count = 12) {
        let html = '';
        for (let i = 0; i < count; i++) {
            html += `
                <div class="sn-skeleton-card">
                    <div class="sn-skeleton sn-skeleton-card-img"></div>
                    <div class="sn-skeleton-card-body">
                        <div class="sn-skeleton sn-skeleton-title"></div>
                        <div class="sn-skeleton sn-skeleton-text"></div>
                    </div>
                </div>
            `;
        }
        return html;
    },

    /**
     * Create skeleton friend cards
     * @param {number} count - Number of skeleton cards to create
     * @returns {string} HTML string of skeleton friend cards
     */
    createSkeletonFriends: function(count = 8) {
        let html = '';
        for (let i = 0; i < count; i++) {
            html += `
                <div class="sn-skeleton-friend">
                    <div class="sn-skeleton sn-skeleton-avatar sn-skeleton-avatar-lg"></div>
                    <div class="sn-skeleton-friend-info">
                        <div class="sn-skeleton sn-skeleton-title" style="width: 50%;"></div>
                        <div class="sn-skeleton sn-skeleton-text" style="width: 30%;"></div>
                    </div>
                </div>
            `;
        }
        return html;
    },

    /**
     * Create skeleton competition cards
     * @param {number} count - Number of skeleton cards to create
     * @returns {string} HTML string of skeleton competition cards
     */
    createSkeletonCompetitions: function(count = 3) {
        let html = '';
        for (let i = 0; i < count; i++) {
            html += `
                <div class="sn-skeleton-competition">
                    <div class="sn-skeleton sn-skeleton-competition-img"></div>
                    <div class="sn-skeleton-competition-body">
                        <div class="sn-skeleton sn-skeleton-title"></div>
                        <div class="sn-skeleton sn-skeleton-text"></div>
                        <div class="sn-skeleton sn-skeleton-text" style="width: 50%;"></div>
                    </div>
                </div>
            `;
        }
        return html;
    },

    /**
     * Wrap a fetch call with loading state
     * @param {HTMLElement} container - Container to show loading on
     * @param {Function} fetchFn - Async function that fetches data
     * @param {Function} renderFn - Function that renders the data
     */
    withLoading: async function(container, fetchFn, renderFn) {
        this.showSectionLoader(container);
        try {
            const data = await fetchFn();
            renderFn(data);
        } catch (error) {
            console.error('Loading error:', error);
            container.innerHTML = `
                <div class="sn-alert sn-alert-danger">
                    <i class="bi bi-exclamation-circle"></i>
                    <span>Failed to load content. Please try again.</span>
                </div>
            `;
        } finally {
            this.hideSectionLoader(container);
        }
    }
};

// Initialize on script load
SteamNexusLoader.init();

// Export for use in other scripts
window.SteamNexusLoader = SteamNexusLoader;

/**
 * SteamNexus Toast Notification System
 */
const SteamNexusToast = {
    container: null,

    /**
     * Initialize the toast container
     */
    init: function() {
        if (!this.container) {
            this.container = document.createElement('div');
            this.container.className = 'sn-toast-container';
            document.body.appendChild(this.container);
        }
    },

    /**
     * Show a toast notification
     * @param {Object} options - Toast options
     * @param {string} options.title - Toast title
     * @param {string} options.message - Toast message (optional)
     * @param {string} options.type - Toast type: 'success', 'error', 'warning', 'info'
     * @param {number} options.duration - Duration in ms (default: 4000)
     */
    show: function(options) {
        this.init();

        const { title, message = '', type = 'info', duration = 4000 } = options;

        const toast = document.createElement('div');
        toast.className = `sn-toast sn-toast-${type}`;

        const iconMap = {
            success: 'bi-check-circle-fill',
            error: 'bi-x-circle-fill',
            warning: 'bi-exclamation-triangle-fill',
            info: 'bi-info-circle-fill'
        };

        toast.innerHTML = `
            <div class="sn-toast-icon">
                <i class="bi ${iconMap[type] || iconMap.info}"></i>
            </div>
            <div class="sn-toast-content">
                <div class="sn-toast-title">${title}</div>
                ${message ? `<div class="sn-toast-message">${message}</div>` : ''}
            </div>
            <button class="sn-toast-close" aria-label="Close">
                <i class="bi bi-x"></i>
            </button>
        `;

        // Close button handler
        const closeBtn = toast.querySelector('.sn-toast-close');
        closeBtn.addEventListener('click', () => this.dismiss(toast));

        this.container.appendChild(toast);

        // Auto dismiss
        if (duration > 0) {
            setTimeout(() => this.dismiss(toast), duration);
        }

        return toast;
    },

    /**
     * Dismiss a toast
     * @param {HTMLElement} toast - The toast element to dismiss
     */
    dismiss: function(toast) {
        if (!toast || !toast.parentNode) return;

        toast.classList.add('sn-toast-exit');
        setTimeout(() => {
            if (toast.parentNode) {
                toast.parentNode.removeChild(toast);
            }
        }, 300);
    },

    /**
     * Show a success toast
     */
    success: function(title, message) {
        return this.show({ title, message, type: 'success' });
    },

    /**
     * Show an error toast
     */
    error: function(title, message) {
        return this.show({ title, message, type: 'error', duration: 6000 });
    },

    /**
     * Show a warning toast
     */
    warning: function(title, message) {
        return this.show({ title, message, type: 'warning' });
    },

    /**
     * Show an info toast
     */
    info: function(title, message) {
        return this.show({ title, message, type: 'info' });
    }
};

// Export for use in other scripts
window.SteamNexusToast = SteamNexusToast;
