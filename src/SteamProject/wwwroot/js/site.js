// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.


// Global Javascript Code

// --- Antiforgery: attach the token to every non-GET request ---
window.getAntiForgeryToken = function () {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : '';
};

if (window.jQuery) {
    $.ajaxSetup({
        beforeSend: function (xhr, settings) {
            if (settings.type && !/^(GET|HEAD|OPTIONS|TRACE)$/i.test(settings.type)) {
                var token = window.getAntiForgeryToken();
                if (token) xhr.setRequestHeader("RequestVerificationToken", token);
            }
        }
    });
}

function showAwardPopup(badgeArray) {
    // Fetch badge details (name, image, etc.) from the server and populate the badge-info div.
    var baseUrl = window.location.origin;

    var badgeInfo = document.getElementById("badge-info");
    if (badgeInfo) badgeInfo.innerHTML = ""; // clear so repeated polls don't duplicate entries

    badgeArray.forEach(function (badgeId) {
        $.ajax({
            url: baseUrl + "/api/Badge/GetBadgeDetails/" + badgeId,
            type: "GET",
            success: function (badge) {
                var container = document.getElementById("badge-info");
                if (!container) return;

                // Build with textContent/attributes so badge data can never inject markup (XSS-safe).
                var title = document.createElement("h3");
                title.textContent = badge.name;

                var img = document.createElement("img");
                img.className = "badgeImg";
                img.src = "/api/Badge/GetBadgeImage/" + badge.id;
                img.alt = badge.name;

                var desc = document.createElement("p");
                desc.textContent = badge.description;

                container.appendChild(title);
                container.appendChild(img);
                container.appendChild(desc);

                var popup = document.getElementById("badge-award-popup");
                if (popup) popup.style.display = "block";
            }
        });
    });
}


function closeAwardPopup() {
    document.getElementById("badge-award-popup").style.display = "none";
}

document.getElementById("close-badge-popup")?.addEventListener("click", closeAwardPopup);

function checkForNewBadges() {
    // Get the base URL for your application
    var baseUrl = window.location.origin;

    $.ajax({
        url: baseUrl + "/api/Badge/CheckForNewBadges",
        type: "GET",
        success: function (badgeId) {
            if (badgeId) {
                // Display the badge award popup
                showAwardPopup(badgeId);
            }
        }
    });
}

// Poll for new badges only for authenticated users, once a minute (anonymous browsers used to
// hammer this endpoint every 6s and just get 302-redirected to login).
(function () {
    var authed = document.body && document.body.getAttribute('data-authenticated') === 'true';
    if (authed) {
        setInterval(checkForNewBadges, 60000);
    }
})();
