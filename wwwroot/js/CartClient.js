var cartBtnStyles = "";

const confirmDelete = [];

const initCartModal = (message) => {
    $('#messagePlaceholder').text(message);

    //automatically delete partial on close
    $('#cart_modal').on('hidden.bs.modal', (e) => {
        $('#cart_modal').remove();
        $('#cart_btn').prop("hidden", false);
    });

    $('#cart_next_btn').on('click', (event) => {
        const target = retarget(event.target, 'ccs-btn');
        const id = target.data('cart-id')

        $.ajax({
            method: 'GET',
            url: `/Home/CheckoutModal?cartId=${id}`,
            success: (data, status, xhr) => {
                if (status == 'success') {
                    console.info(data)
                    $('#modal_container').append($.parseHTML(data, true));
                    $('#checkout_modal').modal('show');
                    $('#checkout_modal').on('modal.bs.hidden', () => {
                        $('#checkout_modal').remove();
                    });
                }
            }
        });
    });

    $('#promoCodeBtn').on('click', (event) => {
        event.preventDefault();
        const code = $('#promoCode').val();
        const cartId = retarget(event.target, "btn").data('cart-id');

        $.ajax({
            method: 'PUT',
            url: '/API/Cart/ApplyDiscount',
            headers: {
                Accept: '*/*',
                'Content-Type': 'application/json'
            },
            data: JSON.stringify({
                cartId: cartId,
                promoCode: code
            }),
            success: (data, status, xhr) => {
                console.log("code accepted.")
                if (status == 'success') {
                    $('#cart_modal').modal('hide');
                    setTimeout(() => {
                        $.ajax({
                            method: "GET",
                            url: `/Home/ViewCartModal?cartId=${cartId}`,
                            success: (modalData, status, xhr) => {
                                if (status == 'success') {
                                    document.getElementById('modal_container').innerHTML = modalData;
                                    console.log(data);
                                    initCartModal(data);
                                }
                            }
                        });
                    }, 1000);
                }
            },
            error: (data, status) => {
                $('#promoErr').text(data.responseText.replaceAll('"', ''))
            }
        })
    });

    $('#cart_modal').modal('show');
    $('#cart_btn').prop("hidden", true);

    $('.cart-edit-line-btn').on('click', (event) => {
        event.preventDefault();
        event.stopPropagation();
        var target = retarget(event.target, 'cart-line-btn');

        var id = target.data('cart-id');
        var index = target.data('index');

        $('#cart_modal').modal('hide');
        $.ajax({
            method: 'GET',
            url: `/Home/EditItemModal?cartId=${id}&index=${index}`,
            success: (data, status, xhr) => {
                if (status == "success") {
                    document.getElementById('modal_container').innerHTML = data;
                    initEditModal(id);
                }
            }
        });
    });

    $('.cart-delete-line-btn').on('click', (event) => {
        event.preventDefault();
        event.stopPropagation();
        const target = retarget(event.target, 'cart-delete-line-btn');
        const cartId = target.data('cart-id');
        const index = target.data('index');
        const id = target.attr('id');

        if (confirmDelete.includes(id)) {

            confirmDelete.splice(confirmDelete.indexOf(id), 1);

            $('#cart_modal').off('hidden.bs.modal');
            $('#cart_modal').on('hidden.bs.modal', () => {
                const data = {
                    cartId: cartId,
                    index: index
                }
                $.ajax({
                    method: 'PUT',
                    url: '/API/Cart/RemoveItem',
                    headers: {
                        Accept: '*/*',
                        'Content-Type': 'application/json'
                    },
                    data: JSON.stringify(data),
                    success: (data, status, xhr) => {
                        $('#cart_counter').text(data.itemCount > 0 ? data.itemCount : "");
                        const badgeClasses = 'mx-2 px-2 fw-bold translate-middle p-1 bg-info border rounded-pill'
                        if (data.itemCount > 0) {
                            $('#cart_counter').addClass(badgeClasses);
                        }
                        else {
                            $('#cart_counter').removeClass(badgeClasses);
                        }
                        if (status == "success") {
                            $.ajax({
                                method: "GET",
                                url: `/Home/ViewCartModal?cartId=${cartId}`,
                                success: (data, status, xhr) => {
                                    if (status == 'success') {
                                        document.getElementById('modal_container').innerHTML = data;
                                        initCartModal();
                                    }
                                }
                            });
                        }
                    }
                });
            });
            $('#cart_modal').modal('hide');
        }
        else {
            confirmDelete.push(id);

            const popover = new bootstrap.Popover(`#${id}`, {
                container: '.modal-body',
                title: 'Confirm Removal',
                content: 'Click delete button again to remove from bag.',
                placement: 'left',
                customClass: 'confirm-delete-popup',
                trigger: 'focus'
            })
            popover.show();
        }                
    });
}

const initEditModal = (id) => {
    $('#edit_item_modal').on('hidden.bs.modal', (e) => {

        $('#edit_item_modal').remove();
        $.ajax({
            method: "GET",
            url: `/Home/ViewCartModal?cartId=${id}`,
            success: (data, status, xhr) => {
                if (status == 'success') {
                    document.getElementById('modal_container').innerHTML = data;
                    initCartModal();
                }
            }
        });
    });

    $('#edit_item_modal').modal('show');

    $('#edit_item_modal').on('shown.bs.modal', () => {
        $('#cart_btn').prop("hidden", true);
    });

    $('#save_edit').on('click', (e) => {
        var data = {
            cartId: $('#cart_id').val(),
            index: $("#item_index").val(),
            varientIndex: $('.varient-input:checked').val(),
            lineComments: $('#line_comment').val() == "" ? null : $('#line_comment').val(),
            ingredientIds: []
        };

        $('.ingredient-input:checked').each((i, element) => {
            data.ingredientIds.push(element.value);
        });

        $.ajax({
            method: "PUT",
            url: "/API/Cart/UpdateItem",
            headers: {
                Accept: '*/*',
                'Content-Type': 'application/json'
            },
            data: JSON.stringify(data),
            success: (data, status, xhr) => {
                if (status == 'success') {
                    console.log('item updated!');
                    console.info(data);

                    $('#edit_item_modal').modal('hide');
                }
                else {
                    console.log('failed to update item.');
                    console.info(data);
                }
            }
        });
    });

    $('#line_comment').on('input', (event) => {
        document.getElementById('comment_chars_used').innerText = event.target.value.length;
    });
}

/**
 * Initializes the client script and binds all buttons.
 */
const initClient = () => {
    cartBtnStyles = $('#cart_btn').attr('style') ?? "";

    $('.ccs-btn').on('click', (event) => {
        event.preventDefault();
        event.stopPropagation();
        var target = retarget(event.target, 'ccs-btn');

        var cartId = target.data('ccs-id');
        var action = target.data('ccs-action');

        switch (action) {
            //view cart
            case 'view':
                $.ajax({
                    method: 'GET',
                    url: `/Home/ViewCartModal?cartId=${cartId}`,
                    success: (data, status, xhr) => {
                        if (status == 'success') {
                            document.getElementById('modal_container').innerHTML = data;
                            initCartModal();
                        }
                        else {
                            alert(`failed to load user cart, status: ${status}`);
                        }
                    }
                });
                break;

            //add item
            case 'add':
                var itemId = target.data('ccs-item-id');
                var varientIndex = target.data('ccs-varient-index');
                var animationStyles = "--fa-animation-iteration-count: 3; --fa-animation-duration: 1s";
                console.log(`${cartId}\t${itemId}\t${varientIndex}`);

                var model = {
                    'cartId': cartId,
                    'itemId': itemId,
                    'varient': varientIndex
                }

                console.info(model);

                $.ajax({
                    method: 'PUT',
                    url: '/API/Cart/AddItem',
                    headers: {
                        Accept: '*/*',
                        'Content-Type': 'application/json'
                    },
                    data: JSON.stringify(model),
                    success: (data, status, xhr) => {
                        $('#cart_counter').text(data.itemCount > 0 ? data.itemCount : "");
                        const badgeClasses = 'mx-2 px-2 fw-bold translate-middle p-1 bg-info border rounded-pill'
                        if (data.itemCount > 0) {
                            $('#cart_counter').addClass(badgeClasses);
                        }
                        else {
                            $('#cart_counter').removeClass(badgeClasses);
                        }
                        $('#cart_btn')
                            .addClass('fa-bounce')
                            .attr('style', `${cartBtnStyles} ${animationStyles}`);

                        setTimeout(() => {
                            $('#cart_btn')
                                .removeClass('fa-bounce')
                                .attr('style', cartBtnStyles)
                        }, 3000);
                    },
                    error: (err) => {
                        console.info(err);
                    }
                });
                break;
        }
    });
}

const retarget = (initialTarget, targetClass) => {
    return target = $(initialTarget).hasClass(targetClass) ? $(initialTarget) : $(initialTarget).parents(`.${targetClass}`);
}

initClient();