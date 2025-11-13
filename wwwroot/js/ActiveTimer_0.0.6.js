class ActiveTimer {

    /**
     * Creates an instance of ActiveTimer.
     * @param {number} initialTime - The initial value of the timer (in milliseconds).
     * @param {boolean} [countUp=true] - Determines if the timer counts up or down from its initial value.
     */
    constructor(initialTime, countUp = true) {
        this.countUp = countUp;
        this.totalSeconds = Math.round(initialTime / 1000);
        this.container = document.createElement('span');
        this.container.classList.add('timer');

        if (this.totalSeconds < 0) {
            this.container.classList.add('text-danger');
        }

        // Create time display elements
        this.componentTimes = {
            hours: document.createElement('span'),
            minutes: document.createElement('span'),
            seconds: document.createElement('span')
        };

        const separator = document.createElement('span');
        separator.innerText = ":";

        this.container.appendChild(this.componentTimes.hours);
        this.container.appendChild(separator.cloneNode(true));
        this.container.appendChild(this.componentTimes.minutes);
        this.container.appendChild(separator.cloneNode(true));
        this.container.appendChild(this.componentTimes.seconds);

        this.updateDisplay();
        setInterval(() => this.updateTime(), 1000);
    }

    /**
     * Updates the displayed time.
     */
    updateDisplay() {
        let absTime = Math.abs(this.totalSeconds);
        let hours = Math.floor(absTime / 3600);
        let minutes = Math.floor((absTime % 3600) / 60);
        let seconds = absTime % 60;

        this.componentTimes.hours.innerText = hours.toString().padStart(2, '0');
        this.componentTimes.minutes.innerText = minutes.toString().padStart(2, '0');
        this.componentTimes.seconds.innerText = seconds.toString().padStart(2, '0');

        // Toggle negative color class
        if (this.totalSeconds < 0) {
            this.container.classList.add('text-danger');
        } else {
            this.container.classList.remove('text-danger');
        }
    }

    /**
     * Updates the timer by incrementing or decrementing the total seconds.
     */
    updateTime() {
        this.totalSeconds += this.countUp ? 1 : -1;
        this.updateDisplay();
    }

    /**
     * The container element that holds the timer display.
     * @type {HTMLElement}
     */
    container;

    /**
     * An object containing references to the span elements representing 
     * the hours, minutes, and seconds of the timer.
     * 
     * @typedef {Object} ComponentTimes
     * @property {HTMLSpanElement} hours - The span element displaying the hours.
     * @property {HTMLSpanElement} minutes - The span element displaying the minutes.
     * @property {HTMLSpanElement} seconds - The span element displaying the seconds.
     */

    /**
     * Stores references to the span elements used to display the timer values.
     * @type {ComponentTimes}
     */
    componentTimes;

    /**
     * Determines the direction the timer is counting (true for up, false for down).
     * @type {boolean}
     */
    countUp = true;

    /**
     * True if the time is below 0;
     */
    isNegative = false;

    initialValue;
}