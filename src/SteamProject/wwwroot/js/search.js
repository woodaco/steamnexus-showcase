document.addEventListener('DOMContentLoaded', () => {
    const search = document.querySelector('#search-input')
    const container = document.querySelector('.flex-container')
    const games = document.querySelectorAll('.flex-item')

    // If filter panel exists (library page), defer to libraryPage.js filtering
    if (document.getElementById('filter-panel')) {
        return;
    }

    // Fallback for other pages without filter panel
    if (search && container && games.length > 0) {
        search.addEventListener('input', () => {
            container.innerHTML = ""
            games.forEach((game) => {
                if (game.id.toUpperCase().includes(search.value.toUpperCase())) {
                    container.appendChild(game)
                }
            })
        })
    }
})
