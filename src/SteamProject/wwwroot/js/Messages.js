// Messages page JavaScript - chat functionality
// ES module: reuses the pure backoff helpers in utils.js. Keep the
// <script type="module"> tag in _Messages.cshtml in sync with this import.
import { nextPollDelayMs, messageBadgeDecision, formatMessageTime } from './utils.js';

document.addEventListener('DOMContentLoaded', () => {
    let msgsBtn = document.querySelector('.messages-btn')
    let msgAlert = document.querySelector('.message-alert')
    let msgContainer = document.querySelector('.message-container')   
    msgContainer.style.visibility = "hidden"
    let msgExit = document.querySelector('#message-exit')
    let messId = document.querySelector('.messengerId').textContent
    let contacts = document.querySelector('.contacts')
    let contactHeader = document.querySelector('.contact-header')
    let msgContent = document.querySelector('.messages-for-contact')
    let msgTextBox = document.querySelector('.message-text-box')
    let sendBtn = document.querySelector('#send-message-btn')
    let msgFooter = document.querySelector('.send-message')
    var msgCount;
    let baseUrl = location.origin;

    const ncode = (s) => {
        let o = s.replace(/<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>/gi, function(m) {
            return m.replace(/</g, '&lt;').replace(/>/g, '&gt;');
        });
        return o;
    }

    const initialCount = () => {
        fetch(`/api/Messages/messageCount`)
        .then(response => response.json())
        .then((data) => {
            msgCount = data;
        })
    }

    // msgCount is the last unread count observed (drives the poll's "is there
    // something new in the open thread?" check). The badge itself is driven
    // straight off the count by messageBadgeDecision — it goes on when there are
    // unread messages and the container is closed, and off the moment the count
    // reaches 0. The old `data > msgCount` comparison lit the badge only on a
    // RISING edge: a message that arrived before the initial count resolved, or
    // one already present on the page, never showed the badge, and reading the
    // messages in another tab never cleared it because nothing lowered the
    // comparison.
    const getCount = () => {
        fetch(`/api/Messages/messageCount`)
        .then(response => response.json())
        .then((data) => {
            const previous = msgCount
            const decision = messageBadgeDecision({
                previousCount: previous,
                newCount: data,
                containerVisible: msgContainer.style.visibility === "visible"
            })
            msgAlert.style.display = decision.showBadge ? "flex" : "none"
            // Reload the open conversation only when the count went UP — a count
            // that fell (messages read in another tab) is not a new message here.
            if (decision.reloadThread) {
                const currentId = document.querySelector('.specific-contact-img')?.id
                if (currentId) {
                    loadContentFor(currentId)
                }
            }
            msgCount = data
        })
    }

    // Self-rescheduling, visibility-aware unread-count poll. Replaces the old
    // fixed `setInterval(() => getCount(), 1000)`, which fired once a second on
    // every authenticated page, in every open tab, forever. The interval now
    // comes from nextPollDelayMs (a long base; short while the container is
    // open), the poll pauses entirely while the tab is hidden, and a single
    // timer handle is kept so a re-open or a bfcache restore can't stack a
    // second poll loop on top of the first.
    let pollTimer = null
    const isContainerOpen = () => msgContainer.style.visibility === "visible"
    const schedulePoll = () => {
        if (pollTimer !== null) {
            clearTimeout(pollTimer)
            pollTimer = null
        }
        const delay = nextPollDelayMs({
            visibilityState: document.visibilityState,
            containerOpen: isContainerOpen()
        })
        // delay === 0 means the tab is hidden: no timer, we resume from the
        // visibilitychange handler below.
        if (delay <= 0) {
            return
        }
        pollTimer = setTimeout(() => {
            pollTimer = null
            getCount()
            schedulePoll()
        }, delay)
    }
    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
            getCount() // resume with an immediate fetch, then reschedule
        }
        schedulePoll()
    })

    const getContacts = () => {
        let url = baseUrl + `/api/Messages/userContacts`
        fetch(url)
        .then((response) => response.json())
        .then((data) => {
            let r = data.contacts
            r.forEach(contact => {
                // Build via DOM properties (not innerHTML) so a contact's Steam name/nickname/avatar can
                // never inject markup. className/id keep the existing class structure the click handler reads.
                const div = document.createElement('div')
                div.className = `contact ${contact.name} ${contact.nickname}`
                div.id = contact.id
                const img = document.createElement('img')
                img.className = 'contact-img'
                img.src = contact.avatar
                img.alt = ''
                div.appendChild(img)
                contacts.appendChild(div)
            });
            let contactBtns = document.querySelectorAll('.contact')
            contactBtns.forEach((contact) => {
                if (contact.id == "69420") {
                    contact.addEventListener('click', () => loadContentForSystemBot())
                } else {
                    if (contact.classList[2] != "null") {
                        let avt = contact.firstChild.src
                        let name = contact.classList[2]
                        let id = contact.id
                        contact.addEventListener('click', () => {
                            loadHeaderFor(avt, name, id)
                            loadContentFor(id)
                            loadFooterFor(id)
                        })
                    } else {
                        let avt = contact.firstChild.src
                        let name = contact.classList[1]
                        let id = contact.id
                        contact.addEventListener('click', () => {
                            loadHeaderFor(avt, name, id)
                            loadContentFor(id)
                            loadFooterFor(id)
                        })
                    }
                }
            })
        })
    }

    const loadContentForSystemBot = () => {
        contactHeader.innerHTML = `<i class="bi bi-robot specific-contact-img" id="69420"></i><div class="contact-name">SteamNexus</div>`
        msgFooter.style.display = "none"
        msgContent.innerHTML = ""
        // The contact whose thread we're opening IS the system sender; the server derives "me" from auth.
        let url = baseUrl + `/api/Messages/messagesBetween?toId=69420`
        fetch(url)
        .then((response) => response.json())
        .then((data) => {
            let msgs = data.messages
            msgs.forEach((msg) => {
                // Build with textContent so message bodies can never inject markup/script (stored XSS).
                const wrap = document.createElement("div")
                wrap.className = "from-message"
                const content = document.createElement("div")
                content.className = "from-message-content"
                content.textContent = msg.content
                const time = document.createElement("div")
                time.className = "from-message-time"
                time.textContent = `sent at ${formatMessageTime(msg.time)}`
                wrap.appendChild(content)
                wrap.appendChild(time)
                msgContent.appendChild(wrap)
            })
            msgContent.scrollTop = msgContent.scrollHeight;
        })
    }

    const loadHeaderFor = (avt, name, id) => {
        contactHeader.innerHTML = ""
        // Header
        let cImg = document.createElement('img')
        cImg.className = "specific-contact-img"
        cImg.src = avt
        cImg.id = id
        contactHeader.innerHTML += `${cImg.outerHTML}`

        let cName = document.createElement('div')
        cName.className = "contact-name"
        cName.innerText = name
        contactHeader.innerHTML += `${cName.outerHTML}`
    }

    const loadContentFor = (id) => {
        msgContent.innerHTML = ""
        // `id` is the contact's domain id (the "to" side); the server derives "me" from auth.
        let url = baseUrl + `/api/Messages/messagesBetween?toId=${id}`
        fetch(url)
        .then((response) => response.json())
        .then((data) => { 
            let msgs = data.messages
            msgs.forEach((msg) => {
                if (msg.from == id) {
                    let msgCnt = document.createElement('div')
                    msgCnt.className = "from-message-content"
                    msgCnt.textContent = msg.content
                    msgContent.innerHTML += `
                    <div class="from-message"> 
                        ${msgCnt.outerHTML}
                        <div class="from-message-time">${formatMessageTime(msg.time)}</div>
                    </div>
                    `
                } else {
                    let msgCnt = document.createElement('div')
                    msgCnt.className = "to-message-content"
                    msgCnt.textContent = msg.content
                    msgContent.innerHTML += `
                    <div class="to-message"> 
                        ${msgCnt.outerHTML}
                        <div class="to-message-time">${formatMessageTime(msg.time)}</div>
                    </div>
                    `
                }
            });
            msgContent.scrollTop = msgContent.scrollHeight;
        })
    }

    const loadFooterFor = (id) => {
        // Message Box
        msgFooter.style.display = "flex"
        msgTextBox.id = id
        toId = id
        msgTextBox.addEventListener('keydown', (event) => {
            if (event.key == "Enter") {
                sendMsg(toId, messId, msgTextBox.value)
            }
        })
        sendBtn.addEventListener('click', () => {
            sendMsg(toId, messId, msgTextBox.value)
        })
    }

    const displayMsgs = () => {
        msgAlert.style.display = "none"
        msgsBtn.style.visibility = "hidden"
        msgContainer.style.visibility = "visible"
        msgFooter.style.display = "none"
    }

    const hideMsgs= () => {
        msgsBtn.style.visibility = "visible"
        msgContainer.style.visibility = "hidden"
    }

    const sendMsg = (to, from, msg) => {
        // Sender is derived server-side; send the body as JSON so & and # are preserved.
        let url = baseUrl + `/api/Messages/sendMessageTo`
        fetch(url, {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
                'RequestVerificationToken': (window.getAntiForgeryToken ? window.getAntiForgeryToken() : '')
            },
            body: JSON.stringify({ toId: to, message: msg })
        })
        .then((data) => {
            if (data.status == 200) {
                msgContent.innerHTML += 
                `
                <div class="to-message">
                    <div class="to-message-content">
                        ${ncode(msg)}
                    </div>
                    <div class="to-message-time">
                        sent at ${formatMessageTime(new Date().toISOString())}
                    </div>
                </div>
                `
                msgContent.scrollTop = msgContent.scrollHeight;
            }
        })
        msgTextBox.value = ""        
    }

    msgExit.addEventListener('click', () => hideMsgs());
    msgsBtn.addEventListener('click', () => displayMsgs());

    getContacts()
    initialCount()
    schedulePoll()
})