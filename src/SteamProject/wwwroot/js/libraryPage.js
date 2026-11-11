function setHiddenGame(name, userId)
{
    // userId is derived server-side from the authenticated user; kept only for call-site compatibility.
    var gameName = document.getElementById(name);
    var gameId = gameName.getAttribute('name');
    $.ajax({
        type: "POST",
        dataType: "text",
        url: `/api/Steam/hide?id=${gameId}`,
        success: function () { hideGame(gameName); },   // only update the UI on a 2xx response
        error: errorOnAjax
    })
}

function setUnhideGame(name, userId)
{
    var gameName = document.getElementById(name);
    var gameId = gameName.getAttribute('value');
    $.ajax({
        type: "POST",
        dataType: "text",
        url: `/api/Steam/unhide?id=${gameId}`,
        success: function () { hideGame(gameName); },
        error: errorOnAjax
    })
}

function refreshLibrary()
{
    // Show refresh overlay using the loading system
    if (window.SteamNexusLoader) {
        SteamNexusLoader.showRefreshOverlay('Refreshing Library...', 'Fetching your games from Steam');
    }

    $.ajax({
        type: "GET",
        dataType: "json",
        url: `/api/Steam/refresh`,
        success: function() {
            window.location.reload();
        },
        error: function(xhr, status, error) {
            console.error("Library refresh failed:", error);
            if (window.SteamNexusLoader) {
                SteamNexusLoader.hideRefreshOverlay();
            }
            if (window.SteamNexusToast) {
                SteamNexusToast.error('Refresh Failed', 'Could not refresh your library. Please try again.');
            } else {
                alert("Failed to refresh library. Please try again.");
            }
        }
    });
}

function closeModal()
{
    window.location.reload()
}

function hideGame(gameName)
{
    gameName.style.display = "none";
    gameName.hidden = true;
}

function showHiddenGamesModal()
{
    var modal = document.getElementById("hidden-game-table");
    $("#hidden-game-modal").modal("show");
    console.log("Modal was shown");
}

function errorOnAjax() {
    console.log("ERROR on ajax request");
}

// ========================================
// LIBRARY FILTERING SYSTEM
// Multi-select genres, categories, and search
// ========================================

// Toggle filter panel visibility
function toggleFilterPanel() {
    const panel = document.getElementById('filter-panel');
    const btn = document.querySelector('.filter-toggle-btn');

    if (panel.style.display === 'none') {
        panel.style.display = 'block';
        btn.classList.add('active');
    } else {
        panel.style.display = 'none';
        btn.classList.remove('active');
    }
}

// Get currently selected genres
function getSelectedGenres() {
    const checkboxes = document.querySelectorAll('#genre-chips input[type="checkbox"]:checked');
    return Array.from(checkboxes).map(cb => cb.value);
}

// Get currently selected categories
function getSelectedCategories() {
    const checkboxes = document.querySelectorAll('#category-chips input[type="checkbox"]:checked');
    return Array.from(checkboxes).map(cb => cb.value);
}

// Get current search term
function getSearchTerm() {
    const input = document.getElementById('search-input');
    return input ? input.value.trim().toUpperCase() : '';
}

// Main filter function - combines all filters
function applyFilters() {
    const games = document.querySelectorAll('#game-Library .gameContainer');
    const selectedGenres = getSelectedGenres();
    const selectedCategories = getSelectedCategories();
    const searchTerm = getSearchTerm();

    let visibleCount = 0;
    const totalFilters = selectedGenres.length + selectedCategories.length;

    games.forEach(game => {
        const gameGenres = (game.getAttribute('data-genres') || '').split(',').map(g => g.trim()).filter(g => g);
        const gameCategories = (game.getAttribute('data-categories') || '').split(',').map(c => c.trim()).filter(c => c);
        const gameName = game.id.toUpperCase();

        let matchesGenre = true;
        let matchesCategory = true;
        let matchesSearch = true;

        // Check genre filter (ANY selected genre matches)
        if (selectedGenres.length > 0) {
            matchesGenre = selectedGenres.some(genre => gameGenres.includes(genre));
        }

        // Check category filter (ANY selected category matches)
        if (selectedCategories.length > 0) {
            matchesCategory = selectedCategories.some(category => gameCategories.includes(category));
        }

        // Check search filter
        if (searchTerm) {
            matchesSearch = gameName.includes(searchTerm);
        }

        // Game must match ALL active filter types
        if (matchesGenre && matchesCategory && matchesSearch) {
            game.style.display = '';
            game.style.opacity = '1';
            game.style.transform = 'scale(1)';
            visibleCount++;
        } else {
            game.style.display = 'none';
            game.style.opacity = '0';
            game.style.transform = 'scale(0.95)';
        }
    });

    // Update UI elements
    updateFilterCount(totalFilters);
    updateFilterMessage(visibleCount, totalFilters, searchTerm);
}

// Clear all genre filters
function clearGenreFilters() {
    const checkboxes = document.querySelectorAll('#genre-chips input[type="checkbox"]');
    checkboxes.forEach(cb => cb.checked = false);
    applyFilters();
}

// Clear all category filters
function clearCategoryFilters() {
    const checkboxes = document.querySelectorAll('#category-chips input[type="checkbox"]');
    checkboxes.forEach(cb => cb.checked = false);
    applyFilters();
}

// Clear all filters including search
function clearAllFilters() {
    const genreBoxes = document.querySelectorAll('#genre-chips input[type="checkbox"]');
    const categoryBoxes = document.querySelectorAll('#category-chips input[type="checkbox"]');
    const searchInput = document.getElementById('search-input');

    genreBoxes.forEach(cb => cb.checked = false);
    categoryBoxes.forEach(cb => cb.checked = false);
    if (searchInput) searchInput.value = '';

    applyFilters();
}

// Update the filter count badge
function updateFilterCount(count) {
    const badge = document.getElementById('active-filter-count');
    const clearBtn = document.getElementById('clear-all-btn');

    if (count > 0) {
        badge.textContent = count;
        badge.style.display = 'inline-flex';
        clearBtn.style.display = 'inline-flex';
    } else {
        badge.style.display = 'none';
        clearBtn.style.display = 'none';
    }
}

// Update the filter message display
function updateFilterMessage(count, filterCount, searchTerm) {
    let messageEl = document.getElementById('filter-message');

    if (!messageEl) {
        messageEl = document.createElement('div');
        messageEl.id = 'filter-message';
        messageEl.className = 'filter-message';
        const library = document.getElementById('game-Library');
        if (library && library.parentNode) {
            library.parentNode.insertBefore(messageEl, library);
        }
    }

    const hasFilters = filterCount > 0 || searchTerm;

    if (hasFilters && count === 0) {
        messageEl.innerHTML = `<i class="bi bi-info-circle"></i> No games match your filters`;
        messageEl.style.display = 'block';
    } else if (hasFilters) {
        messageEl.innerHTML = `<i class="bi bi-funnel-fill"></i> Showing ${count} game${count !== 1 ? 's' : ''}`;
        messageEl.style.display = 'block';
    } else {
        messageEl.style.display = 'none';
    }
}

// Initialize search integration on page load
document.addEventListener('DOMContentLoaded', function() {
    const searchInput = document.getElementById('search-input');
    if (searchInput) {
        // Override default search.js behavior
        searchInput.addEventListener('input', function() {
            applyFilters();
        });
    }
});

