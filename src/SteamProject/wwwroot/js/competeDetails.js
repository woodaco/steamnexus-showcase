$(function () {
    // Delete is only rendered for the creator; guard so a missing button doesn't abort the script
    // (which previously also disabled the vote handler).
    var deleter = document.getElementById("compDeleteBtn");
    if (deleter) {
        var compId = deleter.value;
        deleter.onclick = function () {
            if (!confirm("Delete this competition? This cannot be undone.")) return;
            deleteCompetition(compId);
        };
    }

    // Vote to compete again. The voter is derived server-side (VoteController), so we only send
    // the competition id + intent. The antiforgery token is attached by the global $.ajaxSetup hook.
    var voter = document.getElementById("voteAgainBtn");
    if (voter) {
        voter.onclick = function () {
            var competitionId = parseInt(this.value, 10);
            $.ajax({
                type: 'POST',
                url: '/api/Vote/competitionvote',
                data: JSON.stringify({ competitionId: competitionId, wantsToPlayAgain: true }),
                contentType: 'application/json',
                success: function () {
                    // Reload so the "you voted" state + the rematch tally reflect the new vote.
                    location.reload();
                },
                error: function () {
                    if (window.SteamNexusToast) SteamNexusToast.error('Could not submit your vote.');
                }
            });
        };
    }
});

function deleteCompetition(compId) {
    $.ajax({
        type: "POST",
        dataType: "json",
        url: `/api/Steam/DeleteComp?compId=${compId}`,
        success: function () { location.href = `/compete/`; },
        error: function () {
            if (window.SteamNexusToast) SteamNexusToast.error('Could not delete the competition.');
        }
    });
}
