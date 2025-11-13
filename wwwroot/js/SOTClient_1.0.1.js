let initialized = false;

/**
 * Represents a sanitized order object, stripped of circular references
 * to ensure compatibility with JSON serialization.
 * 
 * @typedef {Object} SanitizedOrder
 * @property {number} id - The unique identifier for the order.
 * @property {Date} orderPlaced - The timestamp when the order was placed.
 * @property {Date|null} orderInprogress - The timestamp when the order moved to "In Progress" (if applicable).
 * @property {Date|null} orderReady - The timestamp when the order became ready for pickup (if applicable).
 * @property {Date|null} orderComplete - The timestamp when the order was completed (if applicable).
 * @property {Object|null} customer - The customer associated with the order, or null if unavailable.
 * @property {Object|null} discount - The discount applied to the order, with circular references removed.
 * @property {SanitizedOrderLine[]} lineItems - A list of order line items, with menu items and ingredients sanitized.
 */

/**
 * Represents a sanitized order line item.
 * 
 * @typedef {Object} SanitizedOrderLine
 * @property {number} id - The unique identifier for the line item.
 * @property {number} orderId - The ID of the order this line item belongs to.
 * @property {number} menuItemVarient - Index of the choosen MenuItemVarient.
 * @property {string | null} lineComments - Customer comments/special requests.
 * @property {SanitizedMenuItem} menuItem - The menu item associated with this order line.
 * @property {SanitizedIngredient[]} ingredients - The list of ingredients included in this line item.
 * @property {SanitizedIngredient[]} addedIngredients - Ingredients that were added to the item.
 * @property {SanitizedIngredient[]} removedIngredients - Ingredients that were removed from the item.
 */

/**
 * Represents a sanitized menu item.
 * 
 * @typedef {Object} SanitizedMenuItem
 * @property {number} id - The unique identifier for the menu item.
 * @property {string} name - The name of the menu item.
 * @property {number} price - The price of the menu item.
 * @property {SanitizedMenuItemVariant[]} menuItemVarients - The available variants of the menu item.
 */

/**
 * Represents a sanitized menu item variant.
 * 
 * @typedef {Object} SanitizedMenuItemVariant
 * @property {number} id - The unique identifier for the variant.
 * @property {string} descriptor - The descriptor for this variant (e.g., "Large", "Extra Spicy").
 * @property {number} price - The price difference for this variant.
 */

/**
 * Represents a sanitized ingredient.
 * 
 * @typedef {Object} SanitizedIngredient
 * @property {number} id - The unique identifier for the ingredient.
 * @property {string} name - The name of the ingredient.
 * @property {number} price - The additional price for the ingredient (if applicable).
 */

/**
 * Represents the status of a printer as returned by the server.
 * This object provides details about the printer's connection state,
 * paper status, error conditions, and queued print jobs.
 *
 * @typedef {Object} PrinterStatus
 * @property {string} lastOnline - The last recorded time the printer was online (ISO 8601 format).
 * @property {boolean} isPaperLow - Whether the printer's paper supply is low.
 * @property {boolean} isPaperOut - Whether the printer is out of paper.
 * @property {boolean} isErrorState - Whether the printer is in an error state.
 * @property {boolean} isBridgeActive - Whether the printer has reported online within the last minute.
 * @property {boolean} isConnected - Whether the printer is currently connected.
 * @property {boolean} isReady - Whether the printer is ready to receive jobs (must be connected and have an active bridge).
 * @property {boolean} isCoverOpen - Whether the printer's cover is open.
 * @property {number} jobsWaiting - The number of print jobs currently in the queue.
 * @property {Object.<string, PrintJob>} queue - A dictionary of queued print jobs, keyed by job ID.
 */

const convertToPrettyPhone = input => {
	let output = '(';

	for (let i = 0; i < input.length; ++i) {
		if (i == 2)
			output += `${input[i]}) `;
		else if (i == 5)
			output += `${input[i]}-`;
		else
			output += input[i];
	}

	return output;
}

const convertToPrettyMoney = input => {
	input = parseFloat(input).toFixed(2);
	return `$${input}`;
}

/**
 * Info button clicked event
 * @param {Event} e
 */
const infoBtnEvent = e => {
	const orderNum = e.currentTarget.dataset.orderNum;
	/**
	 * @type {SanitizedOrder}
	 */
	const orderContent = JSON.parse(e.currentTarget.dataset.orderContent);
	const idRoot = 'order_info_modal';
	const combineId = id => `${idRoot}_${id}`;
	const modal = new bootstrap.Modal(document.getElementById('order_info_modal'));

	document.getElementById(combineId('title')).innerText = `Order: #${orderNum}`;
	document.getElementById(combineId('customer_name')).innerText = orderContent.customer.name;
	document.getElementById(combineId('customer_phone')).innerText = convertToPrettyPhone(orderContent.customer.phone);
	document.getElementById(combineId('customer_email')).innerText = orderContent.customer.email;

	for (var i = 0; i < orderContent.lineItems.length; i++) {
		const itemName = orderContent.lineItems[i].menuItem.name;
		const itemPrice = convertToPrettyMoney(orderContent.lineItems[i].menuItem.price);
		const itemVarient = orderContent.lineItems[i].menuItem.menuItemVarients[orderContent.lineItems[i].menuItemVarient].descriptor;

		const orderLine = document.createElement('li');
		orderLine.innerHTML = `${itemVarient} ${itemName} &mdash; ${itemPrice}`;

		if (orderContent.lineItems[i].addedIngredients.length > 0) {
			const innerList = document.createElement('ul');
            for (var j = 0; j < orderContent.lineItems[i].addedIngredients.length; j++) {
				const added = orderContent.lineItems[i].addedIngredients[j];
				const li = document.createElement('li');
				li.innerHTML = `Add: ${added.name} &mdash; ${convertToPrettyMoney(added.price)}`;
				innerList.appendChild(li);
			}
			orderLine.appendChild(innerList);
		}

		if (orderContent.lineItems[i].removedIngredients.length > 0) {
			const innerList = document.createElement('ul');
			for (var j = 0; j < orderContent.lineItems[i].removedIngredients.length; j++) {
				const removed = orderContent.lineItems[i].removedIngredients[j];
				const li = document.createElement('li');
				li.innerHTML = `Remove: ${removed.name}`;
				innerList.appendChild(li);
			}
			orderLine.appendChild(innerList);
		}

		if (orderContent.lineItems[i].lineComments && orderContent.lineItems[i].lineComments !== '') {
			const comment = document.createElement('div');
			comment.innerHTML = `Comments: ${orderContent.lineItems[i].lineComments}`;
			orderLine.appendChild(comment);
		}

		document.getElementById(combineId('order_detail')).appendChild(orderLine);
    }

	document.getElementById(combineId('subtotal')).innerText = `Subtotal: ${convertToPrettyMoney(orderContent.subtotal)}`;
	document.getElementById(combineId('tax')).innerText = `     Tax: ${convertToPrettyMoney(orderContent.tax)}`;
	document.getElementById(combineId('total')).innerText = `   Total: ${convertToPrettyMoney(orderContent.total)}`;
	modal.show();
}

/**
 * Next or Done button clicked event
 * @param {Event} e
 */
const advanceOrderBtnEvent = e => {
	const orderNum = e.currentTarget.dataset.orderNum;

	fetch('/API/Order/UpdateStatus', {
		method: 'PUT',
		headers: {
			'Content-Type': 'application/json'
		},
		body: JSON.stringify({
			OrderId: orderNum
		})
	})
		.then(response => {
			if (response.ok) {
				loadOrders();
				return response.json()
			}
		})
		.then(data => {
			console.log(data.message);
		});
}

/**
 * Edit Order button clicked event
 * @param {Event} e
 */
const editBtnEvent = e => {
	const orderNum = e.currentTarget.dataset.orderNum;
	const URL = `/Staff/OrderTerminal/Edit/${orderNum}`;

	window.location.href = URL;
}

/**
 * Cancel order button clicked event
 * @param {Event} e
 */
const cancelBtnEvent = e => {
	const orderNum = e.currentTarget.dataset.orderNum;
	/**
	 * @type {SanitizedOrder}
	 */
	const order = JSON.parse(e.currentTarget.dataset.orderContent);
	const confirmModal = new bootstrap.Modal(document.getElementById('confirm_cancel_modal'));

	document.getElementById('cancel_warning_text').innerText = `Are you sure you want to cancel order #${orderNum} for ${order.customer.name}? This action cannot be undone!`;
	document.getElementById('confirm_cancel_btn').addEventListener('click', () => {
		fetch('/API/Order/CancelOrder', {
			method: 'DELETE',
			headers: {
				'Content-Type': 'application/json'
			},
			body: JSON.stringify({
				orderId: orderNum
			})
		})
			.then(result => {
				if (result.ok) {
					loadOrders();
				}
			})

		confirmModal.hide();
	});

	document.getElementById('confirm_cancel_modal').addEventListener('hidden.bs.modal', () => {
		document.getElementById('confirm_cancel_modal').removeEventListener('hidden.bs.modal');
		document.getElementById('confirm_cancel_btn').removeEventListener('click');
	});

	confirmModal.show();
}

/**
 * Print order button clicked event
 * @param {Event} e
 */
const printBtnEvent = e => {
	const orderNum = e.currentTarget.dataset.orderNum;
	fetch('/API/Print/PrintOrderTicket', {
		method: 'POST',
		headers: {
			'Content-Type': 'application/json'
		},
		body: JSON.stringify({
			orderId: orderNum
		})
	})
	.then(result => {
		if (result.ok) {
			loadOrders();
		}
	})
}

const btnIconContainer = document.createElement('span');
btnIconContainer.className = 'fa-stack fa-2x order-btn';

const btnIconOuter = document.createElement('i');
btnIconOuter.className = 'fa-sharp fa-solid fa-circle fa-stack-2x text-primary'

const createBtnIcon = (iconClass) => {
	const container = btnIconContainer.cloneNode();
	const outer = btnIconOuter.cloneNode();
	const inner = document.createElement('i');
	inner.className = 'fa-sharp fa-light fa-stack-1x text-white ' + iconClass;

	container.appendChild(outer);
	container.appendChild(inner);

	return container;
}

const infoBtn = document.createElement('a');
infoBtn.href = "#";
infoBtn.className = 'order-info-btn ms-auto';
infoBtn.dataset.function = 'infoBtn';
infoBtn.appendChild(createBtnIcon('fa-info'));

const nextBtn = document.createElement('a');
nextBtn.href = "#";
nextBtn.className = 'order-next-btn ms-auto';
nextBtn.dataset.function = 'nextBtn';
nextBtn.appendChild(createBtnIcon('fa-arrow-right'));

const editBtn = document.createElement('a');
editBtn.href = "#";
editBtn.className = 'order-edit-btn ms-auto';
editBtn.dataset.function = 'editBtn'
editBtn.appendChild(createBtnIcon('fa-pen-to-square'));

const cancelBtn = document.createElement('a');
cancelBtn.href = "#";
cancelBtn.className = 'order-cancel-btn ms-auto';
cancelBtn.dataset.function = 'cancelBtn';
cancelBtn.appendChild(createBtnIcon('fa-ban'));

const printBtn = document.createElement('a');
printBtn.href = "#";
printBtn.className = 'order-print-btn ms-auto';
printBtn.dataset.function = 'printBtn';
printBtn.appendChild(createBtnIcon('fa-print'));

const doneBtn = document.createElement('a');
doneBtn.href = "#";
doneBtn.className = 'order-complete-btn ms-auto';
doneBtn.dataset.function = 'doneBtn';
doneBtn.appendChild(createBtnIcon('fa-check'));

/**
 * Maps button elements to their corresponding event handlers.
 * @type {Map<HTMLElement, Function>}
 */
const btnEventHandlers = new Map([
	["infoBtn", infoBtnEvent],
	["editBtn", editBtnEvent],
	['cancelBtn', cancelBtnEvent],
	['printBtn', printBtnEvent],
	['nextBtn', advanceOrderBtnEvent],
	['doneBtn', advanceOrderBtnEvent]
]);

/**
 * @typedef {"received" | "in-progress" | "ready" | "complete"} StageName
 */

const orderStages = [
	'received',
	'in-progress',
	'ready',
	'complete'
];

/**
 * A mapping of order stages to their corresponding action buttons.
 * 
 * @typedef {Object} StageButtons
 * @property {HTMLAnchorElement[]} received - Buttons available in the "received" stage.
 * @property {HTMLAnchorElement[]} in-progress - Buttons available in the "in-progress" stage.
 * @property {HTMLAnchorElement[]} ready - Buttons available in the "ready" stage.
 * @property {HTMLAnchorElement[]} complete - Buttons available in the "complete" stage.
 */
const stageBtns = {
	"received": [
		infoBtn,
		editBtn,
		cancelBtn,
		printBtn,
		nextBtn
	],
	"in-progress": [
		infoBtn,
		editBtn,
		cancelBtn,
		printBtn,
		nextBtn
	],
	"ready": [
		infoBtn,
		cancelBtn,
		printBtn,
		doneBtn,
	],
	"complete": [
		infoBtn,
		printBtn
	]
}

/**
 * 
 * @param {StageName} stage
 * @param {SanitizedOrder} order
 * @returns
 */
const createOrderCard = (stage, order) => {
	/**
	 * @type {HTMLElement[]}
	 */
	const btns = stageBtns[/** @type {StageName} */ (stage)];
	const card = document.createElement('li');
	card.className = `list-group-item order-terminal-item d-flex row justify-content-between order-terminal-item-${stage}`;
	card.id = order.id;
	card.dataset.orderId = order.id;

	const orderHeadContainer = document.createElement('h4');
	orderHeadContainer.classList.add('col-12', 'row');

	const orderNum = document.createElement('div');
	orderNum.classList.add('col');
	orderNum.innerText = `Order# ${order.id}`;

	const orderPromo = document.createElement('div');
	orderPromo.classList.add('col');
	orderPromo.innerText = order.discountId ? `Promo Code ${order.discountId} used!` : '';

	orderHeadContainer.appendChild(orderNum);
	orderHeadContainer.appendChild(orderPromo);

	if (stage != 'complete') {
		let stageStarted = new Date();
		let timeStart = 0;
		switch (stage) {

			case 'received':
				stageStarted = new Date(order.orderPlaced + 'Z');
				break;
			case 'in-progress':
				stageStarted = new Date(order.orderInprogress + 'Z');
				break;
			case 'ready':
				stageStarted = new Date(order.orderReady + 'Z');
				break;
		}

		const timerContainer = document.createElement('div');
		timerContainer.classList.add('col', 'ms-auto', 'text-end');

		const now = new Date();
		const timerLabel = document.createElement('span');
		timerLabel.classList.add('fw-bold', 'text-uppercase');
		timerLabel.innerHTML = (stage != 'in-progress' ? 'Time in Stage' : 'Time Until Ready') + ': ';

		if (stage == 'in-progress') {
			stageStarted.setMinutes(stageStarted.getMinutes() + order.minutesToReady)
			timeStart = stageStarted - now;
		}
		else {
			timeStart = now - stageStarted;
		}

		const timer = new ActiveTimer(timeStart, stage != 'in-progress');
		timerContainer.appendChild(timerLabel);
		timerContainer.appendChild(timer.container);
		orderHeadContainer.appendChild(timerContainer);
	}

	card.appendChild(orderHeadContainer);

	const orderInfo = document.createElement('div');
	orderInfo.classList.add('col');

	const customerName = document.createElement('h5');
	customerName.innerText = order.customer.name;
	orderInfo.appendChild(customerName);

	const customerPhone = document.createElement('h6');
	customerPhone.innerText = convertToPrettyPhone(order.customer.phone);
	orderInfo.appendChild(customerPhone);

	const lineItemsList = document.createElement('ul');
	lineItemsList.classList.add('order-line-list');

    switch (stage) {
		case 'received':
		case 'in-progress':
			for (let i = 0; order.lineItems && i < order.lineItems.length; ++i) {
				const lineItem = document.createElement('li');
				lineItem.classList.add('order-line-item');
				
				const itemName = document.createElement('div');
				itemName.innerHTML = `${order.lineItems[i].menuItem.menuItemVarients[order.lineItems[i].menuItemVarient].descriptor} &mdash; ${order.lineItems[i].menuItem.name}`;
				lineItem.appendChild(itemName);

				let modifiedItem = false;
				if (order.lineItems[i].addedIngredients && order.lineItems[i].addedIngredients.length > 0) {
					modifiedItem = true;

					const header = document.createElement('h6');
					header.innerText = 'Add:';
					lineItem.appendChild(header);

					const ingredients = document.createElement('ul');
					ingredients.classList.add('mod-list');

					for (var j = 0; j < order.lineItems[i].addedIngredients.length; j++) {
						const ingredient = document.createElement('li');
						ingredient.classList.add('mod-list-item');
						ingredient.innerText = order.lineItems[i].addedIngredients[j].name;
						ingredients.appendChild(ingredient);
					}

					lineItem.appendChild(ingredients);
				}

				if (order.lineItems[i].removedIngredients && order.lineItems[i].removedIngredients.length > 0) {
					modifiedItem = true;

					const header = document.createElement('h6');
					header.innerText = 'Remove:';
					lineItem.appendChild(header);

					const ingredients = document.createElement('ul');
					ingredients.classList.add('mod-list');

					for (var j = 0; j < order.lineItems[i].removedIngredients.length; j++) {
						const ingredient = document.createElement('li');
						ingredient.classList.add('mod-list-item');
						ingredient.innerText = order.lineItems[i].removedIngredients[j].name;
						ingredients.appendChild(ingredient);
					}

					lineItem.appendChild(ingredients);
				}

				if (order.lineItems[i].lineComments) {
					modifiedItem = true;

					const lineCommentHeader = document.createElement('h6');
					lineCommentHeader.innerText = "Line Comments:";
					lineItem.appendChild(lineCommentHeader);

					const lineComment = document.createElement('p');
					lineComment.innerText = order.lineItems[i].lineComments;
					lineItem.appendChild(lineComment);
				}

				if (!modifiedItem) {
					const noMod = document.createElement('h6');
					noMod.innerText = "As-Is No Modifications";
					lineItem.appendChild(noMod);
				}

				lineItemsList.appendChild(lineItem);
			}
			break;

		case 'ready':
		case 'complete':
			for (let i = 0; order.lineItems && i < order.lineItems.length; ++i) {
				const lineItem = document.createElement('li');
				lineItem.classList.add('order-line-item');

				const itemName = document.createElement('div');
				itemName.innerHTML = `${order.lineItems[i].menuItem.menuItemVarients[order.lineItems[i].menuItemVarient].descriptor} &mdash; ${order.lineItems[i].menuItem.name}`;
				lineItem.appendChild(itemName);

				lineItemsList.appendChild(lineItem);
			}
			
			break;
	}

	orderInfo.appendChild(lineItemsList);
	card.appendChild(orderInfo);

	const btnPannelOuter = document.createElement('div');
	btnPannelOuter.className = "col-4 order-terminal-btn-container d-flex mt-2"

	const btnPannelInner = document.createElement('div');
	btnPannelInner.className = "ms-auto";

	btns.forEach(btn => {
		const clone = btn.cloneNode(true);
		clone.dataset.orderNum = order.id;
		clone.dataset.orderContent = JSON.stringify(order);
		clone.addEventListener('click', e => {
			e.preventDefault();
			const handler = btnEventHandlers.get(btn.dataset.function);
			handler(e);
		});
		btnPannelInner.appendChild(clone);
	});

	btnPannelOuter.appendChild(btnPannelInner);
	card.appendChild(btnPannelOuter)
	card.dataset.orderStage = order.stage;

	return card;
}

//bannerIds
let connectionTimeoutBannerId = 0;
let reconnectingBannerId = 0;

// Function to handle the fetch with timeout
const fetchWithTimeout = (url, options, timeout = 10000) => {
	const controller = new AbortController();
	const timeoutId = setTimeout(() => controller.abort(), timeout);

	return fetch(url, {
		...options,
		signal: controller.signal
	})
		.finally(() => clearTimeout(timeoutId));
};

const MAX_RETRY_LOAD = 2;
const FAILED_LOAD_TIMEOUT = 30000; // 30 seconds
let failedLoad = 0;

const loadOrders = () => {
	const orderCards = [];

	// Call the fetch with a timeout
	fetchWithTimeout('/API/Staff/TerminalService/FetchOrders', { method: 'GET' }, FAILED_LOAD_TIMEOUT)
		.then(response => response.json())
		.then(orders => {
			if (failedLoad > 0) {
				AlertMessage.post(
					"Server Response Slow",
					`The server took an unusually long time to retrieve the orders list, requiring ${failedLoad} attempt${failedLoad > 1 ? 's' : ''} to respond. If this message appears frequently, you may want to contact support. Otherwise, feel free to dismiss it.`,
					true //silent
				);
            }

			failedLoad = 0; //resets failed load counter

			let counters = [0, 0, 0, 0];
			let unconfirmedOrderIds = [];

			// Process orders
			for (let i = 0; i < orders.length; i++) {
				counters[orders[i].stage] += 1;
				orderCards.push(createOrderCard(orderStages[parseInt(orders[i].stage)], orders[i]));
			}

			// Update the UI with order details
			let orderBodies = document.getElementsByClassName('accordion-body');
			for (let i = 0; i < orderBodies.length; i++) {
				orderBodies[i].classList.remove('placeholder-glow');
				if (orderBodies[i].id === 'received_orders_body') {
					for (let j = 0; j < orderBodies[i].children.length; j++) {
						if (orderBodies[i].children[j].dataset.orderId) {
							unconfirmedOrderIds.push(orderBodies[i].children[j].dataset.orderId);
						}
					}
				}

				orderBodies[i].innerHTML = "";

				const noOrders = document.createElement('div');
				noOrders.classList.add('text-uppercase', 'text-center', 'w-100');
				noOrders.innerText = `*** no orders ${orderStages[i] === 'received' ? 'unconfirmed' : orderStages[i] === 'complete' ? 'completed' : orderStages[i]} ***`;
				orderBodies[i].appendChild(noOrders);
			}

			let counterBadges = document.getElementsByClassName('counter-badge');
			for (let i = 0; i < counterBadges.length; i++) {
				counterBadges[i].innerHTML = "";
			}

			// Handle counters
			if (counters[0] > 0) {
				document.getElementById('received_orders_body').innerHTML = '';
				const counter = document.getElementById('received_counter');
				counter.removeAttribute('hidden');
				counter.innerText = counters[0];
			}

			if (counters[1] > 0) {
				document.getElementById('in_progress_orders_body').innerHTML = '';
				const counter = document.getElementById('in_progress_counter');
				counter.removeAttribute('hidden');
				counter.innerText = counters[1];
			}

			if (counters[2] > 0) {
				document.getElementById('ready_orders_body').innerHTML = '';
				const counter = document.getElementById('ready_counter');
				counter.removeAttribute('hidden');
				counter.innerText = counters[2];
			}

			if (counters[3] > 0) {
				document.getElementById('complete_orders_body').innerHTML = '';
				const counter = document.getElementById('complete_counter');
				counter.removeAttribute('hidden');
				counter.innerText = counters[3];
			}

			let newOrderCounter = 0;

			orderCards.forEach(card => {
				const stage = card.dataset.orderStage;
				const containerId = `${orderStages[parseInt(stage)].replace("-", "_")}_orders_body`;

				if (initialized && parseInt(stage) === 0 && !unconfirmedOrderIds.includes(card.dataset.orderId)) {
					console.log('new order received!');
					newOrderCounter++;
				}

				document.getElementById(containerId).append(card);
			});

			if (newOrderCounter > 0) {
				const container = document.createElement('div');
				container.style = 'width: 100dvw;';
				container.classList.add('text-center', 'h4');

				const flashyText = document.createElement('span');
				flashyText.classList.add('fa-fade');
				flashyText.innerHTML = `&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;${newOrderCounter} new order${newOrderCounter > 1 ? 's' : ''} received!&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;`

				const icon = document.createElement('i');
				icon.classList.add('fa-duotone', 'fa-sharp', 'fa-pizza-slice', 'fa-bounce');
				icon.style = '--fa-primary-color: #c09807; --fa-secondary-color: #edda02;';

				container.appendChild(icon.cloneNode());
				container.appendChild(flashyText);
				container.appendChild(icon);

				new AlertBanner(
					null,
					container,
					alerts.newOrderAlert,
					15,
					'success'
				);
            }

			initialized = true;
		})
		.catch(error => {
			if (failedLoad++ < MAX_RETRY_LOAD) {
				displayReconnectBanner()
				setTimeout(loadOrders, FAILED_LOAD_TIMEOUT);
			} else {
				// Display an alert banner for repeated failures
				AlertBanner.dismissById(reconnectingBannerId);
				const banner = new AlertBanner(
					"Server Connection Lost",
					"Something went wrong while trying to reach the server. Please check your internet connection and tap this message to try again. If the issue continues or happens frequently, please contact support.",
					alerts.notification,
					-1, // Persistent
					"danger",
					() => checkConnection()
				);

				connectionTimeoutBannerId = banner.id;
			}
		});
};

const updatePrinterStatus = () => {
	const mainIcon = document.getElementById('printer_connection_status');
	const paperIcon = document.getElementById('printer_paper_status');
	const errorIcon = document.getElementById('printer_error_status')

    /**
     * @param {PrinterStatus} currentStatus
     */
	const updateCallback = currentStatus => {
		if (currentStatus) {
			mainIcon.classList.remove('default', 'pending', 'good', 'warning', 'error', 'fa-flash');
			paperIcon.classList.remove('default', 'pending', 'good', 'warning', 'error', 'fa-flash');
			errorIcon.classList.remove('default', 'pending', 'good', 'warning', 'error', 'fa-flash');

			if (currentStatus.isReady) mainIcon.classList.add('good');
			else if (currentStatus.isBridgeActive) mainIcon.classList.add('warning', 'fa-flash');
			else mainIcon.classList.add('error', 'fa-flash');

			if (!currentStatus.isReady) paperIcon.classList.add('pending', 'fa-flash');
			else if (!currentStatus.isPaperLow && !currentStatus.isPaperOut) paperIcon.classList.add('good');
			else if (currentStatus.isPaperLow && !currentStatus.isPaperOut) paperIcon.classList.add('warning', 'fa-flash');
			else paperIcon.classList.add('error', 'fa-flash');

			if (!currentStatus.isReady) errorIcon.classList.add('pending', 'fa-flash');
			else if (currentStatus.isCoverOpen || currentStatus.isErrorState) errorIcon.classList.add('error', 'fa-flash');
			else errorIcon.classList.add('default');
		}
	}

	fetch('/API/Staff/TerminalService/FetchPrinterStatus')
		.then(response => {
			return response.json();
		})
		.then(updateCallback);
}


let orderStatus = '';
let printerStatus = '';
let failedAttempts = 0;
let pingLockout = false;
const FAILED_PING_THRESHOLD = 24; // number of failed pings before attempts are cut off
let terminalUpdateInterval; // Store the interval reference for stopping updates if needed

const checkTerminalStatus = () => {
	if (pingLockout) {
		console.log('Awaiting previous response. Ping blocked.');
		return;
	}

	pingLockout = true;

	fetchWithTimeout('/API/Staff/TerminalService/Ping', {
		method: 'PUT',
		headers: {
			'Content-Type': 'application/json'
		},
		body: JSON.stringify({
			"State": `${orderStatus}:${printerStatus}`
		})
	}).then(response => {

		pingLockout = false;

		if (response.status === 200 || response.status === 204) {
			// Reset failure counter on a successful response
			if (failedAttempts > 0) {
				try {
					AlertBanner.dismissById(connectionTimeoutBannerId);
				} catch (err) {
					if (err instanceof Error && !err.message.includes('not found')) {
						throw err;
					}
				}
				try {
					AlertBanner.dismissById(reconnectingBannerId);
				} catch (err) {
					if (err instanceof Error && !err.message.includes('not found')) {
						throw err;
					}
				}

				AlertMessage.post(
					"Server Connection Restored",
					`The connection to the server was interrupted but has been restored after ${failedAttempts} attempt${failedAttempts === 1 ? '' : 's'}.`,
					true // silent
				);
			}
			failedAttempts = 0;

			if (response.status === 204) {
				return null; // No data to process
			}
			return response.text();
		} else if (response.status === 401 || response.status === 405) {
			// Unauthorized: User has been logged out
			clearInterval(terminalUpdateInterval);
			new AlertBanner(
				"You’ve Been Logged Out",
				"We are unable to connect to the server until you log back in. Click here to log in.",
				alerts.notification,
				-1,
				'danger',
				() => location.reload()
			).enableClickToDismiss();
		} else {
			throw new Error(`Unexpected response: ${response.status}`);
		}
	}).then(data => {
		if (data) {
			const status = data.split(':');
			if (orderStatus !== status[0]) {
				console.log('Order status changed!');
				loadOrders();
			}
			if (printerStatus !== status[1]) {
				console.log('Printer status changed!');
				updatePrinterStatus();
			}
			orderStatus = status[0];
			printerStatus = status[1];
		}
	}).catch((ex) => {
		pingLockout = false;

		failedAttempts++;
		displayReconnectBanner();
		if (failedAttempts >= FAILED_PING_THRESHOLD) {
			clearInterval(terminalUpdateInterval);
			AlertBanner.dismissById(reconnectingBannerId);
			new AlertBanner(
				"Server Connection Lost",
				"Something went wrong while trying to reach the server. Please check your internet connection and tap this message to try again. If the issue continues or happens frequently, please contact support.",
				alerts.notification,
				-1,
				'danger',
				() => retryConnection()
			).enableClickToDismiss();
		}
	});
};

const checkConnection = async () => {
	try {
		const res = await fetchWithTimeout('/API/System/Ping', {}, 8000);
		return res.ok;
	} catch {
		return false;
	}
};

const retryConnection = async () => {
	const online = await checkConnection();
	if (online) {
		// Hide banner, resume polling, etc.
		try {
			AlertBanner.dismissById(connectionTimeoutBannerId);
		} catch (err) {
			if (err instanceof Error && !err.message.includes('not found')) {
				throw err;
			}
		}

		try {
			AlertBanner.dismissById(reconnectingBannerId);
		} catch (err) {
			if (err instanceof Error && !err.message.includes('not found')) {
				throw err;
			}
		}

		checkTerminalStatus();
		AlertMessage.post("Connection Restored", "You're back online!", true);
	}
};

const displayReconnectBanner = () => {
	try {
		AlertBanner.dismissById(reconnectingBannerId);
	} catch (err) {
		if (err instanceof Error && !err.message.includes('not found')) {
			throw err;
		}
	}

	// Build container styled to match new order banner pattern
	const reconnectingContainer = document.createElement('div');
	reconnectingContainer.style = 'width: 100dvw;';
	reconnectingContainer.classList.add('text-center', 'h4');

	// Create spinner icon
	const spinnerIcon = document.createElement('i');
	spinnerIcon.classList.add('fa-solid', 'fa-spinner', 'fa-spin');
	spinnerIcon.style = '--fa-primary-color: #e1a200; --fa-secondary-color: #fcd84e;';

	// Create fading status text
	const reconnectingText = document.createElement('span');
	reconnectingText.innerHTML = `&nbsp;&nbsp;Reconnecting: Server connection lost, attempting to reconnect. <strong>No further action is needed at this time.</strong>`;


	// Assemble banner content
	reconnectingContainer.appendChild(spinnerIcon);
	reconnectingContainer.appendChild(reconnectingText);

	// Display the persistent, silent reconnecting banner
	const reconnectingBanner = new AlertBanner(
		null,                  // No title
		reconnectingContainer, // Custom spinner + text
		null,                  // Silent (no audio)
		-1,                    // Persistent
		'warning'              // Bootstrap warning color scheme
	);

	// Save the ID if you want to dismiss later
	reconnectingBannerId = reconnectingBanner.id;
}

terminalUpdateInterval = setInterval(checkTerminalStatus, 10000);

checkTerminalStatus();
updatePrinterStatus();

console.log('loaded terminal client 1.0.1');