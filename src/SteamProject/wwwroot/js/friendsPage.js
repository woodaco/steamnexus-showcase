document.addEventListener('DOMContentLoaded', () => {
    const userId = document.querySelector('.userId').id
    const steamId = document.querySelector('.steamId').id
    const friendCards = document.querySelectorAll('.friend-card')
    const states = ["offline", "online", "busy", "away", "snooze"]
    const personas = document.querySelectorAll('.friend-persona')
    const games = document.querySelectorAll('.friend-game')
    const search = document.querySelector('#search-input')
    const wrapper = document.querySelector('.wrapper')

    const inviteModal = document.querySelector('#sendInviteModal')
    const emailError = document.querySelector('#email-error')
    const invBtn = document.querySelector('#send-inv')
    const emailInput = document.querySelector('#email-input')
    // const phoneInput = document.querySelector('#phone-input')

    const reverts = document.querySelectorAll('.revert')
    reverts.forEach((revert) => {
        revert.addEventListener('click', () => {
            let friendId = revert.id
            friendId = friendId.replace('-revert', ''); 
            fetch(`/api/Steam/revertNickname?friendSteamId=${friendId}`, {
                method: 'PATCH',
                headers: {
                    'Accept': 'application/json',
                    'Content-Type': 'text/html; charset=utf-8',
                    'RequestVerificationToken': (window.getAntiForgeryToken ? window.getAntiForgeryToken() : '')
                },
            })
            .then(() => {
                location.reload()
            })
        })
    })

    const createBox = (friend) => {
        let box = document.createElement('input')
        box.type = 'text'
        box.className = 'name-box'
        box.id = `${friend}-nickname`
        box.setAttribute('maxlength', '18')

        let friendId = friend
        friendId = friendId.replace('persona-', ''); // remove the 'name-' prefix

        box.addEventListener('keydown', (event) => {
            if (event.key == "Enter") {
                fetch(`/api/Steam/setNickname?friendSteamId=${friendId}&nickname=${box.value}`, {
                    method: 'PATCH',
                    headers: {
                        'Accept': 'application/json',
                        'Content-Type': 'text/html; charset=utf-8',
                        'RequestVerificationToken': (window.getAntiForgeryToken ? window.getAntiForgeryToken() : '')
                    },
                })
                .then((response) => {
                    if (response.status == 200) {
                        location.reload()
                    }
                    else {
                        console.log(response.status)
                    }
                })
            }
        })
        return box
    }

    const friendNames = document.querySelectorAll('.friend-name')
    friendNames.forEach((name) => {
        name.addEventListener('click', () => {
            let box = createBox(name.nextElementSibling.id)
            name.replaceWith(box)
        })
    })

    const phoneInput = document.querySelector('#phone-input')

    search.addEventListener('input', () => {
        wrapper.innerHTML = ""
        friendCards.forEach((card) => {
            if (card.id.toUpperCase().includes(search.value.toUpperCase())) {
                wrapper.appendChild(card)
            }
        })
    })

    const generateGradient = () => {
        let p1 = `${Math.floor(Math.random() * 100)}%`
        let p2 = `${Math.floor(Math.random() * 100)}%`
        return `radial-gradient(circle at ${p1} ${p2}, rgba(73, 49, 158, 1), rgba(92, 9, 130, 1) 72%)`
    }
    friendCards.forEach((card) => {
        card.style.backgroundImage = generateGradient()
    })

    const valid = (e) => {
        const exp = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/
        return exp.test(e);
    }

    invBtn.addEventListener('click', () => {
        let e = emailInput.value
        if (valid(e)) {
            emailError.style.visibility = "hidden"
            fetch(`/api/Steam/sendInvite?email=${e}`, {
                method: 'GET',
                headers: {
                    'Accept': 'application/json',
                    'Content-Type': 'text/html; charset=utf-8'
                },
            })
            .then(response => {
                if (response.status == 200) {
                    setTimeout(() => {
                        emailError.style.color = "green"
                        emailError.textContent = "Success!"
                        emailError.style.visibility = "visible"
                        setTimeout(() => {
                            emailError.style.visibility = "hidden"
                            inviteModal.style.display = "none";
                            document.querySelector('.modal-backdrop').remove();
                        }, 1000)
                    }, 1000)
                    
                }
                else {
                    setTimeout(() => {
                        emailError.style.visibility = "visible"
                        emailError.style.color = "red"
                        emailError.textContent = "Error Occured When Sending Invite, Try Again Later"
                    }, 1500)
                }
            })
        } else {
            emailError.style.color = "red"
            emailError.textContent = "Please enter a valid email in the correct format!"
            emailError.style.visibility = "visible"
        }
    })

    // Show skeleton loading state for friend status
    const showFriendStatusLoading = () => {
        personas.forEach((persona) => {
            persona.innerHTML = '<span class="sn-skeleton sn-skeleton-text" style="width: 60px; height: 12px;"></span>';
        });
        games.forEach((game) => {
            game.innerHTML = '<span class="sn-skeleton sn-skeleton-text" style="width: 120px; height: 12px;"></span>';
        });
    };

    const fetchStatus = (steamId, userId) => {
        fetch(`/api/Steam/friends?steamid=${steamId}&UserId=${userId}`)
        .then((response) => response.json())
        .then((data) => {
            if (data == null) {
                // Clear loading states if no data
                personas.forEach(p => p.innerHTML = '');
                games.forEach(g => g.innerHTML = '');
                return
            }
            data.forEach((friend) => {
                personas.forEach((persona) => {
                    if (friend.steamId == persona.id) {
                        persona.innerHTML = states[friend.personaState]
                    }
                })
                games.forEach((game) => {
                    if (friend.steamId == game.id){
                        game.innerHTML = friend.gameExtraInfo
                    }
                })
            })
        })
        .catch((error) => {
            console.error('Failed to fetch friend status:', error);
            personas.forEach(p => p.innerHTML = '<span class="text-muted">--</span>');
            games.forEach(g => g.innerHTML = '');
        });
    }

    // Show loading state initially
    showFriendStatusLoading();
    fetchStatus(steamId, userId)
    setInterval(() => {
        fetchStatus(steamId, userId)
    }, 60000)

    // Block / remove friend (owner-checked + antiforgery on the server).
    const friendAction = (url, friendId) =>
        fetch(`${url}?friendId=${friendId}`, {
            method: 'POST',
            headers: { 'RequestVerificationToken': (window.getAntiForgeryToken ? window.getAntiForgeryToken() : '') }
        })
    const wireFriendBtn = (selector, url, confirmMsg) => {
        document.querySelectorAll(selector).forEach((btn) => {
            btn.addEventListener('click', (e) => {
                e.preventDefault()
                if (confirmMsg && !confirm(confirmMsg)) return
                const fid = btn.getAttribute('data-friend-id')
                friendAction(url, fid).then((r) => {
                    if (r.ok) location.reload()
                    else if (window.SteamNexusToast) SteamNexusToast.error('That action could not be completed.')
                })
            })
        })
    }
    wireFriendBtn('.block-friend', '/api/Steam/blockFriend', null)
    wireFriendBtn('.remove-friend', '/api/Steam/removeFriend', 'Remove this friend from your SteamNexus list?')
    wireFriendBtn('.unblock-friend', '/api/Steam/unblockFriend', null)
})
