class AlertBanner {
    static idPool = 0;
    static assignId = () => AlertBanner.idPool++;
    static containerId = 'alert_banner_contaner';

    constructor(title, message, alert = null, timeout = -1, type = 'warning', dismissAction = null) {
        this.id = AlertBanner.assignId();

        const banner = document.createElement('div');
        banner.classList.add('alert', `alert-${type}`, 'alert-dismissible', 'fade', 'show');
        banner.setAttribute('role', 'alert');
        banner.setAttribute('style', 'top: 0; bottom: unset; z-index: 9002;');

        const bannerTitle = document.createElement('h5');
        bannerTitle.classList.add('alert-heading');
        bannerTitle.innerText = title;

        const bannerContent = document.createElement('p');
        if (message.innerHTML) {
            bannerContent.appendChild(message)
        }
        else {
            bannerContent.innerText = message;
        }

        const dismissBtn = document.createElement('button');
        dismissBtn.classList.add('btn-close');
        dismissBtn.dataset.bsDismiss = 'alert';
        dismissBtn.ariaLabel = 'Close';

        if (title) banner.appendChild(bannerTitle);
        banner.appendChild(bannerContent);
        banner.appendChild(dismissBtn);

        this.bannerElement = banner;
        this.bannerElement.id = `banner_${this.id}`;
        document.body.appendChild(this.bannerElement);

        if (timeout > 0) {
            this.timeout = setTimeout(() => {
                this.bannerElement.remove();
            }, timeout * 1000);
        }

        if (alert && alert.play && typeof (alert.play) === 'function') {
            alert.play();
        }

        if (dismissAction && typeof (dismissAction) === 'function') {
            this.bannerElement.addEventListener('closed.bs.alert', dismissAction);
            this.dismissHandler = dismissAction;
        }

        AlertBanner.activeBanners.push(this);
    }

    cancelTimeout() {
        if (this.timeout) {
            clearTimeout(this.timeout);
        }
    }

    enableClickToDismiss() {
        this.bannerElement.addEventListener('click', (e) => {
            if (this.dismissHandler) this.dismissHandler();
            e.currentTarget.remove();
        })
    }

    disableClickToDismiss() {
        this.bannerElement.removeEventListener('click', (e) => {
            if (this.dismissHandler) this.dismissHandler();
            e.currentTarget.remove();
        })
    }

    /**
     * Dismisses the current alert banner by removing it from the DOM.
     */
    dismiss() {
        this.bannerElement.remove();
        const index = AlertBanner.activeBanners.indexOf(this);
        if (index !== -1) AlertBanner.activeBanners.splice(index, 1);
    }

    /**
     * A list of all currently active AlertBanner instances.
     * @type {AlertBanner[]}
     */
    static activeBanners = [];

    /**
     * Dismisses an alert banner instance by its assigned banner ID.
     *
     * @param {number} bannerId - The ID of the banner to dismiss.
     * @throws {Error} If no banner with the specified ID is found.
     */
    static dismissById(bannerId) {
        const banner = this.activeBanners.find(v => v.id == bannerId);
        if (banner)
            banner.dismiss()
        else
            throw new Error(`AlertBanner with ID ${bannerId} not found.`);
    }

    id;
    bannerElement;
    timeout;
    dismissHandler = null;
}

class AlertAudio {
    constructor(src, loops = 1) {
        this.notificationSound = document.createElement("audio")
        this.notificationSound.setAttribute('src', src);
        this.notificationSound.addEventListener('ended', (event) => {
            this.loopCounter++;
            if (this.loopCounter % this.loops != 0) {
                this.notificationSound.play()
            }
            else {
                AlertAudio.soundQueue.pop()
                if (AlertAudio.soundQueue.length > 0) {
                    AlertAudio.soundQueue[0].notificationSound.play();
                }
            }
        })
        this.loops = loops;
    }

    /**
     * @type {Array<AlertAudio>}
     */
    static soundQueue = [];

    /**
     * @type {HTMLAudioElement}
     */
    notificationSound;

    loopCounter = 0;

    /**
     * @type {number}
     */
    loops;

    play() {
        if (AlertAudio.soundQueue.length > 0) {
            AlertAudio.soundQueue.unshift(this);
        }
        else {
            AlertAudio.soundQueue.unshift(this)
            this.notificationSound.play();
        }
    }
}

/**
 * Contains the default alert sounds as AlertAudio instances.
 */
const alerts = {
    newOrderAlert: new AlertAudio("/media/audio/newOrder.mp3"),
    timerLow: new AlertAudio("/media/audio/timerAlert2.wav", 2),
    notification: new AlertAudio("/media/audio/timerAlert.wav", 2)
}

class AlertMessage {
    static idPool = !sessionStorage.getItem("notification_idPool") ? 0 : parseInt(sessionStorage.getItem("notification_idPool"));

    /**
     * @type {AlertMessage[]}
     */
    static active = !sessionStorage.getItem("active_notifications") ? [] : JSON.parse(sessionStorage.getItem("active_notifications"));

    id;
    time;
    html;
    silent;

    constructor(id, time, html, silent) {
        this.id = id;
        this.time = new Date(time);
        this.html = html;
        this.silent = silent;
    }

    static notificationWindowOpen = false;
    static windowAnimationTimeout;

    static toggleNotificationWindow() {
        const notificationWindow = document.getElementById('notification_window');
        const notificationWindowBackdrop = document.getElementById('notification_window_backdrop');
        notificationWindow.classList.remove('open', 'opening', 'closed', 'closing');

        if (this.windowAnimationTimeout) {
            clearTimeout(this.windowAnimationTimeout);
        }

        //window closing
        if (this.notificationWindowOpen) {
            notificationWindow.classList.add('closing');
            this.windowAnimationTimeout = setTimeout(() => {
                notificationWindow.classList.remove('closing');
                notificationWindow.classList.add('closed');
                notificationWindowBackdrop.setAttribute('hidden', true);
                notificationWindowBackdrop.removeEventListener('click', this.closeNotificationWindow);
                this.notificationWindowOpen = false;
            }, 990);
        }

        //window opening
        else {
            AlertMessage.silenceAll();
            notificationWindow.classList.add('opening');
            this.windowAnimationTimeout = setTimeout(() => {
                notificationWindow.classList.remove('opening');
                notificationWindow.classList.add('open');
                notificationWindowBackdrop.removeAttribute('hidden');
                notificationWindowBackdrop.addEventListener('click', this.closeNotificationWindow);
                this.notificationWindowOpen = true;
            }, 990);
        }
    }

    static closeNotificationWindow() {
        const notificationWindow = document.getElementById('notification_window');
        const notificationWindowBackdrop = document.getElementById('notification_window_backdrop');
        notificationWindow.classList.remove('open', 'opening', 'closed', 'closing');
        notificationWindow.classList.add('closing');
        this.windowAnimationTimeout = setTimeout(() => {
            notificationWindow.classList.remove('closing');
            notificationWindow.classList.add('closed');
            notificationWindowBackdrop.setAttribute('hidden', true);
            notificationWindowBackdrop.removeEventListener('click', this.closeNotificationWindow);
            AlertMessage.notificationWindowOpen = false;
        }, 990);
    }

    static post(title, message, silent = false) {
        const id = AlertMessage.idPool++;
        const html =
            `
                <div id="notification_${id}" class="notification${silent ? ' silent' : ''}">
                    <div class="notification-title">
                        <span class="fw-bolder">${title} &mdash; <span class="notification-life" data-id="${id}">Just Now</span></span>
                        <button class="btn-notification-dismiss btn-close float-end me-2" data-id="${id}"></button>
                    </div>
                    <div class="notification-message">${message}</div>
                </div>
            `;

        const card = new AlertMessage(id, new Date(), html, silent)
        AlertMessage.active.push(card);
        sessionStorage.setItem("active_notifications", JSON.stringify(AlertMessage.active));
        sessionStorage.setItem("notification_idPool", AlertMessage.idPool)
        const element = $.parseHTML(html);
        $(element).on('click', (event) => {
            let id = $(event.target).data('id');
            AlertMessage.dismiss(id);
        });
        $('#notifications').prepend(element);
        if (!silent) {
            $('.active-notifications').addClass('fa-shake text-warning');
            alerts.notification.play();
        }

        return id;
    }

    static dismiss(id) {
        var index = -1;

        for (var i = 0; i < AlertMessage.active.length; i++) {
            if (AlertMessage.active[i].id == id) {
                index = i;
                break;
            }
        }

        if (index != -1) {
            AlertMessage.active.splice(index, 1);
            sessionStorage.setItem("active_notifications", JSON.stringify(AlertMessage.active));
            $(`#notification_${id}`).remove();
            let count = 0;
            for (var i = 0; i < AlertMessage.active.length; i++) {
                if (!AlertMessage.active[i].silent) count++;
            }
            if (count < 1) {
                $('.active-notifications').removeClass('fa-shake text-warning');
            }
        }
    }

    static getNotification(id) {
        let notification = null;
        for (var i = 0; i < AlertMessage.active.length; i++) {
            if (AlertMessage.active[i].id == id) {
                notification = AlertMessage.active[i];
                break;
            }
        }

        return notification;
    }

    static silenceAll() {
        $('.notification').addClass('silent');
        $('.active-notifications').removeClass('fa-shake text-warning');
        for (var i = 0; i < AlertMessage.active.length; i++) {
            AlertMessage.active[i].silent = true;
        }
        sessionStorage.setItem("active_notifications", JSON.stringify(AlertMessage.active));
    }

    static restoreActiveNotifications() {
        for (let i = 0; i < AlertMessage.active.length; ++i) {
            $('#notifications').prepend($.parseHTML(AlertMessage.active[i].html));
        }

        $('.btn-notification-dismiss').on('click', (event) => {
            let id = $(event.target).data('id');
            AlertMessage.dismiss(id);
        });

        $(document).on('notification-update', (event) => {
            $('.notification-life').each((index, element) => {
                const id = $(element).data('id');
                const info = AlertMessage.getNotification(id);
                const card = new AlertMessage(info.id, info.time, info.html);
                $(element).text(card.getNotificationLife());
            });
        });
    }

    getNotificationLife() {
        let msg = "Just Now"
        let timeNow = new Date();
        const life = Math.round((timeNow.getTime() - this.time.getTime()) / 1000);
        if (life > 3599) {
            let grammer = life < (3600 * 2) ? "hour" : "hours";
            msg = `${Math.floor(life / 3600)} ${grammer} ago.`
        }
        else if (life > 59) {
            let grammer = life < 120 ? "minute" : "minutes";
            msg = `${Math.floor(life / 60)} ${grammer} ago.`
        }
        else if (life > 30) {
            msg = `${life} seconds ago.`
        }

        return msg
    }
}

const notificationWindowBtns = document.getElementsByClassName('toggle-notification-window-btn');
for (var i = 0; i < notificationWindowBtns.length; i++) {
    notificationWindowBtns[i].addEventListener('click', (e) => {
        e.preventDefault();
        AlertMessage.toggleNotificationWindow();
    });
}

AlertMessage.restoreActiveNotifications();

const months = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];

setInterval(() => {
    const dateNow = new Date();

    // Update date
    document.querySelectorAll('.date').forEach(el => {
        el.textContent = `${months[dateNow.getMonth()]} ${dateNow.getDate()}, ${dateNow.getFullYear()}`;
    });

    // Update time (full format)
    document.querySelectorAll('.time').forEach(el => {
        el.textContent = dateNow.toLocaleTimeString();
    });

    // Update time (basic format)
    document.querySelectorAll('.time-basic').forEach(el => {
        const hours = dateNow.getHours();
        const minutes = dateNow.getMinutes();
        const ampm = dateNow.toLocaleTimeString().slice(-2);

        el.textContent = `${hours > 12 ? hours - 12 : hours}:${minutes < 10 ? "0" + minutes : minutes} ${ampm}`;
    });

    // Dispatch event for notification update
    document.dispatchEvent(new Event('notification-update'));
}, 1000);


const SESSION_START_KEY = 'SESSION_START';
const LAST_ALERT_KEY = 'LAST_ALERT_TIME';
const ALERT_COOLDOWN = 60 * 60 * 1000; // 1 hour in milliseconds
const CHECK_INTERVAL = 10 * 60 * 1000; // 10 minutes in milliseconds

if (!sessionStorage.getItem(SESSION_START_KEY)) {
    console.log('New session started');
    const SESSION_START = new Date().toISOString();
    sessionStorage.setItem(SESSION_START_KEY, SESSION_START);
}

function checkSessionDuration() {
    const sessionStarted = new Date(sessionStorage.getItem(SESSION_START_KEY));
    const now = new Date();
    const elapsedTime = now - sessionStarted;
    const lastAlertTime = sessionStorage.getItem(LAST_ALERT_KEY);

    if (elapsedTime > (20 * 60 * 60 * 1000)) { // 20-hour threshold
        if (!lastAlertTime || (now - new Date(lastAlertTime)) > ALERT_COOLDOWN) {
            AlertMessage.post(
                'Please Restart App',
                `This application has been running for ${(elapsedTime / (60 * 60 * 1000)).toFixed(1)} hours straight. It is recommended to periodically close the application or restart the computer to clear notifications and other possible errors at least once every 24 hours. Once you close the app and reopen it, this message will go away.`
            );
            sessionStorage.setItem(LAST_ALERT_KEY, now.toISOString());
        }
    }
}

// Run check on page load
checkSessionDuration();

// Re-check every 10 minutes
setInterval(checkSessionDuration, CHECK_INTERVAL);
