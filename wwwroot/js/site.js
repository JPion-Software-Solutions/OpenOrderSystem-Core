// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
/**
 * Converts an input string to a properly formatted price 
 * @param {string} input
 * @returns {number}
 */
const convertPriceInput = (input) => {
    var price = +input;
    if (input.includes('.')) {
        price = price.toFixed(2);
    }
    else {
        price = (price / 100).toFixed(2);
    }

    return price;
}